using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.DotNetty.UdpFileTransmission
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;

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
                SendAck(session, message.Header.Sequence);
            }

            private void HandleDirectoryChunk(ClientSession session, FileMessage message)
            {
                // 解析目录块数据
                var json = Encoding.UTF8.GetString(message.Data);
                var items = JsonSerializer.Deserialize<List<object>>(json);

                foreach (var item in items)
                {
                    var element = JsonDocument.Parse(item.ToString()).RootElement;
                    var type = element.GetProperty("Type").GetString();

                    if (type == "DIRECTORY")
                    {
                        var dirID = element.GetProperty("DirID").GetGuid();
                        var name = element.GetProperty("Name").GetString();

                        // 创建目录
                        var dirPath = Path.Combine(session.StorageRoot, name);
                        Directory.CreateDirectory(dirPath);
                        session.DirectoryPaths[dirID] = dirPath;

                        // 初始化待传输文件列表
                        session.PendingFiles[dirID] = new List<FileItem>();
                    }
                    else if (type == "FILE")
                    {
                        var dirID = element.GetProperty("DirID").GetGuid();
                        var name = element.GetProperty("Name").GetString();
                        var size = element.GetProperty("Size").GetInt64();

                        // 添加到待传输文件列表
                        session.PendingFiles[dirID].Add(new FileItem
                        {
                            Name = name,
                            Size = size
                        });
                    }
                }

                SendAck(session, message.Header.Sequence);
            }

            private void HandleDirectoryEnd(ClientSession session, FileMessage message)
            {
                session.IsDirectoryReady = true;
                SendAck(session, message.Header.Sequence);

                // 通知客户端目录准备完成
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

                // 创建文件
                string filePath = Path.Combine(dirPath, message.Header.FileName);
                session.ActiveFile = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                SendAck(session, message.Header.Sequence);
            }

            private void HandleFileChunk(ClientSession session, FileMessage message)
            {
                if (session.ActiveFile == null)
                {
                    SendError(session, "No active file for chunk");
                    return;
                }

                session.ActiveFile.Write(message.Data, 0, message.Header.DataLength);
                if (message.Header.IsLastChunk)
                {
                    session.ActiveFile.Dispose();
                    session.ActiveFile = null;

                    // 标记文件完成
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
                // 客户端确认目录完成，服务端不需要额外处理
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

            public void Dispose()
            {
                _cts.Cancel();
                _udpClient.Close();
            }
        }
    }
}
