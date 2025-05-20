using Assi.Server.ViewModels;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.Server.Models
{
    public partial class GroupInfo : ModelBase
    {
        [ObservableProperty]
        private int _index;
        [ObservableProperty]
        private string _name;

        public List<StudentCardInfo> StudentCards { get; set; }

        public GroupInfo(string name, int index = 0) 
        {
            this.Index = index;
            this.Name = name;
            StudentCards = new List<StudentCardInfo>(); 
        }
    }
}
