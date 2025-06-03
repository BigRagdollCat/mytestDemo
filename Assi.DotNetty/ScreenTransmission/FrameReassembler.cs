using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.DotNetty.ScreenTransmission
{
    public class FrameReassembler
    {
        private readonly Dictionary<int, List<(int FragmentId, byte[] Data)>> _frameBuffers = new();
        private readonly object _lock = new();

        public byte[] Reassemble(int frameId, int totalFragments, int fragmentId, byte[] data)
        {
            lock (_lock)
            {
                if (!_frameBuffers.TryGetValue(frameId, out var fragments))
                {
                    fragments = new List<(int, byte[])>();
                    _frameBuffers[frameId] = fragments;
                }

                fragments.Add((fragmentId, data));

                if (fragments.Count == totalFragments)
                {
                    fragments.Sort((a, b) => a.FragmentId.CompareTo(b.FragmentId));
                    int totalSize = fragments.Sum(x => x.Data.Length);
                    byte[] fullFrame = new byte[totalSize];
                    int offset = 0;

                    foreach (var (_, fragmentData) in fragments)
                    {
                        Buffer.BlockCopy(fragmentData, 0, fullFrame, offset, fragmentData.Length);
                        offset += fragmentData.Length;
                    }

                    _frameBuffers.Remove(frameId);
                    return fullFrame;
                }

                return null; // 尚未完整
            }
        }
    }
}
