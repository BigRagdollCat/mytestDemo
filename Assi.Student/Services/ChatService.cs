using Assi.DotNetty.ChatTransmission;
using Assi.DotNetty.ScreenTransmission;
using Assi.Services;
using Assi.Student.Models;
using Assi.Student.ViewModels;
using Assi.Student.Views;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.Student.Services
{
    public class ChatService
    {
        public ConcurrentQueue<ChatInfoModel<object>> SystemChatInfoQue { get; set; } = new ConcurrentQueue<ChatInfoModel<object>>();
        public ConcurrentQueue<ChatInfoModel<object>> MessageChatinfoQue { get; set; } = new ConcurrentQueue<ChatInfoModel<object>>();
        public VoideService VoideService { get; set; }

        public ScreenRecorder recorder = new ScreenRecorder();

        public ChatService() 
        {
            recorder.OnEncodedFrame += async data =>
            {
                Console.WriteLine($"Received encoded frame, size: {data.Length} bytes");
                await App.Current.Services.GetService<VideoBroadcastServer>().BroadcastFrameAsync(data, 10089);
            };

            // 订阅错误事件
            recorder.OnEncodingError += ex =>
            {
                Console.WriteLine($"Encoding error: {ex.Message}");
            };
        }

        public async void ChatRun(ChatInfoModel<object> cinfo)
        {
            //if (cinfo.MsgType == MsgType.System)
            //{
            //    SystemChatInfoQue.Enqueue(cinfo);
            //}
            //else
            //{
            //    MessageChatinfoQue.Enqueue(cinfo);
            //}
            await WorkServer(cinfo);
        }

        public async Task WorkServer(ChatInfoModel<object> cinfo)
        {
            switch (cinfo.Message)
            {
                case "_close_desktop":
                    if (cinfo != null)
                    {
                        CloseDeskTop((bool)cinfo.Body);
                    }
                    break;
                case "_search_client":
                    await SearchClientService.ReplySearch(cinfo.Ip,cinfo.Port);
                    break;
                case "_close_client":
                    CloseClientService.CloseClient();
                    break;
                case "_file_upload":
                    FileClientService fcs = new FileClientService(cinfo.Ip);
                    fcs.Start(cinfo.Body.ToString());
                    break;
                case "_server_desktop":
                    if ((bool)cinfo.Body)
                    {
                        // 在 UI 线程上开始作业并立即返回。
                        Dispatcher.UIThread.Post(() => MainWindow.PlayerView.Show());
                    }
                    else
                    {
                        Dispatcher.UIThread.Post(() => MainWindow.PlayerView.IsVisible = false);
                    }
                    break;
                case "_client_desktop":
                    if ((bool)cinfo.Body)
                    {
                        recorder.Start(MainWindow.Width, MainWindow.Height);
                    }
                    else
                    {
                        recorder.Stop();
                        recorder.Dispose();
                    }
                    await App.Current.Services.GetService<EnhancedChatServer>().BroadcastAsync(new ChatInfoModel<bool>()
                    {
                        MsgType = MsgType.System,
                        Message = "_client_desktop",
                        SendTimeSpan = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Body = (bool)cinfo.Body
                    },8099);
                    break;
                default:
                    break;
            }
        }

        public void CloseDeskTop(bool body)
        {
            if (body)
            {
                LocalService.LockRun();
            }
            else
            {
                LocalService.UnLockRun();
            }
        }
    }
}
