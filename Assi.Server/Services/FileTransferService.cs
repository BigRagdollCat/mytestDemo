using Assi.DotNetty.UdpFileTransmission;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Assi.Server.Services
{
    // FileTransferService.cs
    public class FileTransferService : BackgroundService
    {
        private UDPFileServer _server;
        private ClientMonitorService _monitor;

        protected override async Task ExecuteAsync(CancellationToken token)
        {
            _server = new UDPFileServer(8888, "C:\\FileServer");
            _server.Start();

            // 设置事件处理
            _server.OnClientConnected += client =>
                Logger.Info($"Client connected: {client.Name} ({client.IP})");

            _server.OnUploadCompleted += (client, file, size) =>
                Logger.Info($"Upload completed: {client.Name} => {file} ({size} bytes)");

            _server.OnDownloadCompleted += (client, file, size) =>
                Logger.Info($"Download completed: {client.Name} <= {file} ({size} bytes)");

            // 启动监控服务
            _monitor = new ClientMonitorService(_server);
            _ = _monitor.StartAsync(token);

            // 保持服务运行
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(1000, token);
            }

            _server.Stop();
        }
    }
}
