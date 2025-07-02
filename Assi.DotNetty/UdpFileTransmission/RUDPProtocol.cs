using System.Text;
using System.Security.Cryptography;

namespace Assi.DotNetty.UdpFileTransmission
{
    public static class RUDPProtocol
    {
        public const int MaxPacketSize = 1400;

        // 添加常量：保守的默认负载大小（用于目录序列化）
        public const int ConservativePayloadSize = 1200;  // 1400 - 200(头部预估)

        public static int CalculateMaxPayload(FileMessageHeader header)
        {
            int headerSize = 4 + 16 + 4 + 4 + 16 +
                Encoding.UTF8.GetByteCount(header.FileName ?? "") +
                8 + 8 + 4 + 1;

            return MaxPacketSize - headerSize - 32;
        }

        public static byte[] CreatePacket(FileMessage message)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            writer.Write((int)message.Header.Type);
            writer.Write(message.Header.SessionId.ToByteArray());
            writer.Write(message.Header.Sequence);
            writer.Write(message.Header.AckSequence);
            writer.Write(message.Header.DirID.ToByteArray());
            writer.Write(message.Header.FileName ?? string.Empty);
            writer.Write(message.Header.FileSize);
            writer.Write(message.Header.Offset);
            writer.Write(message.Header.DataLength);
            writer.Write(message.Header.IsLastChunk);

            if (message.Data != null && message.Data.Length > 0)
            {
                writer.Write(message.Data);
            }

            var data = ms.ToArray();
            var signature = ComputeHmac(data);
            writer.Write(signature);

            return ms.ToArray();
        }

        public static FileMessage ParsePacket(byte[] packet)
        {
            if (packet.Length < 32) return null;

            using var ms = new MemoryStream(packet);
            using var reader = new BinaryReader(ms);

            var header = new FileMessageHeader
            {
                Type = (MessageType)reader.ReadInt32(),
                SessionId = new Guid(reader.ReadBytes(16)),
                Sequence = reader.ReadUInt32(),
                AckSequence = reader.ReadUInt32(),
                DirID = new Guid(reader.ReadBytes(16)),
                FileName = reader.ReadString(),
                FileSize = reader.ReadInt64(),
                Offset = reader.ReadInt64(),
                DataLength = reader.ReadInt32(),
                IsLastChunk = reader.ReadBoolean()
            };

            byte[] data = null;
            if (header.DataLength > 0)
            {
                data = reader.ReadBytes(header.DataLength);
            }

            var signature = reader.ReadBytes(32);
            var computedSignature = ComputeHmac(packet, 0, (int)ms.Position - 32);
            if (!SignaturesMatch(signature, computedSignature))
                return null;

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
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }
}
