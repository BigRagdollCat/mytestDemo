using Assi.DotNetty.ChatTransmission;
using Assi.Server.Models;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Assi.Server.ViewModels
{
    public partial class AssiExplorerViewModel: ViewModelBase
    {
        private StudentCard _student { get; set; }
        public AvaloniaList<ExplorerEntityInfo> DisplayExplorerEntitys { get; }
        public AvaloniaList<ExplorerEntityInfo> Folders { get; }

        public AssiExplorerViewModel(StudentCard student) 
        {
            _student = student;
            DisplayExplorerEntitys = new AvaloniaList<ExplorerEntityInfo>();
            Folders = new AvaloniaList<ExplorerEntityInfo>();
            LoadCommand = new RelayCommand(Load);
            GetFolderListCommand = new RelayCommand(GetFolderList);
            ItemDoubleClickCommand = new RelayCommand<ExplorerEntityInfo?>(OnItemDoubleClicked);

            DisplayExplorerEntitys.Add(new ExplorerEntityInfo(ItemDoubleClickCommand));
        }

        [ObservableProperty]
        private string _statusMessage;

        [ObservableProperty]
        private ExplorerEntityInfo _selectedItem;

        [ObservableProperty]
        private ExplorerEntityInfo _subFolders;

        [ObservableProperty]
        private string _currentPath;

        #region 双击事件
        public ICommand ItemDoubleClickCommand { get; }

        private async void OnItemDoubleClicked(ExplorerEntityInfo? entity)
        {
            if (entity == null) 
            {
                return;
            }
            else
            {
                await App.Current.Services.GetService<EnhancedChatServer>().SendMessageAsync(_student.Ip, 8089, new ChatInfoModel<string>()
                {
                    MsgType = MsgType.System,
                    Message = "_file_tree",
                    SendTimeSpan = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Body = Path.Combine(entity.Parent, $"{entity.Name}.{entity.FileExtension}"),
                });
            }
        }
        #endregion


        #region SendCommand
        public ICommand GetFolderListCommand { get; }
        private void GetFolderList()
        {

        }
        #endregion

        #region LoadCommand
        public ICommand LoadCommand { get; }
        private async void Load() 
        {
            await App.Current.Services.GetService<EnhancedChatServer>().SendMessageAsync(_student.Ip, 8089, new ChatInfoModel<string>()
            {
                MsgType = MsgType.System,
                Message = "_file_tree",
                SendTimeSpan = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Body = "first",
            });
        }
        #endregion
    }
}
