using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using global::DotNetty.Transport.Bootstrapping;
using global::DotNetty.Transport.Channels.Sockets;
using global::DotNetty.Transport.Channels;
using System.Threading.Channels;

namespace Assi.DotNetty.FileTransmission
{
    public class EnhancedFileClient
    {
        // ====== 新增字段 ======
        private readonly object _ackLock = new();
        private readonly Dictionary<long, TaskCompletionSource<bool>> _ackCallbacks = new();
        private IChannel _channel; // 保存连接通道
        private readonly string _host;
        private readonly int _port;
        private string fileName;
        private long startOffset;

        public EnhancedFileClient(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public async Task ConnectAndDownloadFile(string fileName, long startOffset = 0)
        {
            this.fileName = fileName;
            this.startOffset = startOffset;
            var group = new MultithreadEventLoopGroup();
            try
            {
                var bootstrap = new Bootstrap();
                bootstrap.Group(group)
                         .Channel<TcpSocketChannel>()
                         .Handler(new ActionChannelInitializer<ISocketChannel>(channel =>
                         {
                             channel.Pipeline.AddLast(new FileChunkMessageEncoder());
                             channel.Pipeline.AddLast(new FileChunkMessageDecoder());
                             channel.Pipeline.AddLast(new FileClientHandler(fileName, 0, "downloads/") 
                             {
                                 OnAckReceived = HandleAckReceived
                             });
                         }));

                _channel = await bootstrap.ConnectAsync(_host, _port);

                // 发送文件请求（包含偏移量）
                var request = new FileChunkMessage
                {
                    Header = new FileMessageHeader
                    {
                        Type = MessageType.FileRequest,
                        FileName = fileName,
                        Offset = startOffset
                    }
                };

                await _channel.WriteAndFlushAsync(request);
                Console.WriteLine($"已发送文件请求: {fileName}");
                await _channel.CloseCompletion;
            }
            finally
            {
                await group.ShutdownGracefullyAsync();
            }
        }

        public async Task ConnectAndUploadFile(string filepath)
        {
            var group = new MultithreadEventLoopGroup();
            try
            {
                var bootstrap = new Bootstrap();
                bootstrap.Group(group)
                         .Channel<TcpSocketChannel>()
                         .Handler(new ActionChannelInitializer<ISocketChannel>(channel =>
                         {
                             channel.Pipeline.AddLast(new FileChunkMessageEncoder());
                             channel.Pipeline.AddLast(new FileChunkMessageDecoder());
                             channel.Pipeline.AddLast(new FileClientHandler(string.Empty, 0, "downloads/")
                             {
                                 OnAckReceived = HandleAckReceived
                             });
                         }));

                _channel = await bootstrap.ConnectAsync(_host, _port);
                await UploadFileAsync(filepath);
            }
            finally
            {
                await group.ShutdownGracefullyAsync();
            }
        }

        // ====== 新增方法：处理服务端返回的 Ack ======
        private void HandleAckReceived(FileMessageHeader header)
        {
            lock (_ackLock)
            {
                if (_ackCallbacks.TryGetValue(header.Offset, out var tcs))
                {
                    tcs.SetResult(true);
                    _ackCallbacks.Remove(header.Offset);
                }
            }
        }

        // ====== 辅助方法：发送分块并等待 Ack ======
        private async Task SendChunkAndWaitForAck(IChannel channel, FileChunkMessage chunk, CancellationToken ct,int retryCount = 0)
        {
            // 改为等待分片结束位置的ACK（而不是起始位置）
            long expectedAckOffset = chunk.Header.Offset + chunk.Header.DataLength;
            var tcs = new TaskCompletionSource<bool>();
            lock (_ackLock)
            {
                _ackCallbacks[expectedAckOffset] = tcs; // 修改键值
            }

            await channel.WriteAndFlushAsync(chunk);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            //cts.CancelAfter(TimeSpan.FromSeconds(5)); // 设置超时时间
            try
            {
                await tcs.Task.WaitAsync(cts.Token); // 等待 Ack
            }
            catch (OperationCanceledException)
            {
                // 限制重试次数（避免无限递归）
                if (retryCount++ < 3)
                {
                    Console.WriteLine($"分块 {chunk.Header.Offset} 超时，第{retryCount}次重传");
                    await SendChunkAndWaitForAck(channel, chunk, ct,retryCount);
                }
                else
                {
                    throw new TimeoutException($"分块 {chunk.Header.Offset} 重传失败");
                }
            }
        }

        // ====== 上传主方法 ======
        public async Task UploadFileAsync(string filePath, CancellationToken ct = default)
        {
            
            if (_channel == null || !_channel.Active)
                throw new InvalidOperationException("未建立连接");

            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
                throw new FileNotFoundException("指定的文件不存在", filePath);

            // 发送上传请求
            var request = new FileChunkMessage
            {
                Header = new FileMessageHeader
                {
                    Type = MessageType.UploadRequest,
                    FileName = fileInfo.Name,
                    FileSize = fileInfo.Length,
                    Offset = 0
                }
            };

            // 发送上传请求后等待初始ACK
            await SendChunkAndWaitForAck(_channel, request, ct);

            const int chunkSize = 8192;
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.Asynchronous);
            byte[] buffer = new byte[chunkSize];
            long currentOffset = 0;

            while (currentOffset < fileInfo.Length && !ct.IsCancellationRequested)
            {
                int bytesRead = await fileStream.ReadAsync(buffer, 0, chunkSize, ct);
                if (bytesRead <= 0) break;

                var chunk = new FileChunkMessage
                {
                    Header = new FileMessageHeader
                    {
                        Type = MessageType.UploadChunk,
                        FileName = fileInfo.Name,
                        Offset = currentOffset,
                        DataLength = bytesRead
                    },
                    Data = buffer.Take(bytesRead).ToArray()
                };

                // 使用封装好的方法发送并等待确认
                await SendChunkAndWaitForAck(_channel, chunk, ct);
                currentOffset += bytesRead;
                OnUploadProgress(currentOffset, fileInfo.Length);
            }

            // 发送完成通知
            await _channel.WriteAndFlushAsync(new FileChunkMessage
            {
                Header = new FileMessageHeader
                {
                    Type = MessageType.Complete,
                    FileName = fileInfo.Name
                }
            });
        }

        // ====== 事件和清理方法 ======
        private bool _isClosed = false;
        private readonly object _closeLock = new();

        public async Task CloseAsync()
        {
            lock (_closeLock)
            {
                if (_isClosed)
                    return;

                _isClosed = true;
            }

            try
            {
                // 1. 关闭连接通道
                if (_channel != null && _channel.Active)
                {
                    await _channel.CloseAsync();
                }

                // 2. 取消所有未完成的 Ack 任务
                lock (_ackLock)
                {
                    foreach (var tcs in _ackCallbacks.Values)
                    {
                        tcs.TrySetCanceled();
                    }
                    _ackCallbacks.Clear();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"关闭客户端时发生异常: {ex.Message}");
            }
        }

        public event Action<long, long> UploadProgress;

        private void OnUploadProgress(long current, long total)
        {
            UploadProgress?.Invoke(current, total);
        }

        public void Dispose()
        {
            try
            {
                // 同步等待异步关闭完成（适用于 Dispose 模式）
                CloseAsync().Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dispose 时发生异常: {ex.Message}");
            }
        }
    }
}
