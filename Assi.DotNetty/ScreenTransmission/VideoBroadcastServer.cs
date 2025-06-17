using DotNetty.Buffers;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels.Sockets;
using DotNetty.Transport.Channels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Assi.DotNetty.ChatTransmission;
using Newtonsoft.Json;
using System.Net.Sockets;

namespace Assi.DotNetty.ScreenTransmission
{
    public class VideoBroadcastServer
    {
        // 最大 UDP 载荷（IPv4 下为 1472 字节）
        private const int MAX_UDP_SIZE = 1472;
        private readonly Bootstrap _bootstrap;
        private IChannel? _channel = null;
        private readonly MultithreadEventLoopGroup _group;
        private readonly int _port;
        private readonly int _broadcastPort;
        private readonly IPEndPoint _broadcastAddress;

        public VideoBroadcastServer(int port, int broadcastPort, int threadCount = 1)
        {
            // 初始化广播地址
            _broadcastAddress = new IPEndPoint(IPAddress.Parse("192.168.9.255"), port);
            _port = port;
            _broadcastPort = broadcastPort;
            _group = new MultithreadEventLoopGroup(threadCount);
            _bootstrap = new Bootstrap();
        }

        public async Task StartAsync(Action<byte[]> frameHandler)
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
                    channel.Pipeline.AddLast(new VideoBroadcastHandler(frameHandler));
                }));
                _channel = await _bootstrap.BindAsync(_port);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private int _globalFrameId = 0;

        public async Task BroadcastFrameAsync(byte[] frameData, int port)
        {
            // 增加全局帧序号
            int frameId = Interlocked.Increment(ref _globalFrameId);

            int totalFragments = (int)Math.Ceiling(frameData.Length / (double)MAX_UDP_SIZE);

            for (int fragmentId = 0; fragmentId < totalFragments; fragmentId++)
            {
                int offset = fragmentId * MAX_UDP_SIZE;
                int remaining = frameData.Length - offset;
                int chunkSize = Math.Min(MAX_UDP_SIZE, remaining);

                byte[] chunk = new byte[chunkSize];
                Buffer.BlockCopy(frameData, offset, chunk, 0, chunkSize);

                // 构造帧头
                var header = new FrameHeader
                {
                    FrameId = frameId,
                    FragmentId = fragmentId,
                    TotalFragments = totalFragments,
                    IsLastFragment = fragmentId == totalFragments - 1
                };

                // 合并帧头 + 数据
                byte[] packetData = new byte[header.ToBytes().Length + chunk.Length];
                Buffer.BlockCopy(header.ToBytes(), 0, packetData, 0, header.ToBytes().Length);
                Buffer.BlockCopy(chunk, 0, packetData, header.ToBytes().Length, chunk.Length);


                var byteBuffer = Unpooled.CopiedBuffer(packetData);
                // 使用受限广播地址
                var broadcastAddress = new IPEndPoint(IPAddress.Parse("192.168.9.255"), port);
                var datagramPacket = new DatagramPacket(byteBuffer, broadcastAddress);

                try
                {
                    await _channel.WriteAndFlushAsync(datagramPacket);
                }
                catch (Exception ex) when (ex is SocketException || ex is IOException)
                {
                    Console.WriteLine($"发送分片失败: {ex.Message}");
                }
            }
        }

        public async Task StopAsync()
        {
            if (_channel != null && _channel.Active) await _channel.CloseAsync();
            await _group.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1));
        }
    }
}
