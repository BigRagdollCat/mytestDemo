using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.DotNetty
{
    public  class OpenTerminalService
    {
        public static void OpenTerminal(string message)
        {
            // 处理 null 或空字符串的情况
            message = message ?? string.Empty;

            try
            {
                var startInfo = new ProcessStartInfo();
                string argumentsTemplate;

                if (OperatingSystem.IsWindows())
                {
                    // Windows: 转义双引号为两个双引号
                    string escapedMessage = message.Replace("\"", "\"\"");
                    argumentsTemplate = "/C echo \"{0}\" & pause";
                    startInfo.FileName = "cmd.exe";
                    startInfo.Arguments = string.Format(CultureInfo.InvariantCulture, argumentsTemplate, escapedMessage);
                }
                else if (OperatingSystem.IsLinux())
                {
                    // Linux: 转义单引号为 '\'
                    string escapedMessage = message.Replace("'", @"'\''");
                    argumentsTemplate = "-e bash -c 'echo \"{0}\"; read -p \"Press Enter to exit...\"'";
                    startInfo.FileName = "xterm";
                    startInfo.Arguments = string.Format(CultureInfo.InvariantCulture, argumentsTemplate, escapedMessage);
                }
                else if (OperatingSystem.IsMacOS())
                {
                    // macOS: 转义双引号和反斜杠
                    string escapedMessage = message.Replace("\\", "\\\\").Replace("\"", "\\\"");
                    argumentsTemplate = "-e 'tell application \"Terminal\" to do script \"echo \\\"{0}\\\"; read -p \\\"Press Enter to exit...\\\"; exit\"'";
                    startInfo.FileName = "osascript";
                    startInfo.Arguments = string.Format(CultureInfo.InvariantCulture, argumentsTemplate, escapedMessage);
                }
                else
                {
                    // 默认为 Windows 方式
                    string escapedMessage = message.Replace("\"", "\"\"");
                    argumentsTemplate = "/C echo \"{0}\" & pause";
                    startInfo.FileName = "cmd.exe";
                    startInfo.Arguments = string.Format(CultureInfo.InvariantCulture, argumentsTemplate, escapedMessage);
                }

                startInfo.UseShellExecute = true;
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 无法启动终端: {ex.Message}");
                // 可选：记录更多上下文信息，如当前操作系统、message 内容等
            }
        }
    }
}
