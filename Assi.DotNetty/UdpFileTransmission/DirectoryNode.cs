namespace Assi.DotNetty.UdpFileTransmission
{
    public class DirectoryNode
    {
        public Guid DirID { get; } = Guid.NewGuid();
        public string Name { get; set; }
        public List<DirectoryNode> Children { get; } = new List<DirectoryNode>();
        public List<FileItem> Files { get; } = new List<FileItem>();
    }

    public class FileItem
    {
        public string Name { get; set; }
        public long Size { get; set; }
        public string LocalPath { get; set; }
        public Guid DirID { get; set; }
    }

    public class FileTransfer
    {
        public string FileName { get; set; }
        public long TotalSize { get; set; }
        public long Transferred { get; set; }
        public TransferDirection Direction { get; set; }
        public DateTime StartTime { get; } = DateTime.UtcNow;
    }
}
