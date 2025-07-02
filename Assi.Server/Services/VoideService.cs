using Assi.DotNetty.ChatTransmission;
using Assi.DotNetty.ScreenTransmission;
using Assi.Server.ViewModels;
using Assi.Services;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.Server.Services
{
    public class AssiVoideService
    {
        private readonly VideoBroadcastServer _videoBroadcastServer;

        private VideoPlayer player = new VideoPlayer();

        public AssiVoideService(VideoBroadcastServer videoBroadcastServer)
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
