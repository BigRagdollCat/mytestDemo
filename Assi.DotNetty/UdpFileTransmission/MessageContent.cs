using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.DotNetty.UdpFileTransmission
{
    public enum MessageType
    {
        SessionStart,
        SessionEnd,
        DirectoryStart,
        FileStart,
        FileChunk,
        FileAck,
        FileComplete,
        DirectoryComplete,
        Error,
        ProgressRequest,
        ProgressResponse
    }

    public class FileMessageHeader
    {
        public MessageType Type { get; set; }
        public Guid SessionId { get; set; }
        public uint Sequence { get; set; }
        public uint AckSequence { get; set; }
        public string FileName { get; set; }
        public string RelativePath { get; set; }
        public long FileSize { get; set; }
        public long Offset { get; set; }
        public int DataLength { get; set; }
        public bool IsLastChunk { get; set; }
    }

    public class FileMessage
    {
        public FileMessageHeader Header { get; set; } = new FileMessageHeader();
        public byte[] Data { get; set; }
    }
}
