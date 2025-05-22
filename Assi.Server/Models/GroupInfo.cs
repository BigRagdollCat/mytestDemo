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
    public partial class Group : ModelBase
    {
        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private bool _isCheck;

        public List<StudentCard> StudentCards { get; set; }

        public Group(string name,List<StudentCard>? studentCards = null) 
        {
            this.Name = name;
            StudentCards = studentCards == null ? new List<StudentCard>() : studentCards; 
        }
    }
}
