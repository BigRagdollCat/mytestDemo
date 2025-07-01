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
        public Guid DirID { get; set; } // 当前文件所属的目录ID
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
                DirectoryRoot = BuildDirectoryTree(path);
                FlattenFiles(DirectoryRoot, FileItems);
            }
            else
            {
                var fileInfo = new FileInfo(path);
                FileName = fileInfo.Name;
                FileSize = fileInfo.Length;
            }
        }

        private DirectoryNode BuildDirectoryTree(string path)
        {
            var node = new DirectoryNode
            {
                Name = Path.GetFileName(path)
            };

            foreach (var dir in Directory.GetDirectories(path))
            {
                node.Children.Add(BuildDirectoryTree(dir));
            }

            foreach (var file in Directory.GetFiles(path))
            {
                node.Files.Add(new FileItem
                {
                    Name = Path.GetFileName(file),
                    Size = new FileInfo(file).Length,
                    LocalPath = file
                });
            }

            return node;
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

    public enum TransferStatus
    {
        Pending,
        Uploading,
        Downloading,
        Completed,
        Interrupted
    }
}
