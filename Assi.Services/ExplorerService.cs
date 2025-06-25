using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.Services
{
    public static class ExplorerService
    {
        public static List<ExplorerEntityInfo> GetExplorerEntity(string path)
        {
            List<ExplorerEntityInfo> Result = new List<ExplorerEntityInfo>();

            if (!Directory.Exists(path))
            {
                throw new Exception("目标路径不存在!");
            }

            try
            {
                // 获取所有子文件夹
                string[] directories = Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly);
                foreach (var dir in directories)
                {
                    DirectoryInfo dirInfo = new DirectoryInfo(dir);

                    Result.Add(new ExplorerEntityInfo()
                    {
                        Name = dirInfo.Name,
                        EntityType = ExplorerEntityType.Folder,
                        Address = dir,
                        FileExtension = string.Empty,
                        FileSize = 0,
                        Parent = path,
                    });
                }

                // 获取所有文件
                string[] files = Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    FileInfo fileInfo = new FileInfo(file);

                    Result.Add(new ExplorerEntityInfo()
                    {
                        Name = fileInfo.Name,
                        EntityType = ExplorerEntityType.File,
                        Address = file,
                        FileExtension = fileInfo.Extension,
                        FileSize = fileInfo.Length,
                        Parent = path,
                    });
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new Exception("没有访问该目录的权限。", ex);
            }
            catch (Exception ex)
            {
                throw new Exception("读取目录时发生错误。", ex);
            }

            return Result;
        }
    }

    public class ExplorerEntityInfo
    {
        public bool IsChecked { get; set; }
        public string Name { get; set; }
        public ExplorerEntityType EntityType { get; set; }
        public string FileExtension { get; set; }
        public string Address { get; set; }
        public string Parent { get; set; }
        public long FileSize { get; set; }
        public DateTime ChangeTime { get; set; }
    }

    public enum ExplorerEntityType 
    {
        File,
        Folder
    }
}
