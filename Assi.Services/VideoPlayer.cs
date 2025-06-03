using Avalonia.Media.Imaging;
using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Assi.Services
{
    public unsafe class VideoPlayer : IDisposable
    {
        private AVCodecContext* _decoderContext;
        private SwsContext* _swsContext;
        private AVFrame* _decodedFrame;
        private AVFrame* _rgbFrame;
        private IntPtr _rgbBuffer = IntPtr.Zero; // 改用 IntPtr 管理非托管内存
        private int _rgbBufferSize;
        private readonly object _decodeLock = new();

        // 在VideoPlayer类中修改事件声明
        public event Action<FrameRenderedEventArgs>? OnRenderFrame;
        public event Action<Exception>? OnDecodingError;

        public void Initialize()
        {
            // 查找并初始化 H.264 解码器
            AVCodec* codec = ffmpeg.avcodec_find_decoder(AVCodecID.AV_CODEC_ID_H264);
            if (codec == null)
                throw new InvalidOperationException("H.264 decoder not found");

            _decoderContext = ffmpeg.avcodec_alloc_context3(codec);
            if (_decoderContext == null)
                throw new OutOfMemoryException("Failed to allocate codec context");

            // 设置解码器参数
            _decoderContext->pix_fmt = AVPixelFormat.AV_PIX_FMT_YUV420P;
            int ret = ffmpeg.avcodec_open2(_decoderContext, codec, null);
            if (ret < 0)
                throw new InvalidOperationException($"Failed to open codec: {FFmpegError(ret)}");

            // 分配帧内存
            _decodedFrame = ffmpeg.av_frame_alloc();
            _rgbFrame = ffmpeg.av_frame_alloc();
            if (_decodedFrame == null || _rgbFrame == null)
                throw new OutOfMemoryException("Failed to allocate frames");
        }

        public void SubmitPacket(byte[] packetData)
        {
            lock (_decodeLock)
            {
                try
                {
                    // 创建 AVPacket
                    AVPacket* packet = ffmpeg.av_packet_alloc();
                    if (packet == null) return;

                    try
                    {
                        fixed (byte* pData = packetData)
                        {
                            packet->data = pData;
                            packet->size = packetData.Length;
                        }

                        // 发送数据包
                        int ret = ffmpeg.avcodec_send_packet(_decoderContext, packet);
                        if (ret < 0 && ret != ffmpeg.AVERROR(ffmpeg.EAGAIN))
                            ThrowFFmpegError("Error sending packet", ret);

                        // 接收解码帧
                        while (true)
                        {
                            ret = ffmpeg.avcodec_receive_frame(_decoderContext, _decodedFrame);
                            if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN) || ret == ffmpeg.AVERROR_EOF)
                                break;
                            else if (ret < 0)
                                ThrowFFmpegError("Error receiving frame", ret);

                            ConvertToRGB();
                            RenderFrame();
                        }
                    }
                    finally
                    {
                        ffmpeg.av_packet_free(&packet);
                    }
                }
                catch (Exception ex)
                {
                    OnDecodingError?.Invoke(ex);
                }
            }
        }
        private void ConvertToRGB()
        {
            // 检查是否需要重新初始化转换器
            bool needsReinit = _swsContext == null ||
                               _decoderContext->width != _rgbFrame->width ||
                               _decoderContext->height != _rgbFrame->height;

            if (needsReinit)
            {
                // 释放旧资源
                if (_swsContext != null)
                {
                    ffmpeg.sws_freeContext(_swsContext);
                    _swsContext = null;
                }

                if (_rgbBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_rgbBuffer);
                    _rgbBuffer = IntPtr.Zero;
                }

                // 创建像素格式转换器
                _swsContext = ffmpeg.sws_getContext(
                    _decoderContext->width, _decoderContext->height, _decoderContext->pix_fmt,
                    _decoderContext->width, _decoderContext->height, AVPixelFormat.AV_PIX_FMT_BGR24,
                    ffmpeg.SWS_BILINEAR, null, null, null
                );

                if (_swsContext == null)
                    throw new InvalidOperationException("Failed to create SWS context");

                // 分配 RGB 缓冲区
                _rgbBufferSize = ffmpeg.av_image_get_buffer_size(
                    AVPixelFormat.AV_PIX_FMT_BGR24,
                    _decoderContext->width, _decoderContext->height, 1);

                _rgbBuffer = Marshal.AllocHGlobal(_rgbBufferSize);

                // 修复点1: 使用正确的参数类型配置 RGB 帧
                byte_ptrArray4 dstData = new byte_ptrArray4();
                int_array4 dstLinesize = new int_array4();

                // 正确调用 av_image_fill_arrays
                int ret = ffmpeg.av_image_fill_arrays(
                    ref dstData,
                    ref dstLinesize,
                    (byte*)_rgbBuffer,
                    AVPixelFormat.AV_PIX_FMT_BGR24,
                    _decoderContext->width,
                    _decoderContext->height,
                    1
                );

                if (ret < 0)
                    ThrowFFmpegError("Failed to fill image arrays", ret);

                // 设置帧属性 - 使用 uint 索引
                for (uint i = 0; i < 4; i++)
                {
                    _rgbFrame->data[i] = dstData[i];
                    _rgbFrame->linesize[i] = dstLinesize[i];
                }

                // 修复点2: 正确设置帧格式
                _rgbFrame->width = _decoderContext->width;
                _rgbFrame->height = _decoderContext->height;
                _rgbFrame->format = (int)AVPixelFormat.AV_PIX_FMT_BGR24;
            }

            // 执行格式转换
            ffmpeg.sws_scale(
                _swsContext,
                _decodedFrame->data,
                _decodedFrame->linesize,
                0,
                (int)_decoderContext->height,  // 修复点3: 添加显式类型转换
                _rgbFrame->data,
                _rgbFrame->linesize
            );
        }

        private void RenderFrame()
        {
            if (_rgbFrame == null || _rgbFrame->data[0] == null)
                return;

            int width = _rgbFrame->width;
            int height = _rgbFrame->height;
            int stride = _rgbFrame->linesize[0];
            int actualSize = height * stride;

            byte[] frameData = new byte[actualSize];
            Marshal.Copy(_rgbBuffer, frameData, 0, actualSize);
            // 传递尺寸信息
            OnRenderFrame?.Invoke(new FrameRenderedEventArgs(
                frameData,
                _rgbFrame->width,
                _rgbFrame->height,
                stride
            ));
        }

        public void Dispose()
        {
            lock (_decodeLock)
            {
                if (_decoderContext != null)
                {
                    fixed (AVCodecContext** ctx = &_decoderContext)
                    {
                        ffmpeg.avcodec_free_context(ctx);
                    }
                }

                if (_swsContext != null)
                {
                    ffmpeg.sws_freeContext(_swsContext);
                    _swsContext = null;
                }

                if (_decodedFrame != null)
                {
                    fixed (AVFrame** frame = &_decodedFrame)
                    {
                        ffmpeg.av_frame_free(frame);
                    }
                }

                if (_rgbFrame != null)
                {
                    fixed (AVFrame** frame = &_rgbFrame)
                    {
                        ffmpeg.av_frame_free(frame);
                    }
                }

                if (_rgbBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_rgbBuffer);
                    _rgbBuffer = IntPtr.Zero;
                    _rgbBufferSize = 0;
                }
            }
        }

        // 错误处理辅助方法
        private void ThrowFFmpegError(string message, int errorCode)
            => throw new InvalidOperationException($"{message}: {FFmpegError(errorCode)}");

        private static string FFmpegError(int errorCode)
        {
            const int bufferSize = 256;
            byte* buffer = stackalloc byte[bufferSize];
            ffmpeg.av_strerror(errorCode, buffer, bufferSize);
            return Marshal.PtrToStringAnsi((IntPtr)buffer) ?? $"Unknown error ({errorCode})";
        }
    }
}
