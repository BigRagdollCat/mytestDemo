using System.Text;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Assi.DotNetty.UdpFileTransmission
{
    public class UDPFileServer : IDisposable
    {
        private readonly UdpClient _udpClient;
        private readonly CancellationTokenSource _cts;
        private readonly ConcurrentDictionary<Guid, ClientSession> _sessions = new();
        private readonly ConcurrentDictionary<string, Guid> _clientNameToId = new();
        private readonly string _storagePath;
        private readonly int _port;
        private Thread _receiveThread;
        private Timer _retryTimer;
        private Timer _monitorTimer;
        private bool _isRunning;

        // 事件系统
        public event Action<ClientSession> OnClientConnected;
        public event Action<ClientSession> OnClientDisconnected;
        public event Action<ClientSession, string> OnUploadStarted;
        public event Action<ClientSession, string, long> OnUploadCompleted;
        public event Action<ClientSession, string> OnDownloadStarted;
        public event Action<ClientSession, string, long> OnDownloadCompleted;
        public event Action<ClientSession, string, string> OnTransferFailed;

        public IReadOnlyCollection<ClientSession> ConnectedClients => _sessions.Values.ToList().AsReadOnly();

        public UDPFileServer(int port, string storagePath)
        {
            _port = port;
            _storagePath = storagePath;
            _udpClient = new UdpClient(port);
            _cts = new CancellationTokenSource();
            Directory.CreateDirectory(storagePath);
            _retryTimer = new Timer(RetryPendingPackets, null, Timeout.Infinite, Timeout.Infinite);
            _monitorTimer = new Timer(MonitorClients, null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Start()
        {
            if (_isRunning) return;

            _isRunning = true;
            _receiveThread = new Thread(ReceiveLoop);
            _receiveThread.IsBackground = true;
            _receiveThread.Start();
            _retryTimer.Change(100, 100);
            _monitorTimer.Change(5000, 5000);
            Console.WriteLine($"Server started on port {_port}");
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _cts.Cancel();
            _udpClient.Close();

            // 清理所有会话
            foreach (var session in _sessions.Values)
            {
                session.Close();
            }
            _sessions.Clear();

            // 停止定时器
            _retryTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _monitorTimer?.Change(Timeout.Infinite, Timeout.Infinite);

            Console.WriteLine("Server stopped");
        }

        private void ReceiveLoop()
        {
            try
            {
                Console.WriteLine("Receive loop started");
                while (_isRunning && !_cts.IsCancellationRequested)
                {
                    IPEndPoint remoteEP = null;
                    var packet = _udpClient.Receive(ref remoteEP);
                    var message = RUDPProtocol.ParsePacket(packet);
                    if (message == null) continue;

                    ProcessMessage(message, remoteEP);
                }
            }
            catch (SocketException) when (_cts.IsCancellationRequested) { }
            catch (Exception ex)
            {
                if (_isRunning)
                    Console.WriteLine($"ReceiveLoop error: {ex}");
            }
            finally
            {
                Console.WriteLine("Receive loop exited");
            }
        }

        private void ProcessMessage(FileMessage message, IPEndPoint remote)
        {
            var sessionId = message.Header.SessionId;
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                session = new ClientSession(remote)
                {
                    SessionId = sessionId,
                    StorageRoot = Path.Combine(_storagePath, sessionId.ToString()),
                    ClientName = message.Header.ClientName
                };
                Directory.CreateDirectory(session.StorageRoot);
                _sessions[sessionId] = session;
                _clientNameToId[session.ClientName] = sessionId;
                OnClientConnected?.Invoke(session);
            }

            // 更新最后活动时间
            session.LastActivity = DateTime.UtcNow;

            try
            {
                switch (message.Header.Type)
                {
                    case MessageType.ClientRegister:
                        HandleClientRegister(session, message);
                        break;
                    case MessageType.Heartbeat:
                        // 心跳已通过LastActivity更新处理
                        break;
                    case MessageType.SessionStart:
                        HandleSessionStart(session, message);
                        break;
                    case MessageType.DirectoryStart:
                        HandleDirectoryStart(session, message);
                        break;
                    case MessageType.DirectoryChunk:
                        HandleDirectoryChunk(session, message);
                        break;
                    case MessageType.DirectoryEnd:
                        HandleDirectoryEnd(session, message);
                        break;
                    case MessageType.FileStart:
                        HandleFileStart(session, message);
                        break;
                    case MessageType.FileChunk:
                        HandleFileChunk(session, message);
                        break;
                    case MessageType.FileAck:
                        HandleFileAck(session, message);
                        break;
                    case MessageType.DirectoryComplete:
                        HandleDirectoryComplete(session, message);
                        break;
                    case MessageType.FileEnd:
                        HandleFileEnd(session, message);
                        break;
                    default:
                        SendError(session, "Unsupported message type");
                        break;
                }
            }
            catch (Exception ex)
            {
                SendError(session, $"Error processing {message.Header.Type}: {ex.Message}");
            }
        }

        private void HandleClientRegister(ClientSession session, FileMessage message)
        {
            session.ClientName = message.Header.ClientName;
            _clientNameToId[session.ClientName] = session.SessionId;
            Console.WriteLine($"Client registered: {session.ClientName} from {session.RemoteEndpoint}");
            SendAck(session, message.Header.Sequence);
        }

        private void HandleSessionStart(ClientSession session, FileMessage message)
        {
            session.SessionId = message.Header.SessionId;
            SendAck(session, message.Header.Sequence);
        }

        private void HandleDirectoryStart(ClientSession session, FileMessage message)
        {
            session.IsDirectoryReady = false;
            session.PendingFiles.Clear();
            session.DirectoryPaths.Clear();
            session.DirectoryChunks.Clear();
            SendAck(session, message.Header.Sequence);
        }

        private void HandleDirectoryChunk(ClientSession session, FileMessage message)
        {
            session.DirectoryChunks.Add(message.Data);
            SendAck(session, message.Header.Sequence);
        }

        private void HandleDirectoryEnd(ClientSession session, FileMessage message)
        {
            int totalLength = session.DirectoryChunks.Sum(chunk => chunk.Length);
            byte[] fullData = new byte[totalLength];
            int offset = 0;
            foreach (var chunk in session.DirectoryChunks)
            {
                Buffer.BlockCopy(chunk, 0, fullData, offset, chunk.Length);
                offset += chunk.Length;
            }
            session.DirectoryChunks.Clear();

            var json = Encoding.UTF8.GetString(fullData);
            var items = JsonSerializer.Deserialize<List<object>>(json);

            foreach (var item in items)
            {
                var element = JsonDocument.Parse(item.ToString()).RootElement;
                var type = element.GetProperty("Type").GetString();

                if (type == "DIRECTORY")
                {
                    var dirID = element.GetProperty("DirID").GetGuid();
                    var name = element.GetProperty("Name").GetString();

                    var dirPath = Path.Combine(session.StorageRoot, name);
                    Directory.CreateDirectory(dirPath);
                    session.DirectoryPaths[dirID] = dirPath;
                    session.PendingFiles[dirID] = new List<FileItem>();
                }
                else if (type == "FILE")
                {
                    var dirID = element.GetProperty("DirID").GetGuid();
                    var name = element.GetProperty("Name").GetString();
                    var size = element.GetProperty("Size").GetInt64();

                    if (session.PendingFiles.TryGetValue(dirID, out var fileList))
                    {
                        fileList.Add(new FileItem
                        {
                            Name = name,
                            Size = size
                        });
                    }
                }
            }

            session.IsDirectoryReady = true;
            SendAck(session, message.Header.Sequence);

            var completeMsg = new FileMessage
            {
                Header = new FileMessageHeader
                {
                    Type = MessageType.DirectoryComplete,
                    SessionId = session.SessionId
                }
            };
            SendMessage(session, completeMsg);
        }

        private void HandleFileStart(ClientSession session, FileMessage message)
        {
            if (!session.IsDirectoryReady && message.Header.DirID != Guid.Empty)
            {
                SendError(session, "Directory structure not ready");
                return;
            }

            if (message.Header.DirID != Guid.Empty &&
                !session.DirectoryPaths.TryGetValue(message.Header.DirID, out var dirPath))
            {
                SendError(session, $"Directory ID not found: {message.Header.DirID}");
                return;
            }

            string basePath = message.Header.DirID != Guid.Empty
                ? Path.Combine(session.DirectoryPaths[message.Header.DirID], message.Header.FileName)
                : Path.Combine(session.StorageRoot, message.Header.FileName);

            string filePath = GetUniqueFilePath(basePath);
            string fileKey = $"{message.Header.DirID}_{message.Header.FileName}";

            var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            if (!session.ActiveFiles.TryAdd(fileKey, fileStream))
            {
                fileStream.Dispose();
                SendError(session, "File already in transfer");
                return;
            }

            // 开始传输记录
            session.CurrentTransfer = new FileTransfer
            {
                FileName = message.Header.FileName,
                TotalSize = message.Header.FileSize,
                Direction = TransferDirection.Upload
            };

            OnUploadStarted?.Invoke(session, message.Header.FileName);

            SendAck(session, message.Header.Sequence);
        }

        private void HandleFileChunk(ClientSession session, FileMessage message)
        {
            string fileKey = $"{message.Header.DirID}_{message.Header.FileName}";

            if (!session.ActiveFiles.TryGetValue(fileKey, out var fileStream))
            {
                SendError(session, "No active file for chunk");
                return;
            }

            fileStream.Write(message.Data, 0, message.Header.DataLength);

            // 更新传输进度
            if (session.CurrentTransfer != null)
            {
                session.CurrentTransfer.Transferred += message.Header.DataLength;
            }

            if (message.Header.IsLastChunk)
            {
                fileStream.Dispose();
                session.ActiveFiles.TryRemove(fileKey, out _);

                if (session.PendingFiles.TryGetValue(message.Header.DirID, out var files))
                {
                    var file = files.FirstOrDefault(f => f.Name == message.Header.FileName);
                    if (file != null) files.Remove(file);
                }

                // 传输完成
                if (session.CurrentTransfer != null)
                {
                    OnUploadCompleted?.Invoke(session, message.Header.FileName, message.Header.FileSize);
                    session.CurrentTransfer = null;
                }
            }

            SendAck(session, message.Header.Sequence);
        }

        private void HandleFileEnd(ClientSession session, FileMessage message)
        {
            string fileKey = $"{message.Header.DirID}_{message.Header.FileName}";
            if (session.ActiveFiles.TryRemove(fileKey, out var fileStream))
            {
                fileStream.Dispose();
            }

            if (session.CurrentTransfer != null)
            {
                OnUploadCompleted?.Invoke(session, message.Header.FileName, message.Header.FileSize);
                session.CurrentTransfer = null;
            }

            SendAck(session, message.Header.Sequence);
        }

        private void HandleFileAck(ClientSession session, FileMessage message)
        {
            session.PendingAcks.TryRemove(message.Header.AckSequence, out _);
        }

        private void HandleDirectoryComplete(ClientSession session, FileMessage message)
        {
            // 不需要额外处理
        }

        private string GetUniqueFilePath(string basePath)
        {
            string directory = Path.GetDirectoryName(basePath);
            string fileName = Path.GetFileNameWithoutExtension(basePath);
            string extension = Path.GetExtension(basePath);
            string newPath = basePath;
            int count = 1;

            while (File.Exists(newPath))
            {
                newPath = Path.Combine(directory, $"{fileName}_{count}{extension}");
                count++;
            }
            return newPath;
        }

        private void SendAck(ClientSession session, uint sequence)
        {
            var ack = new FileMessage
            {
                Header = new FileMessageHeader
                {
                    Type = MessageType.FileAck,
                    SessionId = session.SessionId,
                    AckSequence = sequence
                }
            };
            SendMessage(session, ack);
        }

        private void SendError(ClientSession session, string error)
        {
            var errorMsg = new FileMessage
            {
                Header = new FileMessageHeader
                {
                    Type = MessageType.Error,
                    SessionId = session.SessionId,
                    FileName = error
                }
            };
            SendMessage(session, errorMsg);
        }

        private void SendMessage(ClientSession session, FileMessage message)
        {
            message.Header.Sequence = session.GetNextSequence();
            var packet = RUDPProtocol.CreatePacket(message);

            for (int i = 0; i < 3; i++) // 最多重试3次
            {
                try
                {
                    _udpClient.Send(packet, packet.Length, session.RemoteEndpoint);
                    session.PendingAcks[message.Header.Sequence] = packet;
                    return;
                }
                catch (SocketException ex)
                {
                    if (i == 2) throw; // 最后一次重试后仍失败则抛出异常
                    Thread.Sleep(50); // 短暂等待后重试
                }
            }
        }

        private void RetryPendingPackets(object state)
        {
            if (!_isRunning) return;

            foreach (var session in _sessions.Values)
            {
                foreach (var kvp in session.PendingAcks)
                {
                    try
                    {
                        _udpClient.Send(kvp.Value, kvp.Value.Length, session.RemoteEndpoint);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Retry error: {ex.Message}");
                    }
                }
            }
        }

        private void MonitorClients(object state)
        {
            if (!_isRunning) return;

            var now = DateTime.UtcNow;
            var inactiveSessions = _sessions.Values
                .Where(s => (now - s.LastActivity) > TimeSpan.FromMinutes(5))
                .ToList();

            foreach (var session in inactiveSessions)
            {
                Console.WriteLine($"Client timeout: {session.ClientName}");
                _sessions.TryRemove(session.SessionId, out _);
                _clientNameToId.TryRemove(session.ClientName, out _);
                OnClientDisconnected?.Invoke(session);
                session.Close();
            }
        }

        public ClientSession GetClientByName(string name)
        {
            return _clientNameToId.TryGetValue(name, out var id)
                ? _sessions.TryGetValue(id, out var session) ? session : null
                : null;
        }

        public async Task SendFileToClient(ClientSession session, string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found", filePath);

            string fileName = Path.GetFileName(filePath);
            long fileSize = new FileInfo(filePath).Length;
            int maxChunkSize = RUDPProtocol.CalculateMaxPayload(new FileMessageHeader
            {
                FileName = fileName,
                ClientName = session.ClientName
            });

            // 开始传输记录
            session.CurrentTransfer = new FileTransfer
            {
                FileName = fileName,
                TotalSize = fileSize,
                Direction = TransferDirection.Download
            };

            OnDownloadStarted?.Invoke(session, fileName);

            try
            {
                // 1. 发送文件开始消息
                var startMsg = new FileMessage
                {
                    Header = new FileMessageHeader
                    {
                        Type = MessageType.FileStart,
                        SessionId = session.SessionId,
                        FileName = fileName,
                        FileSize = fileSize
                    }
                };
                SendMessage(session, startMsg);

                // 2. 分块发送文件内容
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    var buffer = new byte[maxChunkSize];
                    int bytesRead;
                    long offset = 0;
                    uint sequence = 0;

                    while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        var isLast = offset + bytesRead >= fileSize;
                        var chunkData = new byte[bytesRead];
                        Array.Copy(buffer, 0, chunkData, 0, bytesRead);

                        var chunkMsg = new FileMessage
                        {
                            Header = new FileMessageHeader
                            {
                                Type = MessageType.FileChunk,
                                SessionId = session.SessionId,
                                Sequence = sequence++,
                                FileName = fileName,
                                Offset = offset,
                                DataLength = bytesRead,
                                IsLastChunk = isLast
                            },
                            Data = chunkData
                        };

                        SendMessage(session, chunkMsg);
                        offset += bytesRead;

                        // 更新传输进度
                        if (session.CurrentTransfer != null)
                        {
                            session.CurrentTransfer.Transferred = offset;
                        }
                    }
                }

                // 3. 发送文件结束消息
                var endMsg = new FileMessage
                {
                    Header = new FileMessageHeader
                    {
                        Type = MessageType.FileEnd,
                        SessionId = session.SessionId,
                        FileName = fileName
                    }
                };
                SendMessage(session, endMsg);

                // 传输完成
                OnDownloadCompleted?.Invoke(session, fileName, fileSize);
            }
            catch (Exception ex)
            {
                OnTransferFailed?.Invoke(session, fileName, ex.Message);
                throw;
            }
            finally
            {
                session.CurrentTransfer = null;
            }
        }

        public void BroadcastFile(string filePath)
        {
            if (!File.Exists(filePath))
                return;

            byte[] fileData = File.ReadAllBytes(filePath);
            string fileName = Path.GetFileName(filePath);
            int maxChunkSize = RUDPProtocol.CalculateMaxPayload(new FileMessageHeader
            {
                FileName = fileName
            });
            if (maxChunkSize <= 0) maxChunkSize = 1024;

            int totalChunks = (int)Math.Ceiling((double)fileData.Length / maxChunkSize);

            foreach (var session in _sessions.Values)
            {
                try
                {
                    var startMsg = new FileMessage
                    {
                        Header = new FileMessageHeader
                        {
                            Type = MessageType.BroadcastStart,
                            SessionId = session.SessionId,
                            FileName = fileName,
                            FileSize = fileData.Length
                        }
                    };
                    SendMessage(session, startMsg);

                    for (int i = 0; i < totalChunks; i++)
                    {
                        int chunkOffset = i * maxChunkSize;
                        int chunkSize = Math.Min(maxChunkSize, fileData.Length - chunkOffset);
                        bool isLast = (chunkOffset + chunkSize) >= fileData.Length;

                        var chunk = new byte[chunkSize];
                        Array.Copy(fileData, chunkOffset, chunk, 0, chunkSize);

                        var chunkMsg = new FileMessage
                        {
                            Header = new FileMessageHeader
                            {
                                Type = MessageType.BroadcastChunk,
                                SessionId = session.SessionId,
                                Sequence = (uint)i,
                                FileName = fileName,
                                Offset = chunkOffset,
                                DataLength = chunkSize,
                                IsLastChunk = isLast
                            },
                            Data = chunk
                        };
                        SendMessage(session, chunkMsg);
                    }

                    var endMsg = new FileMessage
                    {
                        Header = new FileMessageHeader
                        {
                            Type = MessageType.BroadcastEnd,
                            SessionId = session.SessionId,
                            FileName = fileName
                        }
                    };
                    SendMessage(session, endMsg);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Broadcast to {session.ClientName} failed: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            Stop();
            _retryTimer?.Dispose();
            _monitorTimer?.Dispose();
            _udpClient?.Dispose();
            _cts?.Dispose();
        }
    }
}
