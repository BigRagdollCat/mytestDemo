using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteLibrary
{
    [Table(name: "StudentCardInfo")]
    public class StudentCardInfo
    {
        [Key]
        [Column(name: "MAC")]
        public string MAC { get; set; } 
        [Column(name:"Ip")]
        public string Ip { get; set; }
        [Column(name: "Index")]
        public int? Index{ get; set; }
        [Column(name: "Name")]
        public string? Name { get; set; }

        [NotMapped]
        public virtual ICollection<GroupStudent> Groups { get; set; }  // 导航属性
    }
}
