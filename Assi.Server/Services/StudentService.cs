using Assi.Server.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.Server.Services
{
    public class StudentService
    {
        public Dictionary<string,StudentCard> StudentCards { get; set; }

        public StudentService() 
        {
            StudentCards = new Dictionary<string, StudentCard>();
        }

        public void SetNewStudent(StudentCard scInfo)
        {
            StudentCards.Add(scInfo.Ip,scInfo);
        }
    }
}
