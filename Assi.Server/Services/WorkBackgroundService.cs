using Assi.DotNetty.ChatTransmission;
using Assi.DotNetty.FileTransmission;
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
        private readonly EnhancedFileServer _enhancedFileServer;

        public Action<ChatInfoModel> OnChatInfo { get; set; }

        public WorkBackgroundService(EnhancedChatServer enhancedChatServer, EnhancedFileServer enhancedFileServer)
        {
            _enhancedChatServer = enhancedChatServer;
            _enhancedFileServer = enhancedFileServer;
            OnChatInfo += CanRun;
        }

        public void CanRun(ChatInfoModel chatInfo)
        {
            
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {   
            // 启动UDP服务
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

