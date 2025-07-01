using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Assi.DotNetty.UdpFileTransmission
{
    public class UDPFileClient : IDisposable
    {
        private readonly UdpClient _udpClient;
        private readonly IPEndPoint _serverEP;
        private readonly ConcurrentDictionary<uint, FileMessage> _pendingPackets = new ConcurrentDictionary<uint, FileMessage>();
        private readonly Timer _retryTimer;
        private uint _sequenceCounter = 1;
        private Guid _currentSessionId;
        private ClientTransferState _currentTransfer;
        private readonly ManualResetEventSlim _directoryReadyEvent = new ManualResetEventSlim(false);

        public UDPFileClient(string serverIp, int serverPort)
        {
            _udpClient = new UdpClient();
            _serverEP = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);
            _retryTimer = new Timer(RetryPendingPackets, null, 100, 100);
            Task.Run(ReceiveLoop);
        }

        public async Task UploadAsync(string path)
        {
            try
            {
                _currentTransfer = new ClientTransferState(path);
                _currentSessionId = _currentTransfer.SessionId;

                await SendSessionStart();

                if (_currentTransfer.IsDirectory)
                {
                    await UploadDirectory();
                }
                else
                {
                    await UploadFile(_currentTransfer);
                }

                await SendSessionEnd();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Upload failed: {ex.Message}");
                throw;
            }
        }

        private async Task UploadDirectory()
        {
            // 重置目录准备事件
            _directoryReadyEvent.Reset();

            // 发送目录开始消息
            await SendDirectoryStart();

            // 序列化目录结构并分片
            var chunks = DirectorySerializer.SerializeDirectoryStructure(_currentTransfer.DirectoryRoot);

            // 发送目录块
            foreach (var chunk in chunks)
            {
                await SendDirectoryChunk(chunk);
            }

            // 发送目录结束消息
            await SendDirectoryEnd();

            // 等待服务端确认目录准备完成
            if (!await WaitForDirectoryReady())
                throw new Exception("Directory structure not ready on server");

            // 传输文件
            foreach (var file in _currentTransfer.FileItems)
            {
                await UploadFile(file);
            }
        }

        private async Task<bool> WaitForDirectoryReady()
        {
            // 等待目录准备事件或超时
            return await Task.Run(() => _directoryReadyEvent.Wait(TimeSpan.FromSeconds(30)));
        }

        private async Task UploadFile(ClientTransferState transfer)
        {
            // 对于单个文件传输
            Guid dirID = Guid.Empty; // 单个文件没有目录ID

            var fileStart = new FileMessage
            {
                Header = new FileMessageHeader
                {
                    Type = MessageType.FileStart,
                    SessionId = _currentSessionId,
                    Sequence = GetNextSequence(),
                    DirID = dirID,
                    FileName = transfer.FileName,
                    FileSize = transfer.FileSize
                }
            };
            await SendWithRetry(fileStart);

            using var fs = new FileStream(transfer.BasePath, FileMode.Open, FileAccess.Read);
            var buffer = new byte[RUDPProtocol.MaxPayloadSize];
            int bytesRead;
            long offset = 0;

            while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                var isLast = offset + bytesRead >= transfer.FileSize;
                var chunk = new FileMessage
                {
                    Header = new FileMessageHeader
                    {
                        Type = MessageType.FileChunk,
                        SessionId = _currentSessionId,
                        Sequence = GetNextSequence(),
                        DirID = dirID,
                        FileName = transfer.FileName,
                        Offset = offset,
                        DataLength = bytesRead,
                        IsLastChunk = isLast
                    },
                    Data = new byte[bytesRead]
                };
                Array.Copy(buffer, 0, chunk.Data, 0, bytesRead);
                offset += bytesRead;
                await SendWithRetry(chunk);
            }
        }

        private async Task UploadFile(FileItem file)
        {
            // 获取文件所属的目录ID
            // 注意：在目录元数据中，每个文件应该已经关联了目录ID
            // 这里我们需要从目录树中找到文件的目录ID
            Guid dirID = FindDirectoryIdForFile(file);
            if (dirID == Guid.Empty)
            {
                throw new Exception($"Directory ID not found for file: {file.Name}");
            }

            // 发送文件开始
            var fileStart = new FileMessage
            {
                Header = new FileMessageHeader
                {
                    Type = MessageType.FileStart,
                    SessionId = _currentSessionId,
                    Sequence = GetNextSequence(),
                    DirID = dirID,
                    FileName = file.Name,
                    FileSize = file.Size
                }
            };
            await SendWithRetry(fileStart);

            // 发送文件内容
            using var fs = new FileStream(file.LocalPath, FileMode.Open, FileAccess.Read);
            var buffer = new byte[RUDPProtocol.MaxPayloadSize];
            int bytesRead;
            long offset = 0;

            while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                var isLast = offset + bytesRead >= file.Size;
                var chunk = new FileMessage
                {
                    Header = new FileMessageHeader
                    {
                        Type = MessageType.FileChunk,
                        SessionId = _currentSessionId,
                        Sequence = GetNextSequence(),
                        DirID = dirID,
                        FileName = file.Name,
                        Offset = offset,
                        DataLength = bytesRead,
                        IsLastChunk = isLast
                    },
                    Data = new byte[bytesRead]
                };
                Array.Copy(buffer, 0, chunk.Data, 0, bytesRead);
                offset += bytesRead;
                await SendWithRetry(chunk);
            }
        }

        private Guid FindDirectoryIdForFile(FileItem file)
        {
            // 在目录树中搜索包含指定文件的目录
            return FindDirectoryIdRecursive(_currentTransfer.DirectoryRoot, file);
        }

        private Guid FindDirectoryIdRecursive(DirectoryNode node, FileItem file)
        {
            // 检查当前目录是否包含该文件
            foreach (var f in node.Files)
            {
                if (f.LocalPath == file.LocalPath && f.Name == file.Name)
                {
                    return node.DirID;
                }
            }

            // 递归检查子目录
            foreach (var child in node.Children)
            {
                var foundId = FindDirectoryIdRecursive(child, file);
                if (foundId != Guid.Empty)
                {
                    return foundId;
                }
            }

            return Guid.Empty;
        }

        private async Task SendSessionStart()
        {
            var sessionStart = new FileMessage
            {
                Header = new FileMessageHeader
                {
                    Type = MessageType.SessionStart,
                    SessionId = _currentSessionId,
                    Sequence = GetNextSequence()
                }
            };
            await SendWithRetry(sessionStart);
        }

        private async Task SendSessionEnd()
        {
            var sessionEnd = new FileMessage
            {
                Header = new FileMessageHeader
                {
                    Type = MessageType.SessionEnd,
                    SessionId = _currentSessionId,
                    Sequence = GetNextSequence()
                }
            };
            await SendWithRetry(sessionEnd);
        }

        private async Task SendDirectoryStart()
        {
            var dirStart = new FileMessage
            {
                Header = new FileMessageHeader
                {
                    Type = MessageType.DirectoryStart,
                    SessionId = _currentSessionId,
                    Sequence = GetNextSequence()
                }
            };
            await SendWithRetry(dirStart);
        }

        private async Task SendDirectoryChunk(byte[] chunkData)
        {
            var message = new FileMessage
            {
                Header = new FileMessageHeader
                {
                    Type = MessageType.DirectoryChunk,
                    SessionId = _currentSessionId,
                    Sequence = GetNextSequence(),
                    DataLength = chunkData.Length
                },
                Data = chunkData
            };
            await SendWithRetry(message);
        }

        private async Task SendDirectoryEnd()
        {
            var dirEnd = new FileMessage
            {
                Header = new FileMessageHeader
                {
                    Type = MessageType.DirectoryEnd,
                    SessionId = _currentSessionId,
                    Sequence = GetNextSequence()
                }
            };
            await SendWithRetry(dirEnd);
        }

        private async Task SendWithRetry(FileMessage message)
        {
            uint sequence = message.Header.Sequence;
            _pendingPackets[sequence] = message;

            for (int i = 0; i < 5; i++)
            {
                var packet = RUDPProtocol.CreatePacket(message);
                _udpClient.Send(packet, packet.Length, _serverEP);
                await Task.Delay(200);
                if (!_pendingPackets.ContainsKey(sequence)) return; // 已确认
            }

            throw new TimeoutException($"Packet {sequence} not acknowledged");
        }

        private void RetryPendingPackets(object state)
        {
            foreach (var kvp in _pendingPackets)
            {
                var packet = RUDPProtocol.CreatePacket(kvp.Value);
                _udpClient.Send(packet, packet.Length, _serverEP);
            }
        }

        private async Task ReceiveLoop()
        {
            try
            {
                while (true)
                {
                    var result = await _udpClient.ReceiveAsync();
                    var message = RUDPProtocol.ParsePacket(result.Buffer);
                    if (message == null) continue;

                    if (message.Header.Type == MessageType.FileAck)
                    {
                        _pendingPackets.TryRemove(message.Header.AckSequence, out _);
                    }
                    else if (message.Header.Type == MessageType.Error)
                    {
                        Console.WriteLine($"Server error: {message.Header.FileName}");
                    }
                    else if (message.Header.Type == MessageType.DirectoryComplete)
                    {
                        // 目录准备完成，可以开始文件传输
                        _directoryReadyEvent.Set();
                    }
                    else if (message.Header.Type == MessageType.ProgressResponse)
                    {
                        Console.WriteLine($"Transfer progress: {message.Header.Offset}/{message.Header.FileSize} bytes");
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // 正常关闭
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Receive loop error: {ex}");
            }
        }

        private uint GetNextSequence() => _sequenceCounter++;

        public void Dispose()
        {
            _retryTimer?.Dispose();
            _udpClient?.Dispose();
            _directoryReadyEvent?.Dispose();
        }
    }
}
