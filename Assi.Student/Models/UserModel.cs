using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Assi.Student.Models
{
    public partial class UserModel : ModelBase
    {
        [ObservableProperty]
        public Bitmap _avatar;
        [ObservableProperty]
        public string _name;

        public UserModel()
        {
            //Avatar = LocalService.LoadFromResource(new Uri("avares://Assi.Student/Resources/NotiyNull.png"));

            Name = "Test";
        }
    }
}
