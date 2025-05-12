using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.DotNetty.FileTransmission
{
    // 文件传输状态
    public class FileTransferState
    {
        public string FileName { get; set; }
        public long TotalSize { get; set; }
        public long CurrentOffset { get; set; }
        public FileStream FileStream { get; set; }
    }

    // 客户端上下文
    public class ClientContext
    {
        public FileTransferState CurrentFile { get; set; }
    }

    // 文件请求
    public class FileRequest
    {
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public FileAction Action { get; set; }
    }

    public enum FileAction
    {
        Upload,
        Download
    }

    // 文件分块
    public class FileChunk
    {
        public long Offset { get; set; }
        public byte[] Data { get; set; }
    }
}
