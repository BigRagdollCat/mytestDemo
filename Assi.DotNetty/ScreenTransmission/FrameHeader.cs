using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.DotNetty.ScreenTransmission
{
    public class FrameHeader
    {
        public int FrameId { get; set; }
        public int FragmentId { get; set; }
        public int TotalFragments { get; set; }
        public bool IsLastFragment { get; set; }

        public byte[] ToBytes()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write(FrameId);
                writer.Write(FragmentId);
                writer.Write(TotalFragments);
                writer.Write(IsLastFragment);
                return ms.ToArray();
            }
        }

        public static FrameHeader FromBytes(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                return new FrameHeader
                {
                    FrameId = reader.ReadInt32(),
                    FragmentId = reader.ReadInt32(),
                    TotalFragments = reader.ReadInt32(),
                    IsLastFragment = reader.ReadBoolean()
                };
            }
        }
    }
}
