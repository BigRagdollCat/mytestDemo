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
        private readonly Bootstrap _bootstrap;
        private IChannel? _channel = null;
        private readonly MultithreadEventLoopGroup _group;
        private readonly int _port;

        public EnhancedFileServer(int port, int threadCount)
        {
            _port = port;
            _group = new MultithreadEventLoopGroup(threadCount);
            _bootstrap = new Bootstrap();
        }
        public async Task Start()
        {
            try
            {
                var bootstrap = new ServerBootstrap();
                bootstrap
                    .Group(_group)
                    .Channel<TcpServerSocketChannel>()
                    .Option(ChannelOption.SoBacklog, 100)
                    .ChildHandler(new ActionChannelInitializer<ISocketChannel>(channel =>
                    {
                        var pipeline = channel.Pipeline;
                        pipeline.AddLast(new LengthFieldBasedFrameDecoder(
                            int.MaxValue, 0, 4, 0, 4)); // 处理分块长度
                        pipeline.AddLast(new EnhancedFileServerHandler());
                    }));

                _channel = await bootstrap.BindAsync(_port);

            }
            finally
            {
            }
        }
        public async Task Stop()
        {
            if (_channel != null && _channel.Active)
            {
                await _channel.CloseAsync();
            }
        }
    }
}
