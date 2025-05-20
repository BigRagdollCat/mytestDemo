using Assi.DotNetty.FileTransmission;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace Assi.DotNetty.ChatTransmission
{
    public class EnhancedChatServerHandler : SimpleChannelInboundHandler<DatagramPacket>, IDisposable
    {
        private readonly ConcurrentQueue<ChatInfoModel> _chatInfoQueue = new();
        private readonly System.Timers.Timer _chatTimer;
        private readonly Action<ChatInfoModel> _chatWork;

        public EnhancedChatServerHandler(Action<ChatInfoModel> chatWork)
        {
            _chatWork = chatWork;
            _chatTimer = new System.Timers.Timer(500);
            _chatTimer.Elapsed += ProcessMessages;
            _chatTimer.Start();
        }

        private void ProcessMessages(object? sender, ElapsedEventArgs e)
        {
            _chatTimer.Stop();
            try
            {
                while (_chatInfoQueue.TryDequeue(out var message))
                {
                    _chatWork(message);
                }
            }
            finally
            {
                _chatTimer.Start();
            }
        }

        protected override void ChannelRead0(IChannelHandlerContext ctx, DatagramPacket packet)
        {
            try
            {
                var jsonStr = packet.Content.ToString(Encoding.UTF8);
                var jsonObject = JsonConvert.DeserializeObject<ChatInfoModel>(jsonStr);
                if (jsonObject != null)
                {
                    _chatInfoQueue.Enqueue(jsonObject);
                }
            }
            catch (Exception ex)
            {
                // 记录消息解析错误
                throw;
            }
        }

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception exception)
        {
            ctx.CloseAsync();
        }

        public void Dispose()
        {
            _chatTimer?.Stop();
            _chatTimer?.Dispose();
        }
    }
}
