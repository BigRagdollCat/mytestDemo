using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.Services
{
    public static class FFmpegHelper
    {
        public static void RegisterFFmpegBinaries()
        {
            //获取当前软件启动的位置
            var currentFolder = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
            //ffmpeg在项目中放置的位置
            var probe = Path.Combine("FFmpeg", "workbin", Environment.Is64BitOperatingSystem ? "x64" : "x86");
            while (currentFolder != null)
            {
                var ffmpegBinaryPath = Path.Combine(currentFolder, probe);
                if (Directory.Exists(ffmpegBinaryPath))
                {
                    //找到dll放置的目录，并赋值给rootPath;
                    ffmpeg.RootPath = ffmpegBinaryPath;
                    return;
                }
                currentFolder = Directory.GetParent(currentFolder)?.FullName;
            }
            //旧版本需要要调用这个方法来注册dll文件，新版本已经会自动注册了
            ffmpeg.avdevice_register_all();
        }

        public static TimeSpan ToTimeSpan(this long pts, AVRational timeBase)
        {
            return ((double)pts).ToTimeSpan(timeBase);
        }

        public static TimeSpan ToTimeSpan(this double pts, AVRational timeBase)
        {
            if (double.IsNaN(pts) || pts == long.MinValue)
                return TimeSpan.MinValue;

            if (timeBase.den == 0)
                return TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 1000 * pts / ffmpeg.AV_TIME_BASE)); //) .FromSeconds(pts / ffmpeg.AV_TIME_BASE);

            return TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 1000 * pts * timeBase.num / timeBase.den)); //pts * timeBase.num / timeBase.den);
        }

    }
}
