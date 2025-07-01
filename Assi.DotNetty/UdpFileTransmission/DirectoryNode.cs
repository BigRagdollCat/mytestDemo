namespace Assi.DotNetty.UdpFileTransmission
{
    // 目录节点
    public class DirectoryNode
    {
        public Guid DirID { get; } = Guid.NewGuid(); // 传输用唯一ID
        public string Name { get; set; }
        public List<DirectoryNode> Children { get; } = new List<DirectoryNode>();
        public List<FileItem> Files { get; } = new List<FileItem>();
    }

    // 文件项
    public class FileItem
    {
        public string Name { get; set; }
        public long Size { get; set; }
        public string LocalPath { get; set; } // 仅客户端使用
    }
}
