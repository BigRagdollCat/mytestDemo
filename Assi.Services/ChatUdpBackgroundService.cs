using Assi.DotNetty.ChatTransmission;
using Assi.DotNetty.FileTransmission;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Assi.Services
{
    public class ChatUdpBackgroundService : BackgroundService
    {
        private readonly EnhancedChatServer _enhancedChatServer;

        public Action<ChatInfoModel> OnChatInfo { get; set; }

        public ChatUdpBackgroundService(EnhancedChatServer enhancedChatServer)
        {
            _enhancedChatServer = enhancedChatServer;
            OnChatInfo += ChatRun;
        }

        public void ChatRun(ChatInfoModel cinfo) 
        {

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {    // 启动UDP服务
            await _enhancedChatServer.StartAsync(OnChatInfo);

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
