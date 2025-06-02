using Assi.DotNetty.ChatTransmission;
using Assi.Services;
using Assi.Student.Models;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.Student.Services
{
    public static class SearchClientService
    {
        
        public static async Task ReplySearch(string ip,int port) 
        {
            string MACStr = LocalService.GetMac();
            await App.Current.Services.GetService<EnhancedChatServer>().SendMessageAsync(ip,port, new ChatInfoModel<string>()
            {
                MsgType = MsgType.System,
                Message = "_search_client",
                SendTimeSpan = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Body = MACStr
            });
        }
    }
}
