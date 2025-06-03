using FFmpeg.AutoGen;
using System.Runtime.InteropServices;
using System.Buffers;
using System.Diagnostics;


namespace Assi.Services
{
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
            if (errorCode < 0)
                throw new InvalidOperationException($"FFmpeg error: {GetErrorMessage(errorCode)}");
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

        private int _videoStreamIndex;

        // 控制参数
        private volatile bool _isRunning;
        private Task? _encodingTask;
        private const int TargetFPS = 25;

        // 优化新增字段
        private long _lastPts = -1;
        private readonly ArrayPool<byte> _packetPool = ArrayPool<byte>.Shared;
        private readonly Stopwatch _frameTimer = new Stopwatch();
        private int _frameCount;
        private readonly object _disposeLock = new object();

        // 用于跟踪解码器参数变化
        private int _lastDecoderWidth = -1;
        private int _lastDecoderHeight = -1;
        private AVPixelFormat _lastDecoderPixFmt = (AVPixelFormat)(-1);

        public event Action<byte[]>? OnEncodedFrame;
        public event Action<Exception>? OnEncodingError;

        public void Start()
        {
            if (_isRunning) return;

            lock (_disposeLock)
            {
                _isRunning = true;

                // 4. 初始化数据容器
                _decodedFrame = ffmpeg.av_frame_alloc();
                _encodedFrame = ffmpeg.av_frame_alloc();
                _inputPacket = ffmpeg.av_packet_alloc();
                _outputPacket = ffmpeg.av_packet_alloc();

                ffmpeg.avformat_network_init();

                // 1. 初始化输入设备
                SetupInputDevice();

                // 2. 初始化解码器
                SetupDecoder();

                // 3. 初始化编码器
                SetupEncoder();


                // 5. 像素格式转换器
                SetupPixelConverter();

                // 6. 启动编码线程
                _encodingTask = Task.Run(EncodingLoop);

                // 启动帧率监控
                _frameTimer.Restart();
            }
        }

        private void SetupInputDevice()
        {
            ffmpeg.avdevice_register_all();
            AVInputFormat* inputFormat = ffmpeg.av_find_input_format("gdigrab");
            AVFormatContext* ctx = null;

            // 添加帧率和分辨率控制
            AVDictionary* options = null;
            ffmpeg.av_dict_set(&options, "framerate", TargetFPS.ToString(), 0);
            ffmpeg.av_dict_set(&options, "video_size", "1920x1080", 0);
            ffmpeg.av_dict_set(&options, "draw_mouse", "1", 0);
            ffmpeg.av_dict_set(&options, "probesize", "32", 0);
            ffmpeg.av_dict_set(&options, "rtbufsize", "256M", 0);

            int ret = ffmpeg.avformat_open_input(&ctx, "desktop", inputFormat, &options);
            ffmpeg.av_dict_free(&options);

            ret.ThrowExceptionIfError();
            _inputFormatContext = ctx;
            ffmpeg.avformat_find_stream_info(_inputFormatContext, null).ThrowExceptionIfError();
        }

        private void SetupDecoder()
        {
            _videoStreamIndex = ffmpeg.av_find_best_stream(
                _inputFormatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, null, 0);

            if (_videoStreamIndex < 0)
                throw new InvalidOperationException("Could not find video stream in input");

            AVStream* stream = _inputFormatContext->streams[_videoStreamIndex];

            AVCodec* codec = ffmpeg.avcodec_find_decoder(stream->codecpar->codec_id);
            _videoDecoderContext = ffmpeg.avcodec_alloc_context3(codec);
            ffmpeg.avcodec_parameters_to_context(_videoDecoderContext, stream->codecpar);
            ffmpeg.avcodec_open2(_videoDecoderContext, codec, null).ThrowExceptionIfError();
        }

        private void SetupEncoder()
        {
            // 优先尝试硬件加速
            //AVCodec* codec = ffmpeg.avcodec_find_encoder_by_name("h264_nvenc");
            // 回退到软件编码
            //if (codec == null)
            AVCodec* codec = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_H264);

            _videoEncoderContext = ffmpeg.avcodec_alloc_context3(codec);

            // 基础参数设置
            _videoEncoderContext->width = _videoDecoderContext->width;
            _videoEncoderContext->height = _videoDecoderContext->height;
            _videoEncoderContext->pix_fmt = AVPixelFormat.AV_PIX_FMT_YUV420P;
            _videoEncoderContext->time_base = new AVRational { num = 1, den = 1 };
            _videoEncoderContext->framerate = new AVRational { num = TargetFPS, den = 1 };
            _videoEncoderContext->gop_size = TargetFPS * 2;
            _videoEncoderContext->max_b_frames = 1;

            // 硬件编码器特殊设置
            if (codec->name != null && Marshal.PtrToStringAnsi((IntPtr)codec->name).Contains("nvenc"))
            {
                _videoEncoderContext->bit_rate = 2_000_000;
                _videoEncoderContext->rc_min_rate = 0;
                _videoEncoderContext->rc_max_rate = 0;
                _videoEncoderContext->rc_buffer_size = 0;
                ffmpeg.av_opt_set(_videoEncoderContext->priv_data, "preset", "llhp", 0);
                ffmpeg.av_opt_set(_videoEncoderContext->priv_data, "tune", "ull", 0);
                ffmpeg.av_opt_set(_videoEncoderContext->priv_data, "rc", "vbr", 0);
            }
            else
            {
                _videoEncoderContext->bit_rate = 2_000_000;
                _videoEncoderContext->rc_min_rate = 1_500_000;
                _videoEncoderContext->rc_max_rate = 3_000_000;
                _videoEncoderContext->rc_buffer_size = 4_000_000;
                ffmpeg.av_opt_set(_videoEncoderContext->priv_data, "preset", "fast", 0);
                ffmpeg.av_opt_set(_videoEncoderContext->priv_data, "tune", "zerolatency", 0);
            }

            // 初始化编码器
            int ret = ffmpeg.avcodec_open2(_videoEncoderContext, codec, null);
            if (ret < 0)
            {
                string encoderName = codec->name != null ? Marshal.PtrToStringAnsi((IntPtr)codec->name) : "unknown";
                Console.WriteLine($"Failed to open encoder {encoderName}: {FfmpegHelper.GetErrorMessage(ret)}");
                throw new InvalidOperationException($"Failed to open encoder: {FfmpegHelper.GetErrorMessage(ret)}");
            }

            // 预分配编码帧缓冲区
            _encodedFrame->width = _videoEncoderContext->width;
            _encodedFrame->height = _videoEncoderContext->height;
            _encodedFrame->format = (int)_videoEncoderContext->pix_fmt;
            ffmpeg.av_frame_get_buffer(_encodedFrame, 32).ThrowExceptionIfError();
        }

        private void SetupPixelConverter()
        {
            // 释放现有转换器（防止内存泄漏）
            if (_swsContext != null)
            {
                ffmpeg.sws_freeContext(_swsContext);
                _swsContext = null;
            }

            // 检查是否需要转换
            if (_videoDecoderContext->pix_fmt == _videoEncoderContext->pix_fmt &&
                _videoDecoderContext->width == _videoEncoderContext->width &&
                _videoDecoderContext->height == _videoEncoderContext->height)
            {
                // 记录当前解码器参数
                _lastDecoderWidth = _videoDecoderContext->width;
                _lastDecoderHeight = _videoDecoderContext->height;
                _lastDecoderPixFmt = _videoDecoderContext->pix_fmt;
                return;
            }

            // 使用快速双线性算法
            _swsContext = ffmpeg.sws_getContext(
                _videoDecoderContext->width, _videoDecoderContext->height, _videoDecoderContext->pix_fmt,
                _videoEncoderContext->width, _videoEncoderContext->height, _videoEncoderContext->pix_fmt,
                ffmpeg.SWS_FAST_BILINEAR, null, null, null
            );

            if (_swsContext == null)
                throw new InvalidOperationException("Failed to initialize pixel converter");

            // 记录当前解码器参数
            _lastDecoderWidth = _videoDecoderContext->width;
            _lastDecoderHeight = _videoDecoderContext->height;
            _lastDecoderPixFmt = _videoDecoderContext->pix_fmt;
        }

        private void EncodingLoop()
        {
            try
            {
                while (_isRunning)
                {
                    ProcessFrame();
                }
                FlushEncoder();
            }
            catch (Exception ex)
            {
                OnEncodingError?.Invoke(ex);
            }
        }

        private void ProcessFrame()
        {
            int ret = ffmpeg.av_read_frame(_inputFormatContext, _inputPacket);
            if (ret == ffmpeg.AVERROR_EOF || ret == ffmpeg.AVERROR_EXIT) return;
            ret.ThrowExceptionIfError();

            // 校验是否为视频流
            if (_inputPacket->stream_index != _videoStreamIndex)
            {
                ffmpeg.av_packet_unref(_inputPacket);
                return;
            }

            ffmpeg.avcodec_send_packet(_videoDecoderContext, _inputPacket).ThrowExceptionIfError();
            ffmpeg.av_packet_unref(_inputPacket);

            while (ffmpeg.avcodec_receive_frame(_videoDecoderContext, _decodedFrame) >= 0)
            {
                // 检查解码器参数是否变化
                if (_videoDecoderContext->width != _lastDecoderWidth ||
                    _videoDecoderContext->height != _lastDecoderHeight ||
                    _videoDecoderContext->pix_fmt != _lastDecoderPixFmt)
                {
                    SetupPixelConverter();
                }

                // 像素格式转换（如果需要）
                if (_swsContext != null)
                {
                    ffmpeg.sws_scale(
                        _swsContext,
                        _decodedFrame->data, _decodedFrame->linesize, 0, _videoDecoderContext->height,
                        _encodedFrame->data, _encodedFrame->linesize
                    );
                }

                // 时间戳连续性保护
                long pts = _decodedFrame->best_effort_timestamp;
                if (_lastPts != -1)
                {
                    long expected = _lastPts + _videoDecoderContext->time_base.den / TargetFPS;
                    long diff = Math.Abs(pts - expected);

                    if (diff > 3 * _videoDecoderContext->time_base.den)
                    {
                        pts = expected;
                    }
                }
                _lastPts = pts;

                // 时间基转换
                long encoderPts = ffmpeg.av_rescale_q(
                    pts,
                    _videoDecoderContext->time_base,
                    _videoEncoderContext->time_base
                );

                // 设置帧的时间戳
                if (_swsContext != null)
                {
                    _encodedFrame->pts = encoderPts;
                }
                else
                {
                    _decodedFrame->pts = encoderPts;
                }

                // 发送帧进行编码
                AVFrame* frameToEncode = _swsContext != null ? _encodedFrame : _decodedFrame;
                ffmpeg.avcodec_send_frame(_videoEncoderContext, frameToEncode).ThrowExceptionIfError();

                // 获取编码包
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
                int size = (int)_outputPacket->size;
                if (size == 0) return;

                // 使用数组池优化内存分配
                byte[] buffer = _packetPool.Rent(size);
                Marshal.Copy((IntPtr)_outputPacket->data, buffer, 0, size);

                // 复制有效数据
                byte[] packetData = new byte[size];
                Buffer.BlockCopy(buffer, 0, packetData, 0, size);
                _packetPool.Return(buffer);

                // 触发帧率监控
                _frameCount++;
                if (_frameTimer.ElapsedMilliseconds > 1000)
                {
                    Console.WriteLine($"Encoding FPS: {_frameCount}");
                    _frameCount = 0;
                    _frameTimer.Restart();
                }

                // 触发编码帧事件
                OnEncodedFrame?.Invoke(packetData);
            }
            finally
            {
                ffmpeg.av_packet_unref(_outputPacket);
            }
        }

        private void FlushEncoder()
        {
            // 发送刷新信号
            int ret = ffmpeg.avcodec_send_frame(_videoEncoderContext, null);
            if (ret < 0 && ret != ffmpeg.AVERROR_EOF)
            {
                OnEncodingError?.Invoke(new InvalidOperationException(
                    $"Flush encoder error: {FfmpegHelper.GetErrorMessage(ret)}"));
                return;
            }

            // 接收剩余数据包
            while (ret >= 0)
            {
                ret = ffmpeg.avcodec_receive_packet(_videoEncoderContext, _outputPacket);
                if (ret >= 0) SendEncodedPacket();
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;

            lock (_disposeLock)
            {
                _isRunning = false;

                // 等待编码线程结束
                _encodingTask?.Wait(TimeSpan.FromSeconds(1));
                _encodingTask = null;

                // 安全释放所有资源
                ReleaseCodecContext(ref _videoEncoderContext);
                ReleaseCodecContext(ref _videoDecoderContext);
                ReleaseFormatContext();
                ReleasePacket(ref _outputPacket);
                ReleasePacket(ref _inputPacket);
                ReleaseFrame(ref _encodedFrame);
                ReleaseFrame(ref _decodedFrame);
                ReleaseSwsContext();
            }
        }

        private void ReleaseCodecContext(ref AVCodecContext* context)
        {
            if (context != null)
            {
                fixed (AVCodecContext** ptr = &context)
                {
                    ffmpeg.avcodec_free_context(ptr);
                }
                context = null;
            }
        }

        private void ReleaseFormatContext()
        {
            if (_inputFormatContext != null)
            {
                fixed (AVFormatContext** ptr = &_inputFormatContext)
                {
                    ffmpeg.avformat_close_input(ptr);
                }
                _inputFormatContext = null;
            }
        }

        private void ReleasePacket(ref AVPacket* packet)
        {
            if (packet != null)
            {
                fixed (AVPacket** ptr = &packet)
                {
                    ffmpeg.av_packet_free(ptr);
                }
                packet = null;
            }
        }

        private void ReleaseFrame(ref AVFrame* frame)
        {
            if (frame != null)
            {
                fixed (AVFrame** ptr = &frame)
                {
                    ffmpeg.av_frame_free(ptr);
                }
                frame = null;
            }
        }

        private void ReleaseSwsContext()
        {
            if (_swsContext != null)
            {
                ffmpeg.sws_freeContext(_swsContext);
                _swsContext = null;
            }
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }

        ~ScreenRecorder() => Dispose();
    }
}
