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

            // 正确计算总长度（28 字节固定头部 + 文件名字节数 + 数据长度）
            int totalLength = 28 + fileNameBytes.Length + header.DataLength;
            output.EnsureWritable(totalLength); // 确保缓冲区有足够的空间

            // 写入固定头部
            output.WriteInt((int)header.Type);           // 4 字节
            output.WriteInt(fileNameBytes.Length);        // 4 字节（文件名长度）
            output.WriteBytes(fileNameBytes);             // 文件名字节数
            output.WriteLong(header.FileSize);           // 8 字节
            output.WriteLong(header.Offset);             // 8 字节
            output.WriteInt(header.DataLength);          // 4 字节

            // 写入数据部分
            if (header.DataLength > 0 && message.Data != null && message.Data.Length >= header.DataLength)
            {
                output.WriteBytes(message.Data, 0, header.DataLength);
            }
            else if (header.DataLength > 0)
            {
                throw new InvalidDataException("数据长度不匹配");
            }
        }
    }

    public class FileChunkMessageDecoder : ByteToMessageDecoder
    {
        private const int FixedHeaderSize = 28; // Type(4) + FileNameLen(4) + FileSize(8) + Offset(8) + DataLen(4)
        private const int MaxFileNameLength = 1024; // 文件名最大长度限制
        private const int MaxChunkSize = 1024 * 1024; // 最大分片大小 (1MB)

        protected override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            // 步骤1: 确保有足够数据读取固定头部（Type + FileNameLength）
            if (input.ReadableBytes < 8) // 至少需要读取 Type(4) + FileNameLength(4)
                return;

            input.MarkReaderIndex(); // 标记读取位置

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

            // 步骤3: 确保有足够数据读取剩余头部（FileSize, Offset, DataLength）
            if (input.ReadableBytes < 8 + 8 + 4)
            {
                input.ResetReaderIndex();
                return;
            }

            long fileSize = input.ReadLong();
            long offset = input.ReadLong();
            int dataLength = input.ReadInt();

            // 验证数据长度
            if (dataLength < 0 || dataLength > MaxChunkSize)
            {
                input.ResetReaderIndex();
                throw new InvalidDataException($"Invalid data length: {dataLength}");
            }

            // 步骤4: 确保有足够数据读取分片内容
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
