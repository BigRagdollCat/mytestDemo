using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.DotNetty.ChatTransmission
{
    public class ChatInfoModel<T>
    {
        /// <summary>
        /// 序列号
        /// </summary>
        public int Index { get; set; }  
        /// <summary>
        /// 消息类型
        /// </summary>
        public MsgType MsgType { get; set; }    
        /// <summary>
        /// 消息内容
        /// </summary>
        public string Message { get; set; }
        /// <summary>
        /// 携带的消息体
        /// </summary>
        public T Body { get; set; }
        /// <summary>
        /// 发送时间戳
        /// </summary>
        public long SendTimeSpan { get; set; }
        /// <summary>
        /// 发送消息的IP
        /// </summary>
        public string Ip { get; set; }
        /// <summary>
        /// 发送消息的端口号
        /// </summary>
        public int Port { get; set; }
    }

    public enum MsgType
    {
        System,
        Message
    }
}
