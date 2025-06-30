using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;

namespace Assi.DotNetty.UdpFileTransmission
{
    public class UDPFileClient : IDisposable
    {
        private readonly UdpClient _udpClient;
        private readonly IPEndPoint _serverEP;
        private readonly ConcurrentDictionary<uint, FileMessage> _pendingPackets = new();
        private readonly Timer _retryTimer;
        private uint _sequenceCounter = 1;
        private Guid _currentSessionId;
        private ClientTransferState _currentTransfer;

        public UDPFileClient(string serverIp, int serverPort)
        {
            _udpClient = new UdpClient();
            _serverEP = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);
            _retryTimer = new Timer(RetryPendingPackets, null, 100, 100);
            Task.Run(ReceiveLoop);
        }

        public async Task UploadAsync(string path)
        {
            _currentTransfer = new ClientTransferState(path);
            _currentSessionId = _currentTransfer.SessionId;

            await SendSessionStart();
            if (_currentTransfer.IsDirectory) await UploadDirectory();
            else await UploadFile(_currentTransfer);
            await SendSessionEnd();
        }

        private async Task UploadDirectory()
        {
            var structure = DirectorySerializer.SerializeDirectoryStructure(_currentTransfer.BasePath);
            // 发送目录结构
            await SendWithRetry(new FileMessage
            {
                Header = new FileMessageHeader
                {
                    Type = MessageType.DirectoryStart,
                    SessionId = _currentSessionId,
                    RelativePath = "",
                    DataLength = structure.Length
                },
                Data = structure
            });

            foreach (var file in Directory.GetFiles(_currentTransfer.BasePath, "*", SearchOption.AllDirectories))
            {
                var item = new ClientTransferState(file);
                await UploadFile(item);
            }
        }

        private async Task UploadFile(ClientTransferState file)
        {
            var fileStart = new FileMessage
            {
                Header = new FileMessageHeader
                {
                    Type = MessageType.FileStart,
                    SessionId = _currentSessionId,
                    FileName = file.FileName,
                    RelativePath = file.RelativePath,
                    FileSize = file.FileSize
                }
            };
            await SendWithRetry(fileStart);

            using var fs = new FileStream(file.BasePath, FileMode.Open, FileAccess.Read);
            var buffer = new byte[RUDPProtocol.MaxPayloadSize];
            int bytesRead;
            long offset = 0;

            while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                var isLast = offset + bytesRead >= file.FileSize;
                var chunk = new FileMessage
                {
                    Header = new FileMessageHeader
                    {
                        Type = MessageType.FileChunk,
                        SessionId = _currentSessionId,
                        Sequence = GetNextSequence(),
                        FileName = file.FileName,
                        RelativePath = file.RelativePath,
                        Offset = offset,
                        DataLength = bytesRead,
                        IsLastChunk = isLast
                    },
                    Data = new byte[bytesRead]
                };
                Buffer.BlockCopy(buffer, 0, chunk.Data, 0, bytesRead);
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
                }
            }
            catch { }
        }

        private uint GetNextSequence() => _sequenceCounter++;

        public void Dispose()
        {
            _retryTimer?.Dispose();
            _udpClient?.Close();
        }
    }
}
