using MsBox.Avalonia.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.Server.Services
{
    public class ShowMessageBoxEvent
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public ButtonEnum Buttons { get; set; }
    }
}
