using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels.Sockets;
using DotNetty.Transport.Channels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotNetty.Codecs;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace Assi.DotNetty.FileTransmission
{
    public class EnhancedFileServer
    {
        private IChannel? channelFuture;
        private int _port;

        public EnhancedFileServer(int port) 
        {
            this._port = port;
        }
        public async Task Start()
        {
            var bossGroup = new MultithreadEventLoopGroup(1);
            var workerGroup = new MultithreadEventLoopGroup(Environment.ProcessorCount * 2); // 增加线程数

            try
            {
                var bootstrap = new ServerBootstrap();
                bootstrap
                    .Group(bossGroup, workerGroup)
                    .Channel<TcpServerSocketChannel>()
                    .Option(ChannelOption.SoBacklog, 100)
                    .ChildHandler(new ActionChannelInitializer<ISocketChannel>(channel =>
                    {
                        var pipeline = channel.Pipeline;
                        pipeline.AddLast(new LengthFieldBasedFrameDecoder(
                            int.MaxValue, 0, 4, 0, 4)); // 处理分块长度
                        pipeline.AddLast(new EnhancedFileServerHandler());
                    }));

                channelFuture = await bootstrap.BindAsync(_port);

            }
            finally
            {
            }
        }
        public async Task Stop()
        {
            if (channelFuture != null && channelFuture.Active)
            {
                await channelFuture.CloseAsync();
            }
        }
    }
}
