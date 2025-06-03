using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.Student.ViewModels
{
    public partial class PlayerViewModel : ViewModelBase
    {
        private static readonly Lazy<PlayerViewModel> _instance = new(() => new PlayerViewModel());
        public static PlayerViewModel Instance => _instance.Value;

        [ObservableProperty]
        private WriteableBitmap _imageFromBinding;
    }
}
