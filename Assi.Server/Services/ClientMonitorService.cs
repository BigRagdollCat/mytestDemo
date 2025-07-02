using Assi.DotNetty.UdpFileTransmission;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace Assi.Server.Services
{
    public class ClientMonitorService : BackgroundService
    {
        private readonly UDPFileServer _fileServer;
        private readonly ILogger<ClientMonitorService> _logger;

        public ClientMonitorService(UDPFileServer fileServer, ILogger<ClientMonitorService> logger)
        {
            _fileServer = fileServer;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Client monitoring service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    CheckClientStatus();
                    ReportTransferProgress();
                    await Task.Delay(5000, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Monitor service error");
                }
            }

            _logger.LogInformation("Client monitoring service stopped");
        }

        private void CheckClientStatus()
        {
            var offlineClients = _fileServer.ConnectedClients
                .Where(c => !c.IsAlive(TimeSpan.FromMinutes(5)))
                .ToList();

            foreach (var client in offlineClients)
            {
                _logger.LogWarning($"Client timeout: {client.ClientName}");
                _fileServer.DisconnectClient(client);
            }
        }

        private void ReportTransferProgress()
        {
            foreach (var client in _fileServer.ConnectedClients)
            {
                if (client.CurrentTransfer != null)
                {
                    var transfer = client.CurrentTransfer;
                    double percent = transfer.TotalSize > 0 ?
                        (double)transfer.Transferred / transfer.TotalSize * 100 : 0;

                    _logger.LogInformation($"[{client.ClientName}] " +
                        $"{transfer.FileName}: {percent:0.0}% " +
                        $"({transfer.Transferred}/{transfer.TotalSize} bytes)");
                }
            }
        }
    }

    public static class UDPFileServerExtensions
    {
        public static void DisconnectClient(this UDPFileServer server, ClientSession client)
        {
            server.GetType().GetMethod("RemoveClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(server, new object[] { client.SessionId });
        }
    }
}
