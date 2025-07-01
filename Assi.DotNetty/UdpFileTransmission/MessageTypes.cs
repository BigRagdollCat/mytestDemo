namespace Assi.DotNetty.UdpFileTransmission
{
    public enum MessageType
    {
        SessionStart,
        SessionEnd,
        DirectoryStart,
        DirectoryChunk,
        DirectoryEnd,
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
        public Guid DirID { get; set; } // 目录传输ID
        public string FileName { get; set; } // 文件名
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
