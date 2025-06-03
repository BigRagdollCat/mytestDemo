using Assi.DotNetty.FileTransmission;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.Student.Services
{
    public class FileClientService
    {
        EnhancedFileClient client { get; set; }

        public FileClientService(string Ip)
        {
            client = new EnhancedFileClient(Ip, 9090);
        }


        public async Task Start(string fileName)
        {
            await client.ConnectAndDownloadFile(fileName, startOffset: 0); // 从头下载
        }
    }
}
