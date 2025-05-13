using Assi.DotNetty.ChatTransmission;
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
        public ConcurrentQueue<ChatInfoModel> SystemChatInfoQue { get; set; } = new ConcurrentQueue<ChatInfoModel>();
        public ConcurrentQueue<ChatInfoModel> MessageChatinfoQue { get; set; } = new ConcurrentQueue<ChatInfoModel>();
        public void ChatRun(ChatInfoModel cinfo) 
        {
            if (cinfo.MsgType == MsgType.System)
            {
                SystemChatInfoQue.Enqueue(cinfo);
            }
            else
            {
                MessageChatinfoQue.Enqueue(cinfo);
            }
        }
    }
}
