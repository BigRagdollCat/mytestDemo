using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Assi.Services
{
    public class KeyboardHook : IDisposable
    {
        // 原有结构体和API声明保持不变
        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private static IntPtr _hookId = IntPtr.Zero;

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc callback, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        // 新增：保持委托引用防止GC回收
        private LowLevelKeyboardProc _hookProc;

        public KeyboardHook()
        {
            // 初始化委托实例
            _hookProc = HookCallback;
        }

        // 安装钩子
        public void StartHook()
        {
            if (_hookId == IntPtr.Zero)
            {
                using (Process curProcess = Process.GetCurrentProcess())
                using (ProcessModule curModule = curProcess.MainModule)
                {
                    // 获取当前模块句柄
                    var moduleHandle = GetModuleHandle(curModule.ModuleName);
                    _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, moduleHandle, 0);
                }

                if (_hookId == IntPtr.Zero)
                {
                    throw new System.ComponentModel.Win32Exception();
                }
            }
        }

        // 卸载钩子
        public void StopHook()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        // 修改后的钩子回调方法
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {

            if (nCode >= 0)
            {
                var hookStruct = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));

                // 屏蔽 Alt+F4
                //if (hookStruct.vkCode == (int)Key.F4 && (hookStruct.flags & 0x20) != 0)
                //{
                //    return (IntPtr)1; // 阻止系统处理
                //}
                if (hookStruct.vkCode == 115 /* VK_F4 */ && (hookStruct.flags & 0x20) != 0 /* 检查Alt键 */)
                {
                    // 屏蔽 Alt+F4
                    return (IntPtr)1; // 阻止系统处理
                }
                // 屏蔽 Alt+Tab
                if (hookStruct.vkCode == 9 && (hookStruct.flags & 0x20) != 0)
                {
                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        // 实现IDisposable接口确保资源释放
        public void Dispose()
        {
            StopHook();
        }
    }
}
