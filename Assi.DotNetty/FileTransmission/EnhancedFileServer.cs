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
using DotNetty.Buffers;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using global::DotNetty.Transport.Channels.Groups;

namespace Assi.DotNetty.FileTransmission
{

    public class EnhancedFileServer
    {
        private readonly int _port;
        private readonly string _fileDirectory;
        private MultithreadEventLoopGroup _bossGroup;
        private MultithreadEventLoopGroup _workerGroup;
        private IChannel _serverChannel;

        public EnhancedFileServer(int port, string fileDirectory)
        {
            _port = port;
            _fileDirectory = fileDirectory;
        }

        public async Task StartAsync()
        {
            _bossGroup = new MultithreadEventLoopGroup(1);
            _workerGroup = new MultithreadEventLoopGroup();

            var bootstrap = new ServerBootstrap();
            bootstrap.Group(_bossGroup, _workerGroup)
                     .Channel<TcpServerSocketChannel>()
                     .Option(ChannelOption.SoBacklog, 100)
                     .ChildHandler(new ActionChannelInitializer<ISocketChannel>(channel =>
                     {
                         // 服务端
                         channel.Pipeline.AddLast(new FileChunkMessageEncoder());
                         channel.Pipeline.AddLast(new FileChunkMessageDecoder());
                         channel.Pipeline.AddLast(new FileServerHandler(_fileDirectory));
                     }))
                     .ChildOption(ChannelOption.SoKeepalive, true);

            _serverChannel = await bootstrap.BindAsync(_port);
            Console.WriteLine($"文件服务器已启动，监听端口 {_port}");
            await _serverChannel.CloseCompletion;
        }

        public async Task StopAsync()
        {
            try
            {
                if (_serverChannel != null) await _serverChannel.CloseAsync();
                if (_workerGroup != null) await _workerGroup.ShutdownGracefullyAsync();
                if (_bossGroup != null) await _bossGroup.ShutdownGracefullyAsync();
                Console.WriteLine("文件服务器已关闭");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"关闭服务器时发生异常: {ex.Message}");
            }
        }
    }
}
