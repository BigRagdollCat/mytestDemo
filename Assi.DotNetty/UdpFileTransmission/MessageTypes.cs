namespace Assi.DotNetty.UdpFileTransmission
{
    public enum MessageType
    {
        ClientRegister,   // 客户端注册
        Heartbeat,        // 心跳
        SessionStart,     // 会话开始
        SessionEnd,       // 会话结束
        DirectoryStart,   // 目录传输开始
        DirectoryChunk,   // 目录数据块
        DirectoryEnd,     // 目录传输结束
        FileStart,        // 文件传输开始
        FileChunk,        // 文件数据块
        FileAck,          // 文件块确认
        FileEnd,          // 文件传输结束
        FileComplete,     // 文件传输完成
        DirectoryComplete,// 目录传输完成
        Error,            // 错误消息
        ProgressRequest,  // 进度请求
        ProgressResponse, // 进度响应
        BroadcastStart,   // 广播开始
        BroadcastChunk,   // 广播数据块
        BroadcastEnd      // 广播结束
    }

    public class FileMessageHeader
    {
        public MessageType Type { get; set; }
        public Guid SessionId { get; set; }
        public uint Sequence { get; set; }
        public uint AckSequence { get; set; }
        public Guid DirID { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public long Offset { get; set; }
        public int DataLength { get; set; }
        public bool IsLastChunk { get; set; }
        public string ClientName { get; set; } // 客户端标识
    }

    public class FileMessage
    {
        public FileMessageHeader Header { get; set; } = new FileMessageHeader();
        public byte[] Data { get; set; }
    }

    public enum TransferStatus
    {
        Pending,
        Uploading,
        Downloading,
        Completed,
        Interrupted
    }

    public enum TransferDirection
    {
        Upload,
        Download
    }
}
