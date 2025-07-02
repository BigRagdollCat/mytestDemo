using System.Collections.Concurrent;
using System.Net;



namespace Assi.DotNetty.UdpFileTransmission
{
    public class ClientSession
    {
        public Guid SessionId { get; set; }
        public IPEndPoint RemoteEndpoint { get; }
        public string StorageRoot { get; set; }
        public string ClientName { get; set; }

        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
        public FileTransfer CurrentTransfer { get; set; }

        public ConcurrentDictionary<string, FileStream> ActiveFiles { get; } =
            new ConcurrentDictionary<string, FileStream>();

        public ConcurrentDictionary<uint, byte[]> PendingAcks { get; } =
            new ConcurrentDictionary<uint, byte[]>();

        public Dictionary<Guid, string> DirectoryPaths { get; } =
            new Dictionary<Guid, string>();

        public Dictionary<Guid, List<FileItem>> PendingFiles { get; } =
            new Dictionary<Guid, List<FileItem>>();

        public bool IsDirectoryReady { get; set; }
        public List<byte[]> DirectoryChunks { get; } = new List<byte[]>();

        private uint _sequenceCounter = 1;
        public uint GetNextSequence() => _sequenceCounter++;

        public ClientSession(IPEndPoint remote)
        {
            RemoteEndpoint = remote;
        }

        public void Close()
        {
            foreach (var file in ActiveFiles.Values)
            {
                file.Dispose();
            }
            ActiveFiles.Clear();
        }

        public bool IsAlive(TimeSpan timeout)
        {
            return (DateTime.UtcNow - LastActivity) < timeout;
        }
    }
}
