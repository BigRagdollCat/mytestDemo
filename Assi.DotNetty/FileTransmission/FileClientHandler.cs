﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using global::DotNetty.Transport.Channels;
using System.IO;

namespace Assi.DotNetty.FileTransmission
{
    public class FileClientHandler : SimpleChannelInboundHandler<FileChunkMessage>
    {
        private IChannel _channel; // 保存连接通道
        private readonly ClientTransferState _transfer;
        private readonly string _localFilePath;
        // ====== 新增事件 ======
        public Action<FileMessageHeader> OnAckReceived;

        public FileClientHandler(string fileName, long fileSize, string savePath)
        {
            _transfer = new ClientTransferState(fileName, fileSize);
            _localFilePath = Path.Combine(savePath, fileName);
        }

        protected override void ChannelRead0(IChannelHandlerContext context, FileChunkMessage message)
        {
            var header = message.Header;

            switch (header.Type)
            {
                case MessageType.FileChunk:
                    ReceiveFileChunk(context, header, message.Data);
                    break;
                case MessageType.Complete:
                    HandleTransferComplete(context);
                    break;
                case MessageType.Cancel:
                    HandleTransferCancelled();
                    break;
                case MessageType.Ack:
                    // 触发事件
                    OnAckReceived?.Invoke(header);
                    break;
                default:
                    Console.WriteLine($"未知消息类型: {header.Type}");
                    break;
            }
        }

        private void ReceiveFileChunk(IChannelHandlerContext context, FileMessageHeader header, byte[] data)
        {
            // 初始化文件流（首次接收）
            if (_transfer.FileStream == null)
            {
                var directory = Path.GetDirectoryName(_localFilePath);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

                _transfer.FileStream = new FileStream(_localFilePath, FileMode.OpenOrCreate, FileAccess.Write);
                _transfer.FileStream.Seek(header.Offset, SeekOrigin.Begin);
            }

            // 写入文件
            _transfer.FileStream.Write(data, 0, data.Length);
            _transfer.UpdateOffset(header.Offset + header.DataLength, header.DataLength);

            if (context.Channel.Active)
            {
                // 发送确认
                context.WriteAndFlushAsync(new FileChunkMessage
                {
                    Header = new FileMessageHeader
                    {
                        Type = MessageType.Ack,
                        FileName = header.FileName,
                        Offset = header.Offset + header.DataLength,
                        DataLength = 0,
                    },
                    Data = new byte[0]
                });
            }
            else
            {
                Console.WriteLine("通道已关闭，无法发送 Ack");
            }

            Console.WriteLine($"已接收: {header.DataLength} 字节，进度: {_transfer.GetProgressPercentage():F2}%");
        }

        private void HandleTransferComplete(IChannelHandlerContext context)
        {
            _transfer.Status = TransferStatus.Completed;
            _transfer.Dispose();
            Console.WriteLine("文件接收完成！");
            
            // 主动关闭客户端通道
            context.Channel.CloseAsync();
        }

        private void HandleTransferCancelled()
        {
            _transfer.Status = TransferStatus.Interrupted;
            Console.WriteLine("传输被取消或文件不存在");
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Console.WriteLine($"异常: {exception.Message}");
            _transfer.Dispose();
            context.CloseAsync();
        }


    }
}
