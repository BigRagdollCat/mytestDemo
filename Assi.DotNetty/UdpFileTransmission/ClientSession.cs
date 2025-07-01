using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.DotNetty.UdpFileTransmission
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;

    namespace Assi.DotNetty.UdpFileTransmission
    {
        public class ClientSession
        {
            public Guid SessionId { get; set; }
            public IPEndPoint RemoteEndpoint { get; }
            public long TotalTransferred { get; set; }
            public long TotalSize { get; set; }
            public FileStream ActiveFile { get; set; }
            public ConcurrentDictionary<uint, byte[]> PendingAcks { get; } = new ConcurrentDictionary<uint, byte[]>();
            public Dictionary<Guid, string> DirectoryPaths { get; } = new Dictionary<Guid, string>(); // 目录ID到路径的映射
            public Dictionary<Guid, List<FileItem>> PendingFiles { get; } = new Dictionary<Guid, List<FileItem>>(); // 待传输文件
            public string StorageRoot { get; set; } // 存储根目录
            public bool IsDirectoryReady { get; set; } // 目录结构是否准备完成

            private uint _sequenceCounter = 1;
            public uint GetNextSequence() => _sequenceCounter++;

            public ClientSession(IPEndPoint remote)
            {
                RemoteEndpoint = remote;
            }
        }
    }
}
