using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.Student.Services
{
    public static class CloseClientService
    {
        public static void CloseClient()
        {
            Console.WriteLine("正在尝试关闭计算机...");

            if (OperatingSystem.IsWindows())
            {
                // Windows 系统关机命令
                Process.Start("shutdown", "/s /t 0");
            }
            else if (OperatingSystem.IsLinux())
            {
                // Linux 系统关机命令（需要 root 权限）
                Process.Start("shutdown", "-h now");
            }
            else if (OperatingSystem.IsMacOS())
            {
                // macOS 系统关机命令（需要权限）
                Process.Start("osascript", "-e \"tell app \\\"System Events\\\" to shut down\"");
            }
            else
            {
                Console.WriteLine("不支持当前操作系统！");
            }
        }
    }
}
