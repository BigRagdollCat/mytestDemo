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
        public AvaloniaList<StudentCard> StudentCards { get; set; }

        [ObservableProperty]
        private StudentCard _StudentCard;

        public ChatWindowViewModel()
        {
            StudentCards = new AvaloniaList<StudentCard>()
            {
            };
            StudentCard = StudentCards.First();
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
