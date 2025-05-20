using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.Server.ViewModels
{
    public partial class StudentCardInfo : ViewModelBase
    {
        // 这个属性将暴露给 XAML 绑定
        [ObservableProperty]
        private Bitmap? _thumbnailImage; // 属性名会自动生成为 ThumbnailImage
        [ObservableProperty]
        private int _itemIndex;
        [ObservableProperty]
        private string _ip;

        public StudentCardInfo(string ip)
        {
            Ip = ip;
        }
    }
}
