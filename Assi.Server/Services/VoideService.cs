using Assi.DotNetty.ChatTransmission;
using Assi.DotNetty.ScreenTransmission;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.Server.Services
{
    public class VoideService
    {
        private readonly VideoBroadcastServer _videoBroadcastServer;
        public VoideService(VideoBroadcastServer videoBroadcastServer)
        {
            _videoBroadcastServer = videoBroadcastServer;
        }

        public async void VoideRun(byte[] datas)
        {
            await WorkServer(datas);
        }


        public async Task WorkServer(byte[] datas)
        {

        }
    }
}
