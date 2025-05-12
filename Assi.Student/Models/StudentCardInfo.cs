using Assi.DotNetty.ChatTransmission;
using Assi.Student.Models;
using Avalonia.Collections;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Tmds.DBus.Protocol;

namespace Assi.Student.Models
{
    public partial class StudentCardInfo : ModelBase
    {
        // 这个属性将暴露给 XAML 绑定
        [ObservableProperty]
        private Bitmap? _thumbnailImage; // 属性名会自动生成为 ThumbnailImage
        [ObservableProperty]
        private int _index;
        [ObservableProperty]
        private string _name;
        [ObservableProperty]
        private string _ip;
        [ObservableProperty]
        private int _port;

        public AvaloniaList<ChatInfo> ChatInfoList { get; set; }

        public StudentCardInfo(string name,string ipStr, int port)
        {
            Name = name;
            Ip = ipStr;
            Port = port;
            ChatInfoList = new AvaloniaList<ChatInfo>() 
            {
                new ChatInfo(0,"Test1", ChatType.Send),
                new ChatInfo(1,"Test2", ChatType.Receive),
                new ChatInfo(2,"Test3", ChatType.Send),
                new ChatInfo(3,"Test4", ChatType.Receive),
                new ChatInfo(4,"Test5", ChatType.Send),
                new ChatInfo(5,"Test6", ChatType.Receive),
                new ChatInfo(6,"Test7", ChatType.Send),
                new ChatInfo(7,"Test8", ChatType.Receive),
            };    
        }

        #region 发送消息
        public async void Send(string message)
        {
            await App.Current.Services.GetService<EnhancedChatServer>().SendMessageAsync(Ip,Port,message);
        }
        #endregion
    }
}
