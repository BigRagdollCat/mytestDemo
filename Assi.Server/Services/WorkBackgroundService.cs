using Assi.DotNetty.ChatTransmission;
using Assi.DotNetty.FileTransmission;
using Assi.DotNetty.ScreenTransmission;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Assi.Server.Services
{
    public class WorkBackgroundService : BackgroundService
    {
        private readonly EnhancedChatServer _enhancedChatServer;
        private readonly VideoBroadcastServer _videoBroadcastServer;

        public Action<ChatInfoModel<object>> OnChatInfo { get; set; }
        public Action<byte[]> OnVideo { get; set; }

        public WorkBackgroundService(EnhancedChatServer enhancedChatServer, VideoBroadcastServer videoBroadcastServer)
        {
            _enhancedChatServer = enhancedChatServer;
            _videoBroadcastServer = videoBroadcastServer;
            OnChatInfo += CanRun;
            OnVideo += CanRun2;
        }

        public void CanRun(ChatInfoModel<object> chatInfo)
        {

        }

        public void CanRun2(byte[] bytes) 
        {

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {   
            // 启动UDP服务
            await _enhancedChatServer.StartAsync(OnChatInfo);
            await _videoBroadcastServer.StartAsync(OnVideo);

            // 等待停止信号
            stoppingToken.Register(() => _enhancedChatServer.StopAsync());
            stoppingToken.WaitHandle.WaitOne();
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            // 停止UDP服务
            await _enhancedChatServer.StopAsync();
            await base.StopAsync(cancellationToken);
        }
    }
}

