using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Assi.Services
{
    public static class LocalService
    {

        #region 屏蔽管理器
        private const int PROCESS_SUSPEND_RESUME = 0x0800;

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtSuspendProcess(IntPtr processHandle);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtResumeProcess(IntPtr processHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        public static void SuspendWinLogon()
        {
            Process[] processes = Process.GetProcessesByName("winlogon");
            if (processes.Length == 0) return;

            IntPtr hProcess = OpenProcess(PROCESS_SUSPEND_RESUME, false, processes[0].Id);
            if (hProcess != IntPtr.Zero)
            {
                NtSuspendProcess(hProcess);
                CloseHandle(hProcess);
            }
        }

        public static void ResumeWinLogon()
        {
            Process[] processes = Process.GetProcessesByName("winlogon");
            if (processes.Length == 0) return;

            IntPtr hProcess = OpenProcess(PROCESS_SUSPEND_RESUME, false, processes[0].Id);
            if (hProcess != IntPtr.Zero)
            {
                NtResumeProcess(hProcess);
                CloseHandle(hProcess);
            }
        }
        #endregion


        public static Bitmap LoadFromResource(Uri resourceUri)
        {
            return new Bitmap(AssetLoader.Open(resourceUri));
        }

        public static async Task<Bitmap?> LoadFromWeb(Uri url)
        {
            using var httpClient = new HttpClient();
            try
            {
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var data = await response.Content.ReadAsByteArrayAsync();
                return new Bitmap(new MemoryStream(data));
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"An error occurred while downloading image '{url}' : {ex.Message}");
                return null;
            }
        }
    }
}
