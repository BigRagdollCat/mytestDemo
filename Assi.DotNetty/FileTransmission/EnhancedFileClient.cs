using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels.Sockets;
using DotNetty.Transport.Channels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotNetty.Codecs;

namespace Assi.DotNetty.FileTransmission
{
    public class EnhancedFileClient
    {
        public async void Start() 
        {
            var bootstrap = new Bootstrap();
            bootstrap
                .Group(new MultithreadEventLoopGroup())
                .Channel<TcpSocketChannel>()
                .Option(ChannelOption.TcpNodelay, true);

            var handler = new EnhancedFileClientHandler("test.txt", FileAction.Upload, numThreads: 5);
            bootstrap.Handler(new ActionChannelInitializer<ISocketChannel>(channel =>
            {
                var pipeline = channel.Pipeline;
                pipeline.AddLast(new LengthFieldPrepender(4)); // 添加长度头
                pipeline.AddLast(handler);
            }));

            await bootstrap.ConnectAsync("127.0.0.1", 8080);
        }
    }
}
