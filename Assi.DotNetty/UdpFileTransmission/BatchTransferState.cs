using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;


namespace Assi.DotNetty.UdpFileTransmission
{
    public class BatchTransferState
    {
        public Guid SessionId { get; } = Guid.NewGuid();
        public List<ClientTransferState> FileStates { get; } = new();
        public bool IsComplete => FileStates.All(f => f.Status == TransferStatus.Completed);
    }
}
