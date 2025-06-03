using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using global::DotNetty.Transport.Bootstrapping;
using global::DotNetty.Transport.Channels.Sockets;
using global::DotNetty.Transport.Channels;

namespace Assi.DotNetty.FileTransmission
{
    public class EnhancedFileClient
    {
        private readonly string _host;
        private readonly int _port;

        public EnhancedFileClient(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public async Task ConnectAndDownloadFile(string fileName, long startOffset = 0)
        {
            var group = new MultithreadEventLoopGroup();
            try
            {
                var bootstrap = new Bootstrap();
                bootstrap.Group(group)
                         .Channel<TcpSocketChannel>()
                         .Handler(new ActionChannelInitializer<ISocketChannel>(channel =>
                         {
                             channel.Pipeline.AddLast(new FileChunkMessageEncoder());
                             channel.Pipeline.AddLast(new FileChunkMessageDecoder());
                             channel.Pipeline.AddLast(new FileClientHandler(fileName, 0, "downloads/"));
                         }));

                var channel = await bootstrap.ConnectAsync(_host, _port);

                // 发送文件请求（包含偏移量）
                var request = new FileChunkMessage
                {
                    Header = new FileMessageHeader
                    {
                        Type = MessageType.FileRequest,
                        FileName = fileName,
                        Offset = startOffset
                    }
                };

                await channel.WriteAndFlushAsync(request);
                Console.WriteLine($"已发送文件请求: {fileName}");
                await channel.CloseCompletion;
            }
            finally
            {
                await group.ShutdownGracefullyAsync();
            }
        }
    }
}
