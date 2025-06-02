using Assi.DotNetty.ChatTransmission;
using Assi.Server.ViewModels;
using SQLiteLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.Server.Services
{
    public static class SearchClientService 
    {
        static MainWindowViewModel viewModel { get; set; }
        static SearchClientService() 
        {
            viewModel = MainWindowViewModel.Instance;
        }

        public static void FindNewClient(ChatInfoModel<object> cinfo) 
        {
            try
            {
                App.Current._sqlite.StudentCards.Add(new StudentCardInfo()
                {
                    Ip = cinfo.Ip,
                    MAC = cinfo.Body.ToString()
                });
                App.Current._sqlite.SaveChanges();
                viewModel.Groups[0].StudentCards.Add(new StudentCard(cinfo.Body.ToString(), cinfo.Ip));
                viewModel.DisplayStudentCards.Clear();
                foreach (var item in viewModel.Groups[0].StudentCards)
                {
                    viewModel.DisplayStudentCards.Add(item);
                }
            }
            catch (Exception)
            {

            }
        }
    }
}
