using System.Text;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace Assi.DotNetty.UdpFileTransmission
{
    public class UDPFileServer : IDisposable
    {
        private readonly UdpClient _udpClient;
        private readonly CancellationTokenSource _cts;
        private readonly ConcurrentDictionary<Guid, ClientSession> _sessions = new ConcurrentDictionary<Guid, ClientSession>();
        private readonly string _storagePath;
        private readonly Thread _receiveThread;
        private readonly int _port;
        private readonly Timer _retryTimer;

        public UDPFileServer(int port, string storagePath)
        {
            _port = port;
            _storagePath = storagePath;
            _udpClient = new UdpClient(port);
            _cts = new CancellationTokenSource();
            Directory.CreateDirectory(storagePath);
            _receiveThread = new Thread(ReceiveLoop);
            _receiveThread.IsBackground = true;
            _receiveThread.Start();
            _retryTimer = new Timer(RetryPendingPackets, null, 100, 100);
        }

        private void ReceiveLoop()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
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
                Console.WriteLine($"ReceiveLoop error: {ex}");
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
                    StorageRoot = Path.Combine(_storagePath, sessionId.ToString())
                };
                Directory.CreateDirectory(session.StorageRoot);
                _sessions[sessionId] = session;
            }

            try
            {
                switch (message.Header.Type)
                {
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
            if (!session.IsDirectoryReady)
            {
                SendError(session, "Directory structure not ready");
                return;
            }

            if (!session.DirectoryPaths.TryGetValue(message.Header.DirID, out var dirPath))
            {
                SendError(session, $"Directory ID not found: {message.Header.DirID}");
                return;
            }

            string basePath = Path.Combine(dirPath, message.Header.FileName);
            string filePath = GetUniqueFilePath(basePath);

            var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            string fileKey = $"{message.Header.DirID}_{message.Header.FileName}";

            if (!session.ActiveFiles.TryAdd(fileKey, fileStream))
            {
                fileStream.Dispose();
                SendError(session, "File already in transfer");
                return;
            }

            SendAck(session, message.Header.Sequence);
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

        private void HandleFileChunk(ClientSession session, FileMessage message)
        {
            string fileKey = $"{message.Header.DirID}_{message.Header.FileName}";

            if (!session.ActiveFiles.TryGetValue(fileKey, out var fileStream))
            {
                SendError(session, "No active file for chunk");
                return;
            }

            fileStream.Write(message.Data, 0, message.Header.DataLength);

            if (message.Header.IsLastChunk)
            {
                fileStream.Dispose();
                session.ActiveFiles.TryRemove(fileKey, out _);

                if (session.PendingFiles.TryGetValue(message.Header.DirID, out var files))
                {
                    var file = files.FirstOrDefault(f => f.Name == message.Header.FileName);
                    if (file != null) files.Remove(file);
                }
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
            session.PendingAcks[message.Header.Sequence] = packet;
            _udpClient.Send(packet, packet.Length, session.RemoteEndpoint);
        }

        private void RetryPendingPackets(object state)
        {
            foreach (var session in _sessions.Values)
            {
                foreach (var kvp in session.PendingAcks)
                {
                    _udpClient.Send(kvp.Value, kvp.Value.Length, session.RemoteEndpoint);
                }
            }
        }

        public void BroadcastFile(string filePath)
        {
            if (!File.Exists(filePath))
                return;

            byte[] fileData = File.ReadAllBytes(filePath);
            string fileName = Path.GetFileName(filePath);
            int maxChunkSize = RUDPProtocol.CalculateMaxPayload(new FileMessageHeader { FileName = fileName });
            if (maxChunkSize <= 0) maxChunkSize = 1024;

            int totalChunks = (int)Math.Ceiling((double)fileData.Length / maxChunkSize);

            foreach (var session in _sessions.Values)
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
        }

        public void Dispose()
        {
            _cts.Cancel();
            _retryTimer?.Dispose();
            _udpClient.Close();
        }
    }
}
