using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FFmpeg.AutoGen;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;


namespace Assi.Services
{
    using System;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using FFmpeg.AutoGen;

    public static class FfmpegHelper
    {
        public static unsafe string GetErrorMessage(int errorCode)
        {
            const int bufferSize = 1024;
            byte* buffer = stackalloc byte[bufferSize];
            int ret = ffmpeg.av_strerror(errorCode, buffer, (ulong)bufferSize);
            return ret < 0 ? $"Unknown error ({errorCode})" : Marshal.PtrToStringAnsi((IntPtr)buffer)!;
        }

        public static void ThrowExceptionIfError(this int errorCode)
        {
            if (errorCode < 0) throw new InvalidOperationException($"FFmpeg error: {GetErrorMessage(errorCode)}");
        }
    }

    public unsafe class ScreenRecorder : IDisposable
    {
        // FFmpeg 资源
        private AVFormatContext* _inputFormatContext;
        private AVCodecContext* _videoDecoderContext;
        private AVCodecContext* _videoEncoderContext;
        private SwsContext* _swsContext;
        private AVFrame* _decodedFrame;
        private AVFrame* _encodedFrame;
        private AVPacket* _inputPacket;
        private AVPacket* _outputPacket;

        // 控制参数
        private volatile bool _isRunning;
        private Task? _encodingTask;
        private const int TargetFPS = 25;

        public event Action<byte[]>? OnEncodedFrame;

        public void Start()
        {
            _isRunning = true;
            ffmpeg.avformat_network_init();

            // 1. 初始化输入设备
            SetupInputDevice();

            // 2. 初始化解码器
            SetupDecoder();

            // 3. 初始化编码器
            SetupEncoder();

            // 4. 初始化数据容器
            _decodedFrame = ffmpeg.av_frame_alloc();
            _encodedFrame = ffmpeg.av_frame_alloc();
            _inputPacket = ffmpeg.av_packet_alloc();
            _outputPacket = ffmpeg.av_packet_alloc();

            // 5. 像素格式转换器
            SetupPixelConverter();

            // 6. 启动编码线程
            _encodingTask = Task.Run(EncodingLoop);
        }

        private void SetupInputDevice()
        {
            AVInputFormat* inputFormat = ffmpeg.av_find_input_format("gdigrab");
            AVFormatContext* ctx = null;
            int ret = ffmpeg.avformat_open_input(&ctx, "desktop", inputFormat, null);
            ret.ThrowExceptionIfError();
            _inputFormatContext = ctx;

            ffmpeg.avformat_find_stream_info(_inputFormatContext, null).ThrowExceptionIfError();
        }

        private void SetupDecoder()
        {
            int streamIndex = ffmpeg.av_find_best_stream(
                _inputFormatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, null, 0);
            AVStream* stream = _inputFormatContext->streams[streamIndex];

            AVCodec* codec = ffmpeg.avcodec_find_decoder(stream->codecpar->codec_id);
            _videoDecoderContext = ffmpeg.avcodec_alloc_context3(codec);
            ffmpeg.avcodec_parameters_to_context(_videoDecoderContext, stream->codecpar);
            ffmpeg.avcodec_open2(_videoDecoderContext, codec, null).ThrowExceptionIfError();
        }

        private void SetupEncoder()
        {
            AVCodec* codec = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_H264);
            _videoEncoderContext = ffmpeg.avcodec_alloc_context3(codec);

            // 编码器参数配置
            _videoEncoderContext->width = _videoDecoderContext->width;
            _videoEncoderContext->height = _videoDecoderContext->height;
            _videoEncoderContext->pix_fmt = codec->pix_fmts[0];
            _videoEncoderContext->time_base = new AVRational { num = 1, den = TargetFPS };
            _videoEncoderContext->framerate = new AVRational { num = TargetFPS, den = 1 };
            _videoEncoderContext->gop_size = TargetFPS * 2; // 2秒关键帧间隔
            _videoEncoderContext->max_b_frames = 1;

            // 设置编码预设
            ffmpeg.av_opt_set(_videoEncoderContext->priv_data, "preset", "fast", 0);
            ffmpeg.av_opt_set(_videoEncoderContext->priv_data, "tune", "zerolatency", 0);
            ffmpeg.avcodec_open2(_videoEncoderContext, codec, null).ThrowExceptionIfError();

            // 预分配编码帧缓冲区
            _encodedFrame->width = _videoEncoderContext->width;
            _encodedFrame->height = _videoEncoderContext->height;
            _encodedFrame->format = (int)_videoEncoderContext->pix_fmt;
            ffmpeg.av_frame_get_buffer(_encodedFrame, 32);
        }

        private void SetupPixelConverter()
        {
            _swsContext = ffmpeg.sws_getContext(
                _videoDecoderContext->width, _videoDecoderContext->height, _videoDecoderContext->pix_fmt,
                _videoEncoderContext->width, _videoEncoderContext->height, _videoEncoderContext->pix_fmt,
                ffmpeg.SWS_BICUBIC, null, null, null
            );

            if (_swsContext == null)
                throw new InvalidOperationException("Failed to initialize pixel converter");
        }

        private void EncodingLoop()
        {
            while (_isRunning)
            {
                try
                {
                    ProcessFrame();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Encoding error: {ex.Message}");
                    break;
                }
            }
            FlushEncoder();
        }

        private void ProcessFrame()
        {
            // 1. 读取输入包
            int ret = ffmpeg.av_read_frame(_inputFormatContext, _inputPacket);
            if (ret == ffmpeg.AVERROR_EOF) return;
            ret.ThrowExceptionIfError();

            // 2. 发送到解码器
            ffmpeg.avcodec_send_packet(_videoDecoderContext, _inputPacket)
                .ThrowExceptionIfError();
            ffmpeg.av_packet_unref(_inputPacket);

            // 3. 处理解码帧
            while (ffmpeg.avcodec_receive_frame(_videoDecoderContext, _decodedFrame) >= 0)
            {
                // 4. 像素格式转换
                ffmpeg.sws_scale(
                    _swsContext,
                    _decodedFrame->data, _decodedFrame->linesize, 0, _videoDecoderContext->height,
                    _encodedFrame->data, _encodedFrame->linesize
                );

                // 5. 编码处理
                _encodedFrame->pts = ffmpeg.av_rescale_q(
                    _decodedFrame->pts,
                    _videoDecoderContext->time_base,
                    _videoEncoderContext->time_base
                );

                ffmpeg.avcodec_send_frame(_videoEncoderContext, _encodedFrame)
                    .ThrowExceptionIfError();

                // 6. 获取编码包
                while (ffmpeg.avcodec_receive_packet(_videoEncoderContext, _outputPacket) >= 0)
                {
                    SendEncodedPacket();
                }
            }
        }

        private void SendEncodedPacket()
        {
            try
            {
                byte[] data = new byte[_outputPacket->size];
                Marshal.Copy((IntPtr)_outputPacket->data, data, 0, data.Length);
                OnEncodedFrame?.Invoke(data);
            }
            finally
            {
                ffmpeg.av_packet_unref(_outputPacket);
            }
        }

        private void FlushEncoder()
        {
            ffmpeg.avcodec_send_frame(_videoEncoderContext, null);
            while (ffmpeg.avcodec_receive_packet(_videoEncoderContext, _outputPacket) >= 0)
            {
                SendEncodedPacket();
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _encodingTask?.Wait(500);

            // 安全释放资源（反初始化顺序）
            fixed (AVPacket** p = &_outputPacket) ffmpeg.av_packet_free(p);
            fixed (AVPacket** p = &_inputPacket) ffmpeg.av_packet_free(p);
            fixed (AVFrame** p = &_encodedFrame) ffmpeg.av_frame_free(p);
            fixed (AVFrame** p = &_decodedFrame) ffmpeg.av_frame_free(p);
            fixed (SwsContext** p = &_swsContext) ffmpeg.sws_freeContext(*p);
            fixed (AVFormatContext** p = &_inputFormatContext) ffmpeg.avformat_close_input(p);
            fixed (AVCodecContext** p = &_videoEncoderContext) ffmpeg.avcodec_free_context(p);
            fixed (AVCodecContext** p = &_videoDecoderContext) ffmpeg.avcodec_free_context(p);
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }

        ~ScreenRecorder() => Dispose();
    }


    #region 废弃代码
    //public unsafe class ScreenRecorder : IDisposable
    //{
    //    // FFmpeg 上下文
    //    private AVFormatContext* _inputFormatContext;
    //    private AVCodecContext* _videoDecoderContext;
    //    private AVCodecContext* _videoEncoderContext;
    //    private SwsContext* _swsContext;
    //    private AVFrame* _decodedFrame;
    //    private AVFrame* _encodedFrame;
    //    private AVPacket* _inputPacket;
    //    private AVPacket* _outputPacket;

    //    // 控制录制线程
    //    private volatile bool _isRunning;
    //    private Task? _encodingTask;

    //    public event Action<byte[]>? OnEncodedFrame;

    //    public void Start()
    //    {
    //        _isRunning = true;
    //        ffmpeg.avformat_network_init();

    //        // 1. 打开输入设备 (Windows屏幕)
    //        AVInputFormat* inputFormat = ffmpeg.av_find_input_format("gdigrab");
    //        AVFormatContext* inputCtx = null;
    //        ffmpeg.avformat_open_input(&inputCtx, "desktop", inputFormat, null).ThrowExceptionIfError();
    //        _inputFormatContext = inputCtx;

    //        ffmpeg.avformat_find_stream_info(_inputFormatContext, null).ThrowExceptionIfError();

    //        // 2. 初始化解码器（针对输入流）
    //        int videoStreamIndex = ffmpeg.av_find_best_stream(_inputFormatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, null, 0);
    //        AVStream* videoStream = _inputFormatContext->streams[videoStreamIndex];
    //        AVCodec* decoder = ffmpeg.avcodec_find_decoder(videoStream->codecpar->codec_id);
    //        _videoDecoderContext = ffmpeg.avcodec_alloc_context3(decoder);
    //        ffmpeg.avcodec_parameters_to_context(_videoDecoderContext, videoStream->codecpar);
    //        ffmpeg.avcodec_open2(_videoDecoderContext, decoder, null).ThrowExceptionIfError();

    //        // 3. 初始化H264编码器
    //        AVCodec* encoder = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_H264);
    //        _videoEncoderContext = ffmpeg.avcodec_alloc_context3(encoder);

    //        _videoEncoderContext->width = _videoDecoderContext->width;
    //        _videoEncoderContext->height = _videoDecoderContext->height;
    //        _videoEncoderContext->pix_fmt = encoder->pix_fmts[0]; // 如YUV420P
    //        _videoEncoderContext->time_base = new AVRational { num = 1, den = 25 };
    //        _videoEncoderContext->framerate = new AVRational { num = 25, den = 1 };
    //        _videoEncoderContext->gop_size = 25;
    //        _videoEncoderContext->max_b_frames = 0;

    //        ffmpeg.av_opt_set(_videoEncoderContext->priv_data, "preset", "fast", 0); // 编码速度优化
    //        ffmpeg.avcodec_open2(_videoEncoderContext, encoder, null).ThrowExceptionIfError();

    //        // 4. 初始化帧和包
    //        _decodedFrame = ffmpeg.av_frame_alloc();
    //        _encodedFrame = ffmpeg.av_frame_alloc();
    //        _inputPacket = ffmpeg.av_packet_alloc();
    //        _outputPacket = ffmpeg.av_packet_alloc();

    //        // 5. 像素格式转换器 (例如: BGRA → YUV420P)
    //        _swsContext = ffmpeg.sws_getContext(
    //            _videoDecoderContext->width, _videoDecoderContext->height, _videoDecoderContext->pix_fmt,
    //            _videoEncoderContext->width, _videoEncoderContext->height, _videoEncoderContext->pix_fmt,
    //            ffmpeg.SWS_BILINEAR, null, null, null
    //        );

    //        // 6. 启动编码线程
    //        _encodingTask = Task.Run(EncodingLoop);
    //    }

    //    private void EncodingLoop()
    //    {
    //        while (_isRunning)
    //        {
    //            // 1. 从屏幕读取原始数据包
    //            int readRet = ffmpeg.av_read_frame(_inputFormatContext, _inputPacket);
    //            if (readRet < 0) continue;

    //            // 2. 发送到解码器
    //            int sendRet = ffmpeg.avcodec_send_packet(_videoDecoderContext, _inputPacket);
    //            ffmpeg.av_packet_unref(_inputPacket);
    //            if (sendRet < 0) continue;

    //            // 3. 接收解码后的帧
    //            while (ffmpeg.avcodec_receive_frame(_videoDecoderContext, _decodedFrame) >= 0)
    //            {
    //                // 4. 转换像素格式
    //                ffmpeg.sws_scale(
    //                    _swsContext,
    //                    _decodedFrame->data, _decodedFrame->linesize, 0, _videoDecoderContext->height,
    //                    _encodedFrame->data, _encodedFrame->linesize
    //                );

    //                // 5. 发送帧到编码器
    //                _encodedFrame->width = _videoEncoderContext->width;
    //                _encodedFrame->height = _videoEncoderContext->height;
    //                _encodedFrame->format = (int)_videoEncoderContext->pix_fmt;
    //                ffmpeg.avcodec_send_frame(_videoEncoderContext, _encodedFrame);

    //                // 6. 接收编码后的包
    //                while (ffmpeg.avcodec_receive_packet(_videoEncoderContext, _outputPacket) >= 0)
    //                {
    //                    byte[] data = new byte[_outputPacket->size];
    //                    Marshal.Copy((IntPtr)_outputPacket->data, data, 0, data.Length);
    //                    OnEncodedFrame?.Invoke(data);
    //                    ffmpeg.av_packet_unref(_outputPacket);
    //                }
    //            }
    //        }

    //        // 7. 刷新编码器缓冲区
    //        FlushEncoder();
    //    }

    //    private void FlushEncoder()
    //    {
    //        // 发送null帧触发缓冲数据处理
    //        ffmpeg.avcodec_send_frame(_videoEncoderContext, null);
    //        while (ffmpeg.avcodec_receive_packet(_videoEncoderContext, _outputPacket) >= 0)
    //        {
    //            byte[] data = new byte[_outputPacket->size];
    //            Marshal.Copy((IntPtr)_outputPacket->data, data, 0, data.Length);
    //            OnEncodedFrame?.Invoke(data);
    //            ffmpeg.av_packet_unref(_outputPacket);
    //        }
    //    }

    //    public void Stop()
    //    {
    //        _isRunning = false;
    //        _encodingTask?.Wait();

    //        // 释放所有资源
    //        // 使用 fixed 块逐个释放资源
    //        fixed (AVCodecContext** p = &_videoDecoderContext) ffmpeg.avcodec_free_context(p);
    //        fixed (AVCodecContext** p = &_videoEncoderContext) ffmpeg.avcodec_free_context(p);
    //        fixed (AVFormatContext** p = &_inputFormatContext) ffmpeg.avformat_close_input(p);
    //        fixed (SwsContext** p = &_swsContext) ffmpeg.sws_freeContext(*p);
    //        fixed (AVFrame** p = &_decodedFrame) ffmpeg.av_frame_free(p);
    //        fixed (AVFrame** p = &_encodedFrame) ffmpeg.av_frame_free(p);
    //        fixed (AVPacket** p = &_inputPacket) ffmpeg.av_packet_free(p);
    //        fixed (AVPacket** p = &_outputPacket) ffmpeg.av_packet_free(p);
    //    }

    //    public void Dispose()
    //    {
    //        Stop();
    //        GC.SuppressFinalize(this);
    //    }

    //    ~ScreenRecorder() => Dispose();
    //}
    #endregion
}
