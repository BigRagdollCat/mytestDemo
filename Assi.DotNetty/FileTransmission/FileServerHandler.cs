using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using global::DotNetty.Transport.Channels;
using System.IO;
using System.Net;
using System.Collections.Concurrent;

namespace Assi.DotNetty.FileTransmission
{

    public class FileServerHandler : SimpleChannelInboundHandler<FileChunkMessage>
    {
        private readonly string _fileDirectory;
        private readonly ConcurrentDictionary<IChannel, ClientTransferState> _transfers = new();

        public FileServerHandler(string fileDirectory)
        {
            _fileDirectory = fileDirectory;
        }

        protected override void ChannelRead0(IChannelHandlerContext context, FileChunkMessage message)
        {
            var channel = context.Channel;
            var header = message.Header;

            switch (header.Type)
            {
                case MessageType.FileRequest:
                    HandleFileRequest(channel, header);
                    break;
                case MessageType.Ack:
                    HandleAck(channel, header);
                    break;
                case MessageType.Cancel:
                    HandleCancel(channel, header);
                    break;
                default:
                    Console.WriteLine($"未知消息类型: {header.Type}");
                    break;
            }
        }

        private void HandleFileRequest(IChannel channel, FileMessageHeader header)
        {
            var fileName = Path.Combine(_fileDirectory, header.FileName);
            if (!File.Exists(fileName))
            {
                Console.WriteLine($"文件不存在: {fileName}");
                channel.WriteAndFlushAsync(new FileChunkMessage
                {
                    Header = new FileMessageHeader
                    {
                        Type = MessageType.Cancel,
                        FileName = header.FileName
                    }
                });
                return;
            }

            var fileInfo = new FileInfo(fileName);
            var transfer = new ClientTransferState(header.FileName, fileInfo.Length)
            {
                Ip = channel.RemoteAddress.ToString(),
                Port = ((IPEndPoint)channel.RemoteAddress).Port,
                FileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read),
                Status = TransferStatus.Downloading
            };

            if (header.Offset > 0)
            {
                transfer.CurrentOffset = header.Offset;
                transfer.FileStream.Seek(header.Offset, SeekOrigin.Begin);
            }

            // 直接尝试添加，无需手动检查
            if (_transfers.TryAdd(channel, transfer))
            {
                SendNextChunk(channel, transfer);
            }
            else
            {
                Console.WriteLine("客户端已存在传输任务");
            }
        }

        private void HandleAck(IChannel channel, FileMessageHeader header)
        {
            if (!_transfers.TryGetValue(channel, out var transfer)) return;
            transfer.UpdateOffset(header.Offset + header.DataLength, header.DataLength);
            if (transfer.CurrentOffset >= transfer.TotalSize)
            {
                transfer.Status = TransferStatus.Completed;
                channel.WriteAndFlushAsync(new FileChunkMessage
                {
                    Header = new FileMessageHeader
                    {
                        Type = MessageType.Complete,
                        FileName = transfer.FileName
                    }
                });
                return;
            }
            SendNextChunk(channel, transfer);
        }

        private void HandleCancel(IChannel channel, FileMessageHeader header)
        {
            if (_transfers.TryGetValue(channel, out var transfer))
            {
                transfer.Status = TransferStatus.Interrupted;
                transfer.Dispose();
                ClientTransferState cts;
                _transfers.TryRemove(channel,out cts);
            }
        }

        private void SendNextChunk(IChannel channel, ClientTransferState transfer)
        {
            const int chunkSize = 8192;
            var buffer = new byte[chunkSize];
            transfer.FileStream.Seek(transfer.CurrentOffset, SeekOrigin.Begin);
            int bytesRead = transfer.FileStream.Read(buffer, 0, chunkSize);

            if (bytesRead <= 0)
            {
                transfer.Status = TransferStatus.Completed;
                channel.WriteAndFlushAsync(new FileChunkMessage
                {
                    Header = new FileMessageHeader
                    {
                        Type = MessageType.Complete,
                        FileName = transfer.FileName
                    }
                });
                return;
            }

            var data = new byte[bytesRead];
            Array.Copy(buffer, data, bytesRead);

            var chunkMessage = new FileChunkMessage
            {
                Header = new FileMessageHeader
                {
                    Type = MessageType.FileChunk,
                    FileName = transfer.FileName,
                    FileSize = transfer.TotalSize,
                    Offset = transfer.CurrentOffset,
                    DataLength = bytesRead
                },
                Data = data
            };

            channel.WriteAndFlushAsync(chunkMessage);
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            var channel = context.Channel;
            if (_transfers.TryGetValue(channel, out var transfer))
            {
                transfer.Status = TransferStatus.Interrupted;
                transfer.Dispose();
                ClientTransferState cts;
                _transfers.TryRemove(channel, out cts);
            }
            base.ChannelInactive(context);
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Console.WriteLine($"异常: {exception.Message}");
            context.CloseAsync();
        }
    }
}
