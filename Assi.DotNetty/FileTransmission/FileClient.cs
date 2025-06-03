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
        private IChannel _channel; // 保存连接通道
        private readonly string _host;
        private readonly int _port;

        public EnhancedFileClient(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public async Task ConnectAndDownloadFile(string fileName, long startOffset = 0)
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
                             channel.Pipeline.AddLast(new FileClientHandler(fileName, 0, "downloads/"));
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

        public async Task UploadFileAsync(string filePath)
        {
            if (_channel == null || !_channel.Active)
            {
                throw new InvalidOperationException("未建立连接");
            }

            var fileInfo = new FileInfo(filePath);
            var request = new FileChunkMessage
            {
                Header = new FileMessageHeader
                {
                    Type = MessageType.UploadRequest,
                    FileName = fileInfo.Name,
                    FileSize = fileInfo.Length,
                    Offset = 0 // 初始偏移量（支持断点续传）
                }
            };

            await _channel.WriteAndFlushAsync(request);

            // 分块读取并发送
            const int chunkSize = 8192;
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            byte[] buffer = new byte[chunkSize];
            long currentOffset = 0;

            while (currentOffset < fileInfo.Length)
            {
                int bytesRead = fileStream.Read(buffer, 0, chunkSize);
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

                await _channel.WriteAndFlushAsync(chunk);
                currentOffset += bytesRead;

                // 可选：等待服务器 Ack 确认
                await Task.Delay(10);
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
    }
}
