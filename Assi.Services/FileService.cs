
using Assi.DotNetty.FileTransmission;
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
        EnhancedFileServer server { get; set; }
        public FileServer(string fileDirectory)
        {
            server = new EnhancedFileServer(9090, fileDirectory);
        }

        public void Start() 
        {
            server.StartAsync();
        }

        public async Task Stop() 
        {
            await server.StopAsync();
        }
    }
}
