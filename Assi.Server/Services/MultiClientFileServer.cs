using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Assi.DotNetty.UdpFileTransmission.Assi.DotNetty.UdpFileTransmission;

namespace Assi.Server.Services
{

    namespace Assi.DotNetty.UdpFileTransmission
    {
        public class MultiClientFileServer : IDisposable
        {
            private readonly UdpClient _udpClient;
            private readonly CancellationTokenSource _cts;
            private readonly ConcurrentDictionary<Guid, ClientSession> _sessions = new ConcurrentDictionary<Guid, ClientSession>();
            private readonly string _storagePath;
            private readonly Thread _receiveThread;
            private readonly int _port;
            private readonly Timer _cleanupTimer;

            // 客户端状态变更事件
            public event EventHandler<ClientStatusEventArgs> ClientStatusChanged;

            public MultiClientFileServer(int port, string storagePath)
            {
                _port = port;
                _storagePath = storagePath;
                _udpClient = new UdpClient(port);
                _cts = new CancellationTokenSource();
                Directory.CreateDirectory(storagePath);

                // 启动接收线程
                _receiveThread = new Thread(ReceiveLoop);
                _receiveThread.IsBackground = true;
                _receiveThread.Start();

                // 定时清理超时会话（每5分钟）
                _cleanupTimer = new Timer(CleanupSessions, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
            }

            private void ReceiveLoop()
            {
                try
                {
                    while (!_cts.IsCancellationRequested)
                    {
                        IPEndPoint remoteEP = null;
                        var packet = _udpClient.Receive(ref remoteEP);

                        // 使用Task.Run处理消息，避免阻塞接收循环
                        Task.Run(() => ProcessPacket(packet, remoteEP));
                    }
                }
                catch (SocketException) when (_cts.IsCancellationRequested) { }
                catch (Exception ex)
                {
                    Console.WriteLine($"ReceiveLoop error: {ex}");
                }
            }

            private async Task ProcessPacket(byte[] packet, IPEndPoint remoteEP)
            {
                try
                {
                    var message = RUDPProtocol.ParsePacket(packet);
                    if (message == null) return;

                    var sessionId = message.Header.SessionId;
                    if (!_sessions.TryGetValue(sessionId, out var session))
                    {
                        // 创建新会话
                        session = new ClientSession(remoteEP)
                        {
                            SessionId = sessionId,
                            StorageRoot = Path.Combine(_storagePath, sessionId.ToString())
                        };
                        Directory.CreateDirectory(session.StorageRoot);
                        _sessions[sessionId] = session;

                        // 触发客户端连接事件
                        ClientStatusChanged?.Invoke(this, new ClientStatusEventArgs
                        {
                            SessionId = sessionId,
                            Status = "Connected",
                            RemoteEndpoint = remoteEP
                        });
                    }

                    // 更新最后活动时间
                    session.LastActivity = DateTime.UtcNow;

                    // 处理消息
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
                            await HandleFileChunk(session, message);
                            break;
                        case MessageType.FileAck:
                            HandleFileAck(session, message);
                            break;
                        case MessageType.SessionEnd:
                            HandleSessionEnd(session, message);
                            break;
                        case MessageType.ProgressRequest:
                            HandleProgressRequest(session, message);
                            break;
                        default:
                            SendError(session, "Unsupported message type");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing packet: {ex.Message}");
                }
            }

            private void HandleSessionEnd(ClientSession session, FileMessage message)
            {
                // 标记会话完成
                session.Status = "Completed";

                // 触发客户端完成事件
                ClientStatusChanged?.Invoke(this, new ClientStatusEventArgs
                {
                    SessionId = session.SessionId,
                    Status = session.Status,
                    FilesReceived = session.FilesReceived,
                    TotalBytes = session.TotalTransferred
                });

                // 发送确认
                SendAck(session, message.Header.Sequence);

                // 清理会话（稍后由清理定时器处理）
            }

            private async Task HandleFileChunk(ClientSession session, FileMessage message)
            {
                if (session.ActiveFile == null)
                {
                    SendError(session, "No active file for chunk");
                    return;
                }

                await session.ActiveFile.WriteAsync(message.Data, 0, message.Header.DataLength);
                session.TotalTransferred += message.Header.DataLength;

                // 更新进度
                if (session.TotalSize > 0)
                {
                    float progress = (float)session.TotalTransferred / session.TotalSize * 100;
                    Console.WriteLine($"Session {session.SessionId}: {progress:0.00}% complete");
                }

                if (message.Header.IsLastChunk)
                {
                    session.ActiveFile.Dispose();
                    session.ActiveFile = null;
                    session.FilesReceived++;

                    // 标记文件完成
                    if (session.PendingFiles.TryGetValue(message.Header.DirID, out var files))
                    {
                        var file = files.Find(f => f.Name == message.Header.FileName);
                        if (file != null) files.Remove(file);
                    }
                }

                SendAck(session, message.Header.Sequence);
            }

            // 其他处理方法和之前实现相同...

            private void CleanupSessions(object state)
            {
                var now = DateTime.UtcNow;
                foreach (var sessionId in _sessions.Keys)
                {
                    if (_sessions.TryGetValue(sessionId, out var session))
                    {
                        // 清理超时会话（30分钟无活动）
                        if ((now - session.LastActivity) > TimeSpan.FromMinutes(30))
                        {
                            _sessions.TryRemove(sessionId, out _);
                            Console.WriteLine($"Cleaned up inactive session: {sessionId}");

                            // 触发客户端断开事件
                            ClientStatusChanged?.Invoke(this, new ClientStatusEventArgs
                            {
                                SessionId = sessionId,
                                Status = "TimedOut",
                                RemoteEndpoint = session.RemoteEndpoint
                            });
                        }

                        // 清理已完成会话
                        else if (session.Status == "Completed")
                        {
                            _sessions.TryRemove(sessionId, out _);
                            Console.WriteLine($"Cleaned up completed session: {sessionId}");
                        }
                    }
                }
            }

            public void Stop()
            {
                _cts.Cancel();
                _cleanupTimer.Dispose();
            }

            public void Dispose()
            {
                Stop();
                _udpClient.Close();
            }
        }

        public class ClientStatusEventArgs : EventArgs
        {
            public Guid SessionId { get; set; }
            public string Status { get; set; } // Connected, Transferring, Completed, TimedOut
            public IPEndPoint RemoteEndpoint { get; set; }
            public int FilesReceived { get; set; }
            public long TotalBytes { get; set; }
        }

        public class ClientSession
        {
            // ...原有属性...

            // 新增属性
            public DateTime LastActivity { get; set; } = DateTime.UtcNow;
            public string Status { get; set; } = "Connected";
            public int FilesReceived { get; set; }
        }
    }
}
