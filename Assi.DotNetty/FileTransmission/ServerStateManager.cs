
using DotNetty.Transport.Channels;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Assi.DotNetty.FileTransmission
{
    public class ServerStateManager
    {
        private readonly ConcurrentDictionary<IChannel, ClientTransferState> _activeTransfers = new();
        private readonly Action _onAllTransfersCompleted;

        public ServerStateManager(Action onAllTransfersCompleted)
        {
            _onAllTransfersCompleted = onAllTransfersCompleted;
        }

        public void AddTransfer(IChannel channel, ClientTransferState state)
        {
            if (_activeTransfers.TryAdd(channel, state))
            {
                Console.WriteLine($"新增传输: {channel.Id} - {state.FileName}");
            }
        }

        public void RemoveTransfer(IChannel channel)
        {
            if (_activeTransfers.TryRemove(channel, out var state))
            {
                state.Dispose();
                Console.WriteLine($"移除传输: {channel.Id} - {state.FileName}");

                // 检查是否所有传输都已完成
                if (_activeTransfers.IsEmpty)
                {
                    _onAllTransfersCompleted?.Invoke();
                }
            }
        }

        // 新增方法：强制关闭所有传输
        public void ShutdownAllTransfers()
        {
            foreach (var (channel, state) in _activeTransfers.ToList())
            {
                state.Status = TransferStatus.Interrupted;
                state.Dispose();
                channel.CloseAsync();
            }
            _activeTransfers.Clear();
        }

        public ClientTransferState GetTransfer(IChannel channel)
        {
            if (!_activeTransfers.TryGetValue(channel, out var transfer)) 
            {
                throw new Exception("请求通道异常");
            };
            return transfer;
        }

        public bool HasActiveTransfers => !_activeTransfers.IsEmpty;
    }
}
