using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace Assi.Student.Models
{
    public partial class ChatInfo : ModelBase
    {
        [ObservableProperty]
        private int _chatIndex;
        [ObservableProperty]
        private string _chatMessage;
        [ObservableProperty]
        private ChatType _chatType;

        public ChatInfo(int chatIndex, string chatMessage, ChatType chatType)
        {
            ChatIndex = chatIndex;
            ChatMessage = chatMessage;
            ChatType = chatType;
        }
    }

    public enum ChatType
    {
        Send,
        Receive
    }
}
