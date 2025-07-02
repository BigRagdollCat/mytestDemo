using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;


namespace Assi.DotNetty.UdpFileTransmission
{
    public class ClientTransferState : IDisposable
    {
        public Guid SessionId { get; set; }
        public string BasePath { get; set; }
        public bool IsDirectory { get; set; }
        public string FileName { get; set; }
        public Guid DirID { get; set; }
        public long FileSize { get; set; }
        public long Transferred { get; set; }
        public TransferStatus Status { get; set; } = TransferStatus.Pending;
        public FileStream FileStream { get; set; }
        public List<FileItem> FileItems { get; } = new List<FileItem>();
        public DirectoryNode DirectoryRoot { get; set; }

        public ClientTransferState(string path)
        {
            IsDirectory = Directory.Exists(path);
            BasePath = path;
            SessionId = Guid.NewGuid();
            if (IsDirectory)
            {
                DirectoryRoot = DirectorySerializer.BuildDirectoryTree(path);
                FlattenFiles(DirectoryRoot, FileItems);
                DirID = DirectoryRoot.DirID;
            }
            else
            {
                var fileInfo = new FileInfo(path);
                FileName = fileInfo.Name;
                FileSize = fileInfo.Length;
            }
        }

        private void FlattenFiles(DirectoryNode node, List<FileItem> fileList)
        {
            fileList.AddRange(node.Files);
            foreach (var child in node.Children)
            {
                FlattenFiles(child, fileList);
            }
        }

        public void Dispose()
        {
            FileStream?.Dispose();
            Status = TransferStatus.Completed;
        }
    }
}
