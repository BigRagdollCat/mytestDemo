using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Transport.Channels;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace Assi.DotNetty.FileTransmission
{
    public class EnhancedFileServerHandler : ChannelHandlerAdapter
    {
        private readonly ConcurrentDictionary<IChannel, ClientTransferState> _clientStates = new();

        public override void ChannelActive(IChannelHandlerContext context)
        {
            // 获取客户端连接信息
            var remoteAddress = context.Channel.RemoteAddress as System.Net.IPEndPoint;
            string ip = remoteAddress?.Address.ToString() ?? "unknown";
            int port = remoteAddress?.Port ?? 0;

            // 创建客户端状态对象
            var state = new ClientTransferState("", 0)
            {
                Ip = ip,
                Port = port,
                Status = TransferStatus.Pending
            };

            _clientStates.TryAdd(context.Channel, state);
            Console.WriteLine($"客户端连接: {ip}:{port}");
        }

        private void HandleRequest(IChannelHandlerContext context, FileRequest request)
        {
            var client = context.Channel;
            if (!_clientStates.TryGetValue(client, out var state))
            {
                context.WriteAndFlushAsync("传输状态错误");
                return;
            }

            state.FileName = request.FileName;
            state.TotalSize = request.FileSize;
            state.CurrentOffset = 0;
            state.StartTime = DateTime.Now;
            state.LastUpdateTime = state.StartTime;

            switch (request.Action)
            {
                case FileAction.Upload:
                    state.Status = TransferStatus.Uploading;
                    state.FileStream = File.Open(request.FileName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
                    context.WriteAndFlushAsync($"开始上传: {request.FileName}");
                    break;

                case FileAction.Download:
                    state.Status = TransferStatus.Downloading;
                    if (File.Exists(request.FileName))
                    {
                        state.FileStream = File.OpenRead(request.FileName);
                        context.WriteAndFlushAsync($"开始下载: {request.FileName}");
                    }
                    else
                    {
                        state.Status = TransferStatus.Interrupted;
                        context.WriteAndFlushAsync($"文件不存在: {request.FileName}");
                    }
                    break;
            }
        }

        private void HandleChunk(IChannelHandlerContext context, FileChunk chunk)
        {
            var client = context.Channel;
            if (!_clientStates.TryGetValue(client, out var state) || state.FileStream == null)
            {
                context.WriteAndFlushAsync("无效的传输状态");
                return;
            }

            // 验证偏移量
            if (!state.IsOffsetValid(chunk.Offset))
            {
                context.WriteAndFlushAsync($"偏移量错误: 预期 {state.CurrentOffset}, 收到 {chunk.Offset}");
                return;
            }

            // 写入数据
            lock (state.LockObject)
            {
                state.FileStream.Seek(chunk.Offset, SeekOrigin.Begin);
                state.FileStream.Write(chunk.Data, 0, chunk.Data.Length);
                state.UpdateOffset(chunk.Offset + chunk.Data.Length, chunk.Data.Length);
            }

            // 检查是否完成
            if (state.CurrentOffset >= state.TotalSize)
            {
                state.Dispose();
                context.WriteAndFlushAsync("传输完成");
            }
            else
            {
                // 定期报告进度
                if (DateTime.Now - state.LastUpdateTime > TimeSpan.FromSeconds(1))
                {
                    double progress = state.GetProgressPercentage();
                    TimeSpan remaining = state.GetEstimatedRemainingTime();
                    context.WriteAndFlushAsync($"进度: {progress:F1}%, 剩余时间: {remaining:mm\\:ss}");
                }
            }
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            var client = context.Channel;
            if (_clientStates.TryRemove(client, out var state))
            {
                if (state.Status == TransferStatus.Uploading || state.Status == TransferStatus.Downloading)
                {
                    state.Status = TransferStatus.Interrupted;
                    Console.WriteLine($"传输中断: {state.FileName}, 进度: {state.GetProgressPercentage():F1}%");
                }
                state.Dispose();
            }
        }
    }
}
