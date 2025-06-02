using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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

        public static IPAddress GetLocalIp()
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces();
            IPAddress? broadcastAddress = null;
            foreach (var adapter in nics)
            {
                // 只选择已启用且为以太网/无线/Wi-Fi的适配器
                if (adapter.OperationalStatus == OperationalStatus.Up &&
                    (adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                     adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                {
                    var properties = adapter.GetIPProperties();
                    foreach (var address in properties.UnicastAddresses)
                    {
                        if (address.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            // 获取IP地址和子网掩码
                            IPAddress ip = address.Address;
                            IPAddress subnetMask = address.IPv4Mask;

                            // 计算广播地址
                            broadcastAddress = CalculateBroadcastAddress(ip, subnetMask);
                            break;
                        }
                    }
                }
            }
            return broadcastAddress;
        }

        /// <summary>
        /// 根据IP和子网掩码计算广播地址
        /// </summary>
        public static IPAddress CalculateBroadcastAddress(IPAddress ip, IPAddress subnetMask)
        {
            byte[] ipBytes = ip.GetAddressBytes();
            byte[] maskBytes = subnetMask.GetAddressBytes();
            byte[] broadcastBytes = new byte[ipBytes.Length];

            for (int i = 0; i < broadcastBytes.Length; i++)
            {
                broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
            }

            return new IPAddress(broadcastBytes);
        }

        public static string GetMac()
        {
            // 获取所有可用的网络接口
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up && // 正在运行的接口
                              nic.NetworkInterfaceType != NetworkInterfaceType.Loopback) // 排除环回接口
                .ToList();

            // 选择第一个符合条件的接口
            var validNic = interfaces.FirstOrDefault();
            if (validNic != null)
            {
                return validNic.GetPhysicalAddress().ToString();
            }

            return "MAC Address Not Found";
        }
    }
}
