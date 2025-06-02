using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Assi.Student.Services
{
    public static class LockscreenService
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
    }
}
