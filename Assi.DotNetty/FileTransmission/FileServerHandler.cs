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
        private readonly ServerStateManager _stateManager;

        public FileServerHandler(string fileDirectory, ServerStateManager stateManager)
        {
            _fileDirectory = fileDirectory;
            _stateManager = stateManager;
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
                case MessageType.UploadRequest:
                    HandleUploadRequest(channel, header);
                    break;
                case MessageType.UploadChunk:
                    HandleUploadChunk(channel, message);
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

            // 注册到全局状态管理器
            _stateManager.AddTransfer(channel, transfer);
            SendNextChunk(channel, transfer);
        }

        private void HandleAck(IChannel channel, FileMessageHeader header)
        {
            ClientTransferState transfer = _stateManager.GetTransfer(channel);
            if (transfer == default) return;
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
                // 传输完成时移除
                _stateManager.RemoveTransfer(channel);
                return;
            }
            SendNextChunk(channel, transfer);
        }

        private void HandleCancel(IChannel channel, FileMessageHeader header)
        {
            _stateManager.RemoveTransfer(channel);
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
            _stateManager.RemoveTransfer(channel);
            base.ChannelInactive(context);
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            // 异常时强制移除
            _stateManager.RemoveTransfer(context.Channel);
            base.ExceptionCaught(context, exception);
        }

        private void HandleUploadRequest(IChannel channel, FileMessageHeader header)
        {
            var filePath = Path.Combine(_fileDirectory, header.FileName);
            var transfer = new ClientTransferState(header.FileName, header.FileSize)
            {
                Ip = channel.RemoteAddress.ToString(),
                FileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write),
                Status = TransferStatus.Uploading
            };

            if (header.Offset > 0 && File.Exists(filePath))
            {
                transfer.CurrentOffset = header.Offset;
                transfer.FileStream.Seek(header.Offset, SeekOrigin.Begin);
            }

            _stateManager.AddTransfer(channel,transfer);
            Console.WriteLine($"开始接收文件: {header.FileName} ({header.FileSize} 字节)");
        }

        private void HandleUploadChunk(IChannel channel, FileChunkMessage msg)
        {
            ClientTransferState transfer = _stateManager.GetTransfer(channel);
            if (transfer == null || transfer.Status != TransferStatus.Uploading)
                return;

            if (transfer.IsOffsetValid(msg.Header.Offset))
            {
                transfer.FileStream.Write(msg.Data, 0, msg.Header.DataLength);
                transfer.UpdateOffset(msg.Header.Offset + msg.Header.DataLength, msg.Header.DataLength);

                // 发送确认
                channel.WriteAndFlushAsync(new FileChunkMessage
                {
                    Header = new FileMessageHeader
                    {
                        Type = MessageType.Ack,
                        FileName = msg.Header.FileName,
                        Offset = msg.Header.Offset + msg.Header.DataLength,
                        DataLength = msg.Header.DataLength
                    },
                    Data = new byte[0]
                });

                if (transfer.CurrentOffset >= transfer.TotalSize)
                {
                    transfer.Status = TransferStatus.Completed;
                    Console.WriteLine($"{msg.Header.FileName} 接收完成");
                }
            }
        }
    }
}
