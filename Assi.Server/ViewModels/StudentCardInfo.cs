using Assi.Server.Services;
using Avalonia.Collections;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SQLiteLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Assi.Server.ViewModels
{
    public partial class StudentCard : ViewModelBase
    {
        // 这个属性将暴露给 XAML 绑定
        [ObservableProperty]
        private Bitmap? _thumbnailImage; // 属性名会自动生成为 ThumbnailImage
        [ObservableProperty]
        private int _itemIndex;
        [ObservableProperty]
        private string _ip;
        [ObservableProperty]
        private string _mac;

        public StudentCard(string mac,string ip)
        {
            Mac = mac;
            Ip = ip;
            ToggleMenuCommand = new RelayCommand(ToggleMenu);
            Option1Command = new RelayCommand<StudentCard>(Option1);
            Option2Command = new RelayCommand(Option2);
        }


        #region 
        private bool _isMenuOpen = false;
        public bool IsMenuOpen
        {
            get => _isMenuOpen;
            set
            {
                _isMenuOpen = value;
                OnPropertyChanged();
            }
        }
        public ICommand ToggleMenuCommand { get; }
        public async void ToggleMenu()
        {
            IsMenuOpen = !IsMenuOpen;
        }
        #endregion

        #region 
        public ICommand Option1Command { get; }
        public async void Option1(StudentCard student)
        {
            AssiExplorer ae = new AssiExplorer(student);
            await ae.ShowDialog(App.Current.Services.GetRequiredService<IMainWindowService>().getMainWindow());
        }
        #endregion
        #region 
        public ICommand Option2Command { get; }
        public async void Option2()
        {

        }
        #endregion
        
    }
}
