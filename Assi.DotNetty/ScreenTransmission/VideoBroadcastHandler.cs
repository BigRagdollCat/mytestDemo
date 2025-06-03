using DotNetty.Transport.Channels.Sockets;
using DotNetty.Transport.Channels;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.DotNetty.ScreenTransmission
{
    public class VideoBroadcastHandler : SimpleChannelInboundHandler<DatagramPacket>
    {
        private readonly Action<byte[]> _frameHandler;
        private readonly ConcurrentQueue<byte[]> _frameQueue = new();
        private readonly System.Timers.Timer _processTimer = new(30); // 30ms = 33fps

        private readonly FrameReassembler _reassembler = new();
        private const int HEADER_SIZE = 13; // FrameHeader.ToBytes().Length

        public VideoBroadcastHandler(Action<byte[]> frameHandler)
        {
            _frameHandler = frameHandler;
            _processTimer.Elapsed += (s, e) => ProcessFrames();
            //_processTimer.Start();
        }

        protected override void ChannelRead0(IChannelHandlerContext context, DatagramPacket packet)
        {
            byte[] fullData = new byte[packet.Content.ReadableBytes];
            packet.Content.ReadBytes(fullData);

            // 解析帧头
            byte[] headerBytes = new byte[HEADER_SIZE];
            Buffer.BlockCopy(fullData, 0, headerBytes, 0, HEADER_SIZE);

            var header = FrameHeader.FromBytes(headerBytes);
            byte[] fragmentData = new byte[fullData.Length - HEADER_SIZE];
            Buffer.BlockCopy(fullData, HEADER_SIZE, fragmentData, 0, fragmentData.Length);

            // 分片重组
            byte[] fullFrame = _reassembler.Reassemble(
                header.FrameId,
                header.TotalFragments,
                header.FragmentId,
                fragmentData
            );

            if (fullFrame != null)
            {
                _frameHandler(fullFrame);
               // _frameQueue.Enqueue(fullFrame);
            }
        }

        private void ProcessFrames()
        {
            while (_frameQueue.TryDequeue(out var frame))
            {
                _frameHandler(frame);
            }
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Console.WriteLine($"接收视频帧异常: {exception.Message}");
            context.CloseAsync();
        }
    }
}
