using System.Collections.Concurrent;
using System.Net;

namespace Assi.DotNetty.UdpFileTransmission
{
    public class ClientSession
    {
        // 基础信息
        public Guid SessionId { get; set; }
        public IPEndPoint RemoteEndpoint { get; }
        public string StorageRoot { get; set; } // 客户端专属存储目录

        // 传输状态
        public long TotalTransferred { get; set; } // 已传输的总字节数
        public long TotalSize { get; set; }       // 要传输的总字节数
        public int FilesReceived { get; set; }    // 已接收的文件数量
        public string Status { get; set; } = "Connected"; // 状态：Connected, Transferring, Completed, TimedOut
        public DateTime LastActivity { get; set; } = DateTime.UtcNow; // 最后活动时间

        // 当前活动文件
        public FileStream ActiveFile { get; set; }

        // 待确认的数据包
        public ConcurrentDictionary<uint, byte[]> PendingAcks { get; } = new ConcurrentDictionary<uint, byte[]>();

        // 目录结构管理
        public Dictionary<Guid, string> DirectoryPaths { get; } = new Dictionary<Guid, string>(); // 目录ID到路径映射
        public Dictionary<Guid, List<FileItem>> PendingFiles { get; } = new Dictionary<Guid, List<FileItem>>(); // 待接收文件
        public bool IsDirectoryReady { get; set; } // 目录元数据是否准备完成

        // 序列号计数器
        private uint _sequenceCounter = 1;
        public uint GetNextSequence() => _sequenceCounter++;

        // 目录映射缓存（用于快速查找）
        private Dictionary<Guid, DirectoryNode> _directoryMap = new Dictionary<Guid, DirectoryNode>();

        public ClientSession(IPEndPoint remote)
        {
            RemoteEndpoint = remote;
        }

        // 添加目录映射
        public void AddDirectoryMapping(Guid dirId, string path)
        {
            DirectoryPaths[dirId] = path;
        }

        // 添加待接收文件
        public void AddPendingFile(Guid dirId, FileItem file)
        {
            if (!PendingFiles.ContainsKey(dirId))
            {
                PendingFiles[dirId] = new List<FileItem>();
            }
            PendingFiles[dirId].Add(file);
            TotalSize += file.Size; // 更新总大小
        }

        // 查找目录路径
        public string GetDirectoryPath(Guid dirId)
        {
            return DirectoryPaths.TryGetValue(dirId, out var path) ? path : null;
        }

        // 关闭所有资源
        public void Close()
        {
            ActiveFile?.Dispose();
            ActiveFile = null;
        }
    }
}
