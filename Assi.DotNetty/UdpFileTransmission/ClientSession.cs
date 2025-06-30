using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Collections.Concurrent;


namespace Assi.DotNetty.UdpFileTransmission
{
    public class ClientSession
    {
        public Guid SessionId { get; set; }
        public IPEndPoint RemoteEndpoint { get; }
        public long TotalTransferred { get; set; }
        public long TotalSize { get; set; }
        public FileStream ActiveFile { get; set; }
        public ConcurrentDictionary<uint, byte[]> PendingAcks { get; } = new();

        private uint _sequenceCounter = 1;
        public uint GetNextSequence() => _sequenceCounter++;

        public ClientSession(IPEndPoint remote) => RemoteEndpoint = remote;
    }
}
