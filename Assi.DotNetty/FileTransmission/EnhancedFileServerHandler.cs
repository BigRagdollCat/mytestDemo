using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Transport.Channels;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace Assi.DotNetty.FileTransmission
{
    public class EnhancedFileServerHandler : ChannelHandlerAdapter
    {
        private readonly ConcurrentDictionary<string, FileTransferState> _fileStates = new();
        private readonly ConcurrentDictionary<IChannel, ClientContext> _clientContexts = new();

        public override void ChannelActive(IChannelHandlerContext context)
        {
            _clientContexts.TryAdd(context.Channel, new ClientContext());
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            if (message is FileRequest request)
            {
                HandleRequest(context, request);
            }
            else if (message is FileChunk chunk)
            {
                HandleChunk(context, chunk);
            }
        }

        private void HandleRequest(IChannelHandlerContext context, FileRequest request)
        {
            var client = context.Channel;
            var fileName = request.FileName;
            var action = request.Action;

            switch (action)
            {
                case FileAction.Upload:
                    // 初始化上传状态
                    var uploadState = new FileTransferState
                    {
                        FileName = fileName,
                        TotalSize = request.FileSize,
                        CurrentOffset = 0,
                        FileStream = File.Open(fileName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None)
                    };
                    _fileStates.TryAdd(fileName, uploadState);
                    _clientContexts[client].CurrentFile = uploadState;
                    context.WriteAndFlushAsync($"Upload started for {fileName}");
                    break;

                case FileAction.Download:
                    // 初始化下载状态
                    if (File.Exists(fileName))
                    {
                        var downloadState = new FileTransferState
                        {
                            FileName = fileName,
                            TotalSize = new FileInfo(fileName).Length,
                            CurrentOffset = 0,
                            FileStream = File.OpenRead(fileName)
                        };
                        _clientContexts[client].CurrentFile = downloadState;
                        context.WriteAndFlushAsync($"Download started for {fileName}");
                    }
                    else
                    {
                        context.WriteAndFlushAsync($"File not found: {fileName}");
                    }
                    break;
            }
        }

        private void HandleChunk(IChannelHandlerContext context, FileChunk chunk)
        {
            var client = context.Channel;
            if (!_clientContexts.TryGetValue(client, out var clientCtx) || clientCtx.CurrentFile == null)
            {
                context.WriteAndFlushAsync("Invalid transfer state");
                return;
            }

            var fileState = clientCtx.CurrentFile;
            var receivedOffset = chunk.Offset;
            var data = chunk.Data;

            // 断点续传：验证偏移量
            if (receivedOffset != fileState.CurrentOffset)
            {
                context.WriteAndFlushAsync($"Invalid offset: Expected {fileState.CurrentOffset}, got {receivedOffset}");
                return;
            }

            // 写入数据
            lock (fileState.FileStream)
            {
                fileState.FileStream.Seek(receivedOffset, SeekOrigin.Begin);
                fileState.FileStream.Write(data);
                fileState.CurrentOffset += data.Length;
            }

            // 完成上传
            if (fileState.CurrentOffset >= fileState.TotalSize)
            {
                fileState.FileStream.Close();
                _fileStates.TryRemove(fileState.FileName, out _);
                _clientContexts[client].CurrentFile = null;
                context.WriteAndFlushAsync("Transfer completed");
            }
            else
            {
                context.WriteAndFlushAsync($"Received {data.Length} bytes at offset {receivedOffset}");
            }
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Console.WriteLine($"Exception: {exception.Message}");
            context.CloseAsync();
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            var client = context.Channel;
            if (_clientContexts.TryRemove(client, out var clientCtx))
            {
                clientCtx.CurrentFile?.FileStream?.Close();
            }
        }
    }
}
