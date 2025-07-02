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
        private readonly ConcurrentDictionary<string, FileStream> _broadcastFiles = new ConcurrentDictionary<string, FileStream>();

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
            _directoryReadyEvent.Reset();
            await SendDirectoryStart();

            var chunks = DirectorySerializer.SerializeDirectoryStructure(_currentTransfer.DirectoryRoot);

            foreach (var chunk in chunks)
            {
                await SendDirectoryChunk(chunk);
            }

            await SendDirectoryEnd();

            if (!await WaitForDirectoryReady())
                throw new Exception("Directory structure not ready on server");

            foreach (var file in _currentTransfer.FileItems)
            {
                await UploadFile(file);
            }
        }

        private async Task<bool> WaitForDirectoryReady()
        {
            return await Task.Run(() => _directoryReadyEvent.Wait(TimeSpan.FromSeconds(30)));
        }

        private async Task UploadFile(ClientTransferState transfer)
        {
            Guid dirID = transfer.DirectoryRoot?.DirID ?? Guid.Empty;

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
            var buffer = new byte[RUDPProtocol.MaxPacketSize - 200];
            int bytesRead;
            long offset = 0;

            while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                var isLast = offset + bytesRead >= transfer.FileSize;
                var chunkData = new byte[bytesRead];
                Array.Copy(buffer, 0, chunkData, 0, bytesRead);

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
                    Data = chunkData
                };
                offset += bytesRead;
                await SendWithRetry(chunk);
            }
        }

        private async Task UploadFile(FileItem file)
        {
            var fileStart = new FileMessage
            {
                Header = new FileMessageHeader
                {
                    Type = MessageType.FileStart,
                    SessionId = _currentSessionId,
                    Sequence = GetNextSequence(),
                    DirID = file.DirID,
                    FileName = file.Name,
                    FileSize = file.Size
                }
            };
            await SendWithRetry(fileStart);

            // 计算动态负载大小
            int dynamicPayloadSize = RUDPProtocol.CalculateMaxPayload(
                new FileMessageHeader
                {
                    FileName = file.Name,
                    DirID = file.DirID
                });

            // 使用动态计算或保守值
            int chunkSize = Math.Max(dynamicPayloadSize, 1024);

            using var fs = new FileStream(file.LocalPath, FileMode.Open, FileAccess.Read);
            var buffer = new byte[chunkSize];  // 使用正确的大小

            int bytesRead;
            long offset = 0;

            while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                var isLast = offset + bytesRead >= file.Size;
                var chunkData = new byte[bytesRead];
                Array.Copy(buffer, 0, chunkData, 0, bytesRead);

                var chunk = new FileMessage
                {
                    Header = new FileMessageHeader
                    {
                        Type = MessageType.FileChunk,
                        SessionId = _currentSessionId,
                        Sequence = GetNextSequence(),
                        DirID = file.DirID,
                        FileName = file.Name,
                        Offset = offset,
                        DataLength = bytesRead,
                        IsLastChunk = isLast
                    },
                    Data = chunkData
                };
                offset += bytesRead;
                await SendWithRetry(chunk);
            }
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
                if (!_pendingPackets.ContainsKey(sequence)) return;
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
                        _directoryReadyEvent.Set();
                    }
                    else if (message.Header.Type == MessageType.BroadcastStart)
                    {
                        HandleBroadcastStart(message);
                    }
                    else if (message.Header.Type == MessageType.BroadcastChunk)
                    {
                        HandleBroadcastChunk(message);
                    }
                    else if (message.Header.Type == MessageType.BroadcastEnd)
                    {
                        CompleteBroadcast(message);
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

        private void HandleBroadcastStart(FileMessage message)
        {
            string fileName = message.Header.FileName;
            string filePath = Path.Combine("Broadcasts", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));

            var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            _broadcastFiles[fileName] = fs;
        }

        private void HandleBroadcastChunk(FileMessage message)
        {
            if (_broadcastFiles.TryGetValue(message.Header.FileName, out var fs))
            {
                fs.Seek(message.Header.Offset, SeekOrigin.Begin);
                fs.Write(message.Data, 0, message.Header.DataLength);
            }
        }

        private void CompleteBroadcast(FileMessage message)
        {
            if (_broadcastFiles.TryRemove(message.Header.FileName, out var fs))
            {
                fs.Dispose();
                Console.WriteLine($"Broadcast received: {message.Header.FileName}");
            }
        }

        private uint GetNextSequence() => _sequenceCounter++;

        public void Dispose()
        {
            _retryTimer?.Dispose();
            _udpClient?.Dispose();
            _directoryReadyEvent?.Dispose();
            foreach (var fs in _broadcastFiles.Values)
            {
                fs.Dispose();
            }
            _broadcastFiles.Clear();
        }
    }
}
