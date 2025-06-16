using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.Services
{
    public class OpenTerminalService
    {
        public void OpenTerminal()
        {
            try
            {
                var startInfo = new ProcessStartInfo();

                // 根据操作系统选择终端命令
                if (OperatingSystem.IsWindows())
                {
                    startInfo.FileName = "cmd.exe";
                    startInfo.Arguments = "/K echo Hello from Windows Terminal";
                }
                else if (OperatingSystem.IsLinux())
                {
                    startInfo.FileName = "xterm";
                    startInfo.Arguments = "-hold -e bash -c 'echo Hello from Linux Terminal; read'";
                }
                else if (OperatingSystem.IsMacOS())
                {
                    startInfo.FileName = "osascript";
                    startInfo.Arguments = "-e 'tell application \"Terminal\" to do script \"echo Hello from macOS Terminal; read\"'";
                }
                else
                {
                    // 默认方案（可选）
                    startInfo.FileName = "cmd.exe";
                    startInfo.Arguments = "/K echo Unsupported OS";
                }

                startInfo.UseShellExecute = true;
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {

            }
        }
    }
}
