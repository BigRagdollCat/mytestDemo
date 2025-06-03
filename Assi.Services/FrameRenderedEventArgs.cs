using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Assi.Services
{
    public class FrameRenderedEventArgs : EventArgs
    {
        public byte[] FrameData { get; }
        public int Width { get; }
        public int Height { get; }
        public int Stride { get; }

        public FrameRenderedEventArgs(byte[] frameData, int width, int height, int stride)
        {
            FrameData = frameData;
            Width = width;
            Height = height;
            Stride = stride;
        }
    }
}
