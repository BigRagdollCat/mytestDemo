using Assi.DotNetty.ChatTransmission;
using Assi.Student.Models;
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
                default:
                    break;
            }
        }
    }
}
