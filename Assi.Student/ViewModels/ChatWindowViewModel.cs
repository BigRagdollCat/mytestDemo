using Assi.Student.Models;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Assi.Student.ViewModels
{
    public partial class ChatWindowViewModel : ViewModelBase
    {
        public AvaloniaList<StudentCardInfo> StudentCardInfos { get; set; }

        [ObservableProperty]
        private StudentCardInfo _studentCardInfo;

        public ChatWindowViewModel()
        {
            StudentCardInfos = new AvaloniaList<StudentCardInfo>()
            {
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
                new StudentCardInfo("Test","192.168.9.110",8099),
            };
            StudentCardInfo = StudentCardInfos.First();
            SendMessageCommand = new RelayCommand(SendMessage);
        }

        #region SendCommand
        public ICommand SendMessageCommand { get; }
        private void SendMessage()
        {

        }
        #endregion
    }
}
