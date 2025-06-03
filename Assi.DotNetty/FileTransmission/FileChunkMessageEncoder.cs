using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Transport.Channels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.DotNetty.FileTransmission
{
    public class FileChunkMessageEncoder : MessageToByteEncoder<FileChunkMessage>
    {
        protected override void Encode(IChannelHandlerContext context, FileChunkMessage message, IByteBuffer output)
        {
            var header = message.Header;
            byte[] fileNameBytes = Encoding.UTF8.GetBytes(header.FileName);

            // 计算总长度并确保缓冲区容量
            int totalLength = 24 + fileNameBytes.Length + header.DataLength;
            output.EnsureWritable(totalLength);

            output.WriteInt((int)header.Type);
            output.WriteInt(fileNameBytes.Length);
            output.WriteBytes(fileNameBytes);
            output.WriteLong(header.FileSize);
            output.WriteLong(header.Offset);
            output.WriteInt(header.DataLength);

            // 安全写入数据
            if (header.DataLength > 0 && message.Data != null )
            {
                // 使用更安全的写入方式
                output.WriteBytes(message.Data, 0, header.DataLength);
            }
        }
    }

    public class FileChunkMessageDecoder : ByteToMessageDecoder
    {
        private const int FixedHeaderSize = 24; // Type(4) + FileNameLen(4) + FileSize(8) + Offset(8) + DataLen(4)
        private const int MaxFileNameLength = 1024; // 文件名最大长度限制
        private const int MaxChunkSize = 1024 * 1024; // 最大分片大小 (1MB)

        protected override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            // 步骤1: 确保有足够数据读取固定头部
            if (input.ReadableBytes < FixedHeaderSize)
                return;

            // 标记读取位置以便回退
            input.MarkReaderIndex();

            // 读取固定头部
            var type = (MessageType)input.ReadInt();
            int fileNameLength = input.ReadInt();

            // 验证文件名长度
            if (fileNameLength < 0 || fileNameLength > MaxFileNameLength)
            {
                input.ResetReaderIndex();
                throw new InvalidDataException($"Invalid file name length: {fileNameLength}");
            }

            // 步骤2: 确保有足够数据读取文件名
            if (input.ReadableBytes < fileNameLength)
            {
                input.ResetReaderIndex();
                return;
            }

            // 读取文件名
            string fileName = input.ReadString(fileNameLength, Encoding.UTF8);

            // 读取剩余头部
            long fileSize = input.ReadLong();
            long offset = input.ReadLong();
            int dataLength = input.ReadInt();

            // 验证数据长度
            if (dataLength < 0 || dataLength > MaxChunkSize)
            {
                input.ResetReaderIndex();
                throw new InvalidDataException($"Invalid data length: {dataLength}");
            }

            // 步骤3: 确保有足够数据读取分片内容
            if (input.ReadableBytes < dataLength)
            {
                input.ResetReaderIndex();
                return;
            }

            // 读取分片数据
            byte[] data = new byte[dataLength];
            input.ReadBytes(data, 0, dataLength);

            output.Add(new FileChunkMessage
            {
                Header = new FileMessageHeader
                {
                    Type = type,
                    FileName = fileName,
                    FileSize = fileSize,
                    Offset = offset,
                    DataLength = dataLength
                },
                Data = data
            });
        }
    }
}
