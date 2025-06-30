using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using Assi.DotNetty.FileTransmission;


namespace Assi.DotNetty.UdpFileTransmission
{
    public static class RUDPProtocol
    {
        public const int HeaderSize = 48;
        public const int MaxPacketSize = 1400;
        public const int MaxPayloadSize = MaxPacketSize - HeaderSize;

        public static byte[] CreatePacket(FileMessage message)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            writer.Write((int)message.Header.Type);
            writer.Write(message.Header.SessionId.ToByteArray());
            writer.Write(message.Header.Sequence);
            writer.Write(message.Header.AckSequence);
            writer.Write(message.Header.FileSize);
            writer.Write(message.Header.Offset);
            writer.Write(message.Header.DataLength);
            writer.Write(message.Header.IsLastChunk);

            writer.Write(Encoding.UTF8.GetBytes(message.Header.FileName.PadRight(128, '\0')));
            writer.Write(Encoding.UTF8.GetBytes(message.Header.RelativePath.PadRight(256, '\0')));

            if (message.Data != null && message.Header.DataLength > 0)
            {
                writer.Write(message.Data, 0, message.Header.DataLength);
            }

            var data = ms.ToArray();
            var signature = ComputeHmac(data);
            writer.Write(signature);

            return ms.ToArray();
        }

        public static FileMessage ParsePacket(byte[] packet)
        {
            if (packet.Length < HeaderSize) return null;

            using var ms = new MemoryStream(packet);
            using var reader = new BinaryReader(ms);

            var dataLength = packet.Length - 32;
            var signature = reader.ReadBytesAt(dataLength, 32);
            var computedSignature = ComputeHmac(packet, 0, dataLength);

            if (!SignaturesMatch(signature, computedSignature)) return null;

            ms.Seek(0, SeekOrigin.Begin);

            var header = new FileMessageHeader
            {
                Type = (MessageType)reader.ReadInt32(),
                SessionId = new Guid(reader.ReadBytes(16)),
                Sequence = reader.ReadUInt32(),
                AckSequence = reader.ReadUInt32(),
                FileSize = reader.ReadInt64(),
                Offset = reader.ReadInt64(),
                DataLength = reader.ReadInt32(),
                IsLastChunk = reader.ReadBoolean()
            };

            header.FileName = Encoding.UTF8.GetString(reader.ReadBytes(128)).TrimEnd('\0');
            header.RelativePath = Encoding.UTF8.GetString(reader.ReadBytes(256)).TrimEnd('\0');

            byte[] data = null;
            if (header.DataLength > 0)
            {
                data = reader.ReadBytes(header.DataLength);
            }

            return new FileMessage
            {
                Header = header,
                Data = data
            };
        }

        private static byte[] ComputeHmac(byte[] data, int offset = 0, int count = -1)
        {
            if (count == -1) count = data.Length;
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("SecureKey123!"));
            return hmac.ComputeHash(data, offset, count);
        }

        private static bool SignaturesMatch(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }

    public static class BinaryReaderExtensions
    {
        public static byte[] ReadBytesAt(this BinaryReader reader, long position, int count)
        {
            var current = reader.BaseStream.Position;
            reader.BaseStream.Position = position;
            var data = reader.ReadBytes(count);
            reader.BaseStream.Position = current;
            return data;
        }
    }
}
