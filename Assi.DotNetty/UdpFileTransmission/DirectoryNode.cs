namespace Assi.DotNetty.UdpFileTransmission
{
    // 目录节点
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
}
