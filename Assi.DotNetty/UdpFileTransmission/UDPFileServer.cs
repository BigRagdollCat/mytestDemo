using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Threading;

namespace Assi.DotNetty.UdpFileTransmission
{

    public class UDPFileServer : IDisposable
    {
        private readonly UdpClient _udpClient;
        private readonly CancellationTokenSource _cts;
        private readonly ConcurrentDictionary<Guid, ClientSession> _sessions = new();
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
            catch { }
        }

        private void ProcessMessage(FileMessage message, IPEndPoint remote)
        {
            var sessionId = message.Header.SessionId;
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                session = new ClientSession(remote);
                _sessions[sessionId] = session;
            }

            switch (message.Header.Type)
            {
                case MessageType.SessionStart: HandleSessionStart(session, message); break;
                case MessageType.DirectoryStart: HandleDirectoryStart(session, message); break;
                case MessageType.FileStart: HandleFileStart(session, message); break;
                case MessageType.FileChunk: HandleFileChunk(session, message); break;
                case MessageType.FileAck: HandleFileAck(session, message); break;
                case MessageType.ProgressRequest: HandleProgressRequest(session, message); break;
                default: SendError(session, "Unsupported message type"); break;
            }
        }

        private void HandleSessionStart(ClientSession session, FileMessage message)
        {
            session.SessionId = message.Header.SessionId;
            SendAck(session, message.Header.Sequence);
        }

        private void HandleDirectoryStart(ClientSession session, FileMessage message)
        {
            var dirPath = Path.Combine(_storagePath, message.Header.SessionId.ToString(), message.Header.RelativePath);
            Directory.CreateDirectory(dirPath);
            SendAck(session, message.Header.Sequence);
        }

        private void HandleFileStart(ClientSession session, FileMessage message)
        {
            var filePath = Path.Combine(_storagePath, message.Header.SessionId.ToString(), message.Header.RelativePath, message.Header.FileName);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            session.ActiveFile = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write);
            session.ActiveFile.Seek(0, SeekOrigin.End);
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
            }

            SendAck(session, message.Header.Sequence);
        }

        private void HandleFileAck(ClientSession session, FileMessage message)
        {
            session.PendingAcks.TryRemove(message.Header.AckSequence, out _);
        }

        private void HandleProgressRequest(ClientSession session, FileMessage message)
        {
            var progress = new FileMessage
            {
                Header = new FileMessageHeader
                {
                    Type = MessageType.ProgressResponse,
                    SessionId = session.SessionId,
                    Offset = session.TotalTransferred,
                    FileSize = session.TotalSize
                }
            };
            SendMessage(session, progress);
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
