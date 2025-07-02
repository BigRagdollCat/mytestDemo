using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Assi.DotNetty.UdpFileTransmission
{
    public class UDPFileClient : IDisposable
    {
        private readonly UdpClient _udpClient;
        private readonly IPEndPoint _serverEP;
        private readonly ConcurrentDictionary<uint, FileMessage> _pendingPackets = new();
        private readonly ManualResetEventSlim _directoryReadyEvent = new(false);
        private readonly ConcurrentDictionary<string, FileStream> _broadcastFiles = new();
        private readonly Timer _retryTimer;
        private Timer _heartbeatTimer;

        private uint _sequenceCounter = 1;
        private Guid _currentSessionId;
        private ClientTransferState _currentTransfer;
        private string _clientName;
        private bool _isRunning;
        private Thread _receiveThread;
        private CancellationTokenSource _receiveCts;
        private FileStream _currentDownload;

        // 事件
        public event Action OnConnected;
        public event Action<string> OnDisconnected;
        public event Action<string> OnFileReceived;
        public event Action<string, string> OnTransferFailed;

        public UDPFileClient(string serverIp, int serverPort)
        {
            _serverEP = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);
            _udpClient = new UdpClient();
            _retryTimer = new Timer(RetryPendingPackets, null, Timeout.Infinite, Timeout.Infinite);
            _receiveCts = new CancellationTokenSource();
        }

        public async Task StartAsync(string clientName)
        {
            if (_isRunning) return;

            _isRunning = true;
            _clientName = clientName;
            _currentSessionId = Guid.NewGuid();

            // 发送注册消息
            await SendRegisterMessage();

            // 启动接收线程
            _receiveThread = new Thread(ReceiveLoop);
            _receiveThread.IsBackground = true;
            _receiveThread.Start();

            // 启动定时器
            _retryTimer.Change(100, 100);
            _heartbeatTimer = new Timer(SendHeartbeat, null, 30000, 30000);

            Console.WriteLine("Client started");
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _receiveCts.Cancel();
            _udpClient.Close();

            // 停止定时器
            _retryTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _heartbeatTimer?.Dispose();

            // 清理资源
            foreach (var fs in _broadcastFiles.Values)
            {
                fs.Dispose();
            }
            _broadcastFiles.Clear();

            _currentDownload?.Dispose();
            _currentDownload = null;

            Console.WriteLine("Client stopped");
        }

        public async Task UploadAsync(string path)
        {
            if (!_isRunning)
                throw new InvalidOperationException("Client must be started before uploading");

            try
            {
                _currentTransfer = new ClientTransferState(path);
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
                OnTransferFailed?.Invoke(path, ex.Message);
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
                    FileSize = transfer.FileSize,
                    ClientName = _clientName
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
                        IsLastChunk = isLast,
                        ClientName = _clientName
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
                    FileSize = file.Size,
                    ClientName = _clientName
                }
            };
            await SendWithRetry(fileStart);

            using var fs = new FileStream(file.LocalPath, FileMode.Open, FileAccess.Read);
            var buffer = new byte[RUDPProtocol.MaxPacketSize - 200];
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
                        IsLastChunk = isLast,
                        ClientName = _clientName
                    },
                    Data = chunkData
                };
                offset += bytesRead;
                await SendWithRetry(chunk);
            }
        }

        private async Task SendRegisterMessage()
        {
            var registerMsg = new FileMessage
            {
                Header = new FileMessageHeader
                {
                    Type = MessageType.ClientRegister,
                    SessionId = _currentSessionId,
                    ClientName = _clientName
                }
            };
            await SendWithRetry(registerMsg);
        }

        private void SendHeartbeat(object state)
        {
            try
            {
                var heartbeat = new FileMessage
                {
                    Header = new FileMessageHeader
                    {
                        Type = MessageType.Heartbeat,
                        SessionId = _currentSessionId,
                        ClientName = _clientName
                    }
                };
                var packet = RUDPProtocol.CreatePacket(heartbeat);
                _udpClient.Send(packet, packet.Length, _serverEP);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Heartbeat failed: {ex.Message}");
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
                    Sequence = GetNextSequence(),
                    ClientName = _clientName
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
                    Sequence = GetNextSequence(),
                    ClientName = _clientName
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
                    Sequence = GetNextSequence(),
                    ClientName = _clientName
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
                    DataLength = chunkData.Length,
                    ClientName = _clientName
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
                    Sequence = GetNextSequence(),
                    ClientName = _clientName
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
            if (!_isRunning) return;

            foreach (var kvp in _pendingPackets)
            {
                try
                {
                    var packet = RUDPProtocol.CreatePacket(kvp.Value);
                    _udpClient.Send(packet, packet.Length, _serverEP);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Retry error: {ex.Message}");
                }
            }
        }

        private void ReceiveLoop()
        {
            try
            {
                Console.WriteLine("Client receive loop started");
                while (_isRunning && !_receiveCts.IsCancellationRequested)
                {
                    try
                    {
                        var result = _udpClient.ReceiveAsync().GetAwaiter().GetResult();
                        var message = RUDPProtocol.ParsePacket(result.Buffer);
                        if (message == null) continue;

                        ProcessMessage(message);
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted)
                    {
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (_isRunning)
                            Console.WriteLine($"Receive error: {ex}");
                    }
                }
            }
            finally
            {
                Console.WriteLine("Client receive loop exited");
            }
        }

        private void ProcessMessage(FileMessage message)
        {
            switch (message.Header.Type)
            {
                case MessageType.FileAck:
                    _pendingPackets.TryRemove(message.Header.AckSequence, out _);
                    break;

                case MessageType.Error:
                    Console.WriteLine($"Server error: {message.Header.FileName}");
                    OnTransferFailed?.Invoke("Server", message.Header.FileName);
                    break;

                case MessageType.DirectoryComplete:
                    _directoryReadyEvent.Set();
                    break;

                case MessageType.BroadcastStart:
                    HandleBroadcastStart(message);
                    break;

                case MessageType.BroadcastChunk:
                    HandleBroadcastChunk(message);
                    break;

                case MessageType.BroadcastEnd:
                    CompleteBroadcast(message);
                    break;

                case MessageType.FileStart:
                    StartFileTransfer(message);
                    break;

                case MessageType.FileChunk:
                    ProcessFileChunk(message);
                    break;

                case MessageType.FileEnd:
                    CompleteFileTransfer(message);
                    break;
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
                OnFileReceived?.Invoke(message.Header.FileName);
                Console.WriteLine($"Broadcast received: {message.Header.FileName}");
            }
        }

        private void StartFileTransfer(FileMessage message)
        {
            string savePath = Path.Combine("Downloads", message.Header.FileName);
            _currentDownload = File.Create(savePath);
            Console.WriteLine($"Receiving file: {message.Header.FileName}");
        }

        private void ProcessFileChunk(FileMessage message)
        {
            if (_currentDownload != null)
            {
                _currentDownload.Write(message.Data, 0, message.Header.DataLength);
                SendAck(message.Header.Sequence);
            }
        }

        private void CompleteFileTransfer(FileMessage message)
        {
            if (_currentDownload != null)
            {
                _currentDownload.Dispose();
                _currentDownload = null;
                OnFileReceived?.Invoke(message.Header.FileName);
                Console.WriteLine($"File received: {message.Header.FileName}");
            }
        }

        private void SendAck(uint sequence)
        {
            var ack = new FileMessage
            {
                Header = new FileMessageHeader
                {
                    Type = MessageType.FileAck,
                    AckSequence = sequence,
                    ClientName = _clientName
                }
            };
            var packet = RUDPProtocol.CreatePacket(ack);
            _udpClient.Send(packet, packet.Length, _serverEP);
        }

        private uint GetNextSequence() => _sequenceCounter++;

        public void Dispose()
        {
            Stop();
            _retryTimer?.Dispose();
            _heartbeatTimer?.Dispose();
            _directoryReadyEvent?.Dispose();
            _receiveCts?.Dispose();
        }
    }
}
