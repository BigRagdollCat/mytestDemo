using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteLibrary
{

    public class SQLiteBase : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // 获取数据库文件路径
            string databasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database.db");

            // 配置SQLite数据库连接
            optionsBuilder.UseSqlite($@"Data Source={databasePath};Foreign Keys=True;");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 配置 GroupStudent 的复合主键（GroupId + StudentIp）
            modelBuilder.Entity<GroupStudent>()
                .HasKey(gs => new { gs.GroupId, gs.StudentIp });  // 显式定义复合主键 [[1]]

            // 配置 GroupInfo 与 GroupStudent 的一对多关系
            modelBuilder.Entity<GroupInfo>()
                .HasMany(g => g.Students)  // 假设 GroupInfo 中有 public ICollection<GroupStudent> Students { get; set; }
                .WithOne(s => s.GroupInfo)
                .HasForeignKey(s => s.GroupId);

            // 配置 StudentCard 与 GroupStudent 的一对多关系
            modelBuilder.Entity<StudentCardInfo>()
                .HasMany(sc => sc.Groups)  // 假设 StudentCard 中有 public ICollection<GroupStudent> Groups { get; set; }
                .WithOne(gs => gs.StudentCard)
                .HasForeignKey(gs => gs.StudentIp);
        }

        public DbSet<GroupInfo> Groups { get; set; }
        public DbSet<StudentCardInfo> StudentCards { get; set; }
        public DbSet<GroupStudent> GroupStudents { get; set; }
    }
}
