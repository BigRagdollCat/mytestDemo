using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Transport.Channels;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Assi.DotNetty.FileTransmission
{

    public class EnhancedFileClientHandler : ChannelHandlerAdapter
    {
        private readonly string _fileName;
        private readonly FileAction _action;
        private readonly int _numThreads;
        private FileStream _fileStream;
        private long _totalSize;
        private long _currentOffset;
        private readonly object _lock = new();

        public EnhancedFileClientHandler(string fileName, FileAction action, int numThreads = 5)
        {
            _fileName = fileName;
            _action = action;
            _numThreads = numThreads;
            _fileStream = _action == FileAction.Upload ? File.OpenRead(fileName) : File.OpenWrite(fileName);
            _totalSize = _fileStream.Length;
        }

        public override void ChannelActive(IChannelHandlerContext context)
        {
            // 发送请求初始化
            var request = new FileRequest
            {
                FileName = _fileName,
                FileSize = _totalSize,
                Action = _action
            };
            context.WriteAndFlushAsync(request);

            // 启动多线程传输
            if (_action == FileAction.Upload)
            {
                StartUpload(context);
            }
            else
            {
                StartDownload(context);
            }
        }

        private void StartUpload(IChannelHandlerContext context)
        {
            var chunkSize = (int)(_totalSize / _numThreads);
            Parallel.For(0, _numThreads, i =>
            {
                var offset = i * chunkSize;
                var data = ReadChunk(offset, chunkSize);
                SendChunk(context, offset, data);
            });
        }

        private void StartDownload(IChannelHandlerContext context)
        {
            var chunkSize = (int)(_totalSize / _numThreads);
            Parallel.For(0, _numThreads, i =>
            {
                var offset = i * chunkSize;
                RequestChunk(context, offset, chunkSize);
            });
        }

        private byte[] ReadChunk(long offset, int size)
        {
            lock (_fileStream)
            {
                _fileStream.Seek(offset, SeekOrigin.Begin);
                var buffer = new byte[size];
                _fileStream.Read(buffer, 0, size);
                return buffer;
            }
        }

        private async Task SendChunk(IChannelHandlerContext context, long offset, byte[] data)
        {
            var chunk = new FileChunk { Offset = offset, Data = data };
            await context.WriteAndFlushAsync(chunk);
        }

        private void RequestChunk(IChannelHandlerContext context, long offset, int size)
        {
            // 发送请求下载指定偏移量的块（服务端需支持）
            // 这里简化处理，假设服务端支持按偏移量返回数据
            // 实际需实现更复杂的逻辑
            // context.WriteAndFlushAsync(new FileChunkRequest { Offset = offset, Size = size });
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            if (message is FileChunk chunk)
            {
                lock (_fileStream)
                {
                    _fileStream.Seek(chunk.Offset, SeekOrigin.Begin);
                    _fileStream.Write(chunk.Data);
                }
            }
        }
    }
}
