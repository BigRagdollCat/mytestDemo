using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assi.DotNetty.FileTransmission;

namespace Assi.Server.Services
{
    public static class FileWorkService
    {
        private static ConcurrentDictionary<string, ClientTransferState> _clientStates { get; set; } = 
            new ConcurrentDictionary<string, ClientTransferState>();
    }
}
