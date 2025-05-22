using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteLibrary
{
    [Table(name: "GroupInfo")]
    public class GroupInfo
    {
        [Key]
        [Column(name:"Id")]
        public Guid Id { get; set; }
        [Column(name: "Name")]
        public string Name { get; set; }

        [NotMapped]
        public virtual ICollection<GroupStudent> Students { get; set; }  // 导航属性
    }
}
