
using Assi.Services;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Assi.Server.ViewModels
{
    public class AssiExplorerViewModel: ViewModelBase
    {
        public AvaloniaList<ExplorerEntityInfo> ExplorerEntityInfoList { get; }

        public AssiExplorerViewModel() 
        {
            ExplorerEntityInfoList = new AvaloniaList<ExplorerEntityInfo>();
            GetFolderListCommand = new RelayCommand(GetFolderList);

            this.ExplorerEntityInfoList.Add(new ExplorerEntityInfo());
            this.ExplorerEntityInfoList.Add(new ExplorerEntityInfo());
            this.ExplorerEntityInfoList.Add(new ExplorerEntityInfo());
        }

        

        #region SendCommand
        public ICommand GetFolderListCommand { get; }
        private void GetFolderList()
        {

        }
        #endregion
    }
}
