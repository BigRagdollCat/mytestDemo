using Assi.DotNetty.ChatTransmission;
using Assi.Server.ViewModels;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.Server.Services
{
    public class ChatService
    {
        public ConcurrentQueue<ChatInfoModel<object>> SystemChatInfoQue { get; set; } = new ConcurrentQueue<ChatInfoModel<object>>();
        public ConcurrentQueue<ChatInfoModel<object>> MessageChatinfoQue { get; set; } = new ConcurrentQueue<ChatInfoModel<object>>();

        private readonly EnhancedChatServer _enhancedChatServer;
        public ChatService(EnhancedChatServer enhancedChatServer)
        {
            _enhancedChatServer = enhancedChatServer;
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

                    break;
                case "_search_client":
                    SearchClientService.FindNewClient(cinfo);
                    break;
                default:
                    break;
            }
        }
    }
}
