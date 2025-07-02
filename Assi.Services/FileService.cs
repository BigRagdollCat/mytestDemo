
using Assi.DotNetty.FileTransmission;
using Assi.DotNetty.UdpFileTransmission;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.Services
{
    public class FileServer
    {
        public bool IsRun { get; set; } = false;
        UDPFileServer server { get; set; }
        public FileServer(string fileDirectory)
        {
            server = new UDPFileServer(9090, fileDirectory);
        }

        public void Start()
        {
            // 启动服务器
            server.Start();
        }

        public void BroadcastFile(string filePaht) 
        {
            server.BroadcastFile(filePaht);
        }

        public async Task Stop()
        {
            // 停止服务器
            server.Stop();
        }
    }
}
