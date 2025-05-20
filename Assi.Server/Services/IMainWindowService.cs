using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.Server.Services
{
    // IMainWindowService.cs
    public interface IMainWindowService
    {
        Window? getMainWindow();
        void Close();
    }

    // MainWindowService.cs
    public class MainWindowService : IMainWindowService
    {
        private Window _mainWindow;

        public void Initialize(Window window) => _mainWindow = window;
        public void Close() => _mainWindow?.Close();

        public Window getMainWindow()
        {
            return _mainWindow;
        }
    }
}
