using Assi.DotNetty.ScreenTransmission;
using Assi.Services;
using Assi.Student.ViewModels;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia;
using Avalonia.Threading;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;
using PixelFormat = Avalonia.Platform.PixelFormat;

namespace Assi.Student.Services
{
    public class VoideService
    {
        private readonly VideoBroadcastServer _videoBroadcastServer;

        private VideoPlayer player = new VideoPlayer();

        public VoideService(VideoBroadcastServer videoBroadcastServer)
        {
            _videoBroadcastServer = videoBroadcastServer;
            player.OnRenderFrame += e =>
            {
                // 确保在UI线程执行
                Dispatcher.UIThread.Post(() =>
                {
                    PlayerViewModel.Instance.ImageFromBinding = new WriteableBitmap(
                            new PixelSize(e.Width, e.Height),
                            new Vector(96, 96),
                            PixelFormat.Bgra8888,
                            AlphaFormat.Opaque
                        );

                    // 锁定位图进行写入
                    using (var buffer = PlayerViewModel.Instance.ImageFromBinding.Lock())
                    {
                        var handle = buffer.Address;
                        int destStride = buffer.RowBytes;

                        // 将BGR24转换为BGRA32
                        ConvertBgrToBgra(
                            e.FrameData,
                            e.Stride,
                            handle,
                            destStride,
                            e.Width,
                            e.Height
                        );
                    }
                    
                }, DispatcherPriority.Render);
            };
            player.Initialize();
        }

        public async void VoideRun(byte[] datas)
        {
            await WorkServer(datas);
        }


        public async Task WorkServer(byte[] datas)
        {

            player.SubmitPacket(datas);
        }

        private unsafe void ConvertBgrToBgra(
        byte[] src, int srcStride,
        IntPtr dest, int destStride,
        int width, int height)
        {
            byte* destPtr = (byte*)dest.ToPointer();
            fixed (byte* srcPtr = src)
            {
                for (int y = 0; y < height; y++)
                {
                    byte* srcRow = srcPtr + y * srcStride;
                    byte* destRow = destPtr + y * destStride;

                    for (int x = 0; x < width; x++)
                    {
                        // 复制BGR通道并添加Alpha
                        *destRow++ = *srcRow++; // B
                        *destRow++ = *srcRow++; // G
                        *destRow++ = *srcRow++; // R
                        *destRow++ = 0xFF;      // A (不透明)
                    }
                }
            }
        }
    }
}
