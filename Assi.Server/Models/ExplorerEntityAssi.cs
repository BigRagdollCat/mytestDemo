using Assi.Services;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Assi.Server.Models
{
    public class ExplorerEntityInfo : ModelBase
    {
        public string FullPath { get; set; }
        public string IconPath { get; set; }
        public bool IsChecked { get; set; }
        public string Name { get; set; } = string.Empty;
        public ExplorerEntityType EntityType { get; set; }
        public string FileExtension { get; set; } = string.Empty ;
        public string Address { get; set; } = string.Empty ;
        public string Parent { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime ChangeTime { get; set; }
        public string Type { get; set; }

        public ExplorerEntityInfo(ICommand DoubleClickCommand) 
        {
            ItemDoubleClickCommand = DoubleClickCommand;
            SubFolders = new AvaloniaList<ExplorerEntityInfo>();
        }

        #region 双击事件
        public ICommand ItemDoubleClickCommand { get; }
        #endregion

        public AvaloniaList<ExplorerEntityInfo> SubFolders { get; }
        
    }

    public enum ExplorerEntityType
    {
        File,
        Folder
    }
}
