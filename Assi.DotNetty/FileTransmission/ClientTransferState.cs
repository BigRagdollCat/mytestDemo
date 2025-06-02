using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.DotNetty.FileTransmission
{
    public class ClientTransferState : IDisposable
    {
        /// <summary>
        /// 目标Mac地址
        /// </summary>
        public string Mac { get; set; }

        /// <summary>
        /// 目标IP地址
        /// </summary>
        public string Ip { get; set; }

        /// <summary>
        /// 目标端口号
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// 文件总大小（字节）
        /// </summary>
        public long TotalSize { get; set; }

        /// <summary>
        /// 当前传输偏移量（字节）
        /// </summary>
        public long CurrentOffset { get; set; }

        /// <summary>
        /// 文件流（用于读取或写入）
        /// </summary>
        public FileStream FileStream { get; set; }

        /// <summary>
        /// 线程同步锁
        /// </summary>
        public readonly object LockObject = new object();

        /// <summary>
        /// 传输开始时间
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 传输状态（上传中/下载中/已完成/已中断）
        /// </summary>
        public TransferStatus Status { get; set; } = TransferStatus.Pending;

        /// <summary>
        /// 传输速度（字节/秒）
        /// </summary>
        public double TransferSpeed { get; set; }

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdateTime { get; set; }

        /// <summary>
        /// 是否已释放资源
        /// </summary>
        private bool _disposed = false;

        /// <summary>
        /// 初始化传输状态
        /// </summary>
        public ClientTransferState(string fileName, long totalSize)
        {
            FileName = fileName;
            TotalSize = totalSize;
            CurrentOffset = 0;
            StartTime = DateTime.Now;
            LastUpdateTime = StartTime;
        }

        /// <summary>
        /// 更新偏移量（线程安全）
        /// </summary>
        public void UpdateOffset(long offset, int dataLength)
        {
            lock (LockObject)
            {
                if (offset < 0 || offset > TotalSize)
                    throw new ArgumentOutOfRangeException(nameof(offset), "偏移量超出文件范围");

                CurrentOffset = offset;

                // 计算传输速度
                var now = DateTime.Now;
                var timeSpan = now - LastUpdateTime;
                if (timeSpan.TotalSeconds > 0)
                {
                    TransferSpeed = dataLength / timeSpan.TotalSeconds;
                }
                LastUpdateTime = now;
            }
        }

        /// <summary>
        /// 检查偏移量是否有效
        /// </summary>
        public bool IsOffsetValid(long expectedOffset)
        {
            lock (LockObject)
            {
                return CurrentOffset == expectedOffset;
            }
        }

        /// <summary>
        /// 获取传输进度百分比
        /// </summary>
        public double GetProgressPercentage()
        {
            lock (LockObject)
            {
                return TotalSize > 0 ? (double)CurrentOffset / TotalSize * 100 : 0;
            }
        }

        /// <summary>
        /// 获取已用时间
        /// </summary>
        public TimeSpan GetElapsedTime()
        {
            return DateTime.Now - StartTime;
        }

        /// <summary>
        /// 获取预估剩余时间
        /// </summary>
        public TimeSpan GetEstimatedRemainingTime()
        {
            lock (LockObject)
            {
                if (TransferSpeed <= 0 || CurrentOffset >= TotalSize)
                    return TimeSpan.Zero;

                long remainingBytes = TotalSize - CurrentOffset;
                return TimeSpan.FromSeconds(remainingBytes / TransferSpeed);
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                Status = TransferStatus.Completed;
                FileStream?.Close();
                FileStream?.Dispose();
                FileStream = null;
                _disposed = true;
            }
        }
    }

    public enum TransferStatus
    {
        Pending,     // 等待开始
        Uploading,   // 上传中
        Downloading, // 下载中
        Completed,   // 已完成
        Interrupted  // 已中断
    }


    // 文件请求
    public class FileRequest
    {
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public FileAction Action { get; set; }
    }

    public enum FileAction
    {
        Upload,
        Download
    }

    // 文件分块
    public class FileChunk
    {
        public long Offset { get; set; }
        public byte[] Data { get; set; }
    }

    // 消息头（Header）
    public class FileMessageHeader
    {
        public MessageType Type { get; set; } // 消息类型：文件请求、数据块、确认等
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public long Offset { get; set; } // 当前偏移量
        public int DataLength { get; set; } // 数据长度
    }

    // 消息类型
    public enum MessageType
    {
        FileRequest,   // 文件请求（上传/下载）
        FileChunk,     // 数据块
        Ack,           // 确认
        Cancel,        // 取消传输
        Complete       // 传输完成
    }
}
