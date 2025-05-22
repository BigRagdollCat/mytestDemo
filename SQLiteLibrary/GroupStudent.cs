using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteLibrary
{
    [Table(name: "GroupStudent")]
    public class GroupStudent
    {
        public Guid GroupId { get; set; }

        public string StudentIp { get; set; }

        public virtual GroupInfo GroupInfo { get; set; }
        public virtual StudentCardInfo StudentCard { get; set; }
    }
}
