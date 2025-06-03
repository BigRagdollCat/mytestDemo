using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels.Sockets;
using DotNetty.Transport.Channels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotNetty.Buffers;
using System.Net;
using Newtonsoft.Json;
using System.Net.Sockets;

namespace Assi.DotNetty.ChatTransmission
{

    public class EnhancedChatServer
    {
        private readonly Bootstrap _bootstrap;
        private IChannel? _channel = null;
        private readonly MultithreadEventLoopGroup _group;
        private readonly int _port;
        private readonly int _broadcastPort;

        public EnhancedChatServer(int port, int broadcastPort, int threadCount = 1)
        {
            _port = port;
            _broadcastPort = broadcastPort;
            _group = new MultithreadEventLoopGroup(threadCount);
            _bootstrap = new Bootstrap();
        }

        public async Task StartAsync(Action<ChatInfoModel<object>> _chatWork)
        {
            try
            {
                _bootstrap
                .Group(_group)
                .Channel<SocketDatagramChannel>()
                .Option(ChannelOption.SoBroadcast, true)
                .Option(ChannelOption.SoReuseaddr, true)
                .Handler(new ActionChannelInitializer<IChannel>(channel =>
                {
                    channel.Pipeline.AddLast(new EnhancedChatServerHandler(_chatWork));
                }));
                _channel = await _bootstrap.BindAsync(_port);
            }
            catch (Exception ex)
            {
                throw;
            }

        }

        /// <summary>
        /// 向指定的目标地址发送消息。
        /// </summary>
        /// <param name="targetIp">目标 IP 地址。</param>
        /// <param name="targetPort">目标端口号。</param>
        /// <param name="message">要发送的消息。</param>
        /// <returns>是否成功发送消息。</returns>
        public async Task<bool> SendMessageAsync<T>(string targetIp, int targetPort, ChatInfoModel<T> message)
        {
            if (_channel == null || !_channel.Active)
            {
                throw new InvalidOperationException("Server is not running");
            }

            string msg = JsonConvert.SerializeObject(message);
            var byteBuffer = Unpooled.CopiedBuffer(msg, Encoding.UTF8);

            var remoteEndpoint = new IPEndPoint(IPAddress.Parse(targetIp), targetPort);
            var datagramPacket = new DatagramPacket(byteBuffer, remoteEndpoint);

            try
            {
                await _channel.WriteAndFlushAsync(datagramPacket);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send message: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 向局域网广播消息
        /// </summary>
        public async Task BroadcastAsync<T>(ChatInfoModel<T> message,int port)
        {
            if (_channel == null || !_channel.Active)
            {
                throw new InvalidOperationException("Server is not running");
            }

            try
            {
                string msg = JsonConvert.SerializeObject(message);
                var byteBuffer = Unpooled.CopiedBuffer(msg, Encoding.UTF8);

                // 使用受限广播地址
                var broadcastAddress = new IPEndPoint(IPAddress.Parse("255.255.255.255"), port);
                var datagramPacket = new DatagramPacket(byteBuffer, broadcastAddress);

                await _channel.WriteAndFlushAsync(datagramPacket);
                Console.WriteLine($"Broadcast sent to {broadcastAddress}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Broadcast failed: {ex.Message}");
                throw;
            }
        }

        public async Task StopAsync()
        {
            if (_channel != null && _channel.Active)
            {
                await _channel.CloseAsync();
            }
            await _group.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1));
        }
    }
}
