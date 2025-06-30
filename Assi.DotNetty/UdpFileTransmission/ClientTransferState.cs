using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.DotNetty.UdpFileTransmission
{
    using System;
    using System.IO;

    public class ClientTransferState : IDisposable
    {
        public Guid SessionId { get; set; }
        public string BasePath { get; set; }
        public bool IsDirectory { get; set; }
        public string FileName { get; set; }
        public string RelativePath { get; set; }
        public long FileSize { get; set; }
        public long Transferred { get; set; }
        public TransferStatus Status { get; set; } = TransferStatus.Pending;
        public FileStream FileStream { get; set; }

        public ClientTransferState(string path)
        {
            IsDirectory = Directory.Exists(path);
            BasePath = path;
            SessionId = Guid.NewGuid();
            if (IsDirectory) InitializeDirectoryItems(path);
            else InitializeFileItem(path);
        }

        private void InitializeDirectoryItems(string rootPath)
        {
            // 递归处理
        }

        private void InitializeFileItem(string path)
        {
            var fileInfo = new FileInfo(path);
            FileName = fileInfo.Name;
            RelativePath = "";
            FileSize = fileInfo.Length;
            BasePath = path;
        }

        public void MoveToNextFile() { }
        public void Dispose() => Status = TransferStatus.Completed;
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
