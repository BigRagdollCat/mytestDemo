using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Assi.DotNetty.UdpFileTransmission
{
    public static class DirectorySerializer
    {
        public static byte[] SerializeDirectoryStructure(string path)
        {
            var structure = new List<string>();
            AddDirectoryContents(structure, path, path);
            return Encoding.UTF8.GetBytes(string.Join("\n", structure));
        }

        private static void AddDirectoryContents(List<string> structure, string basePath, string currentPath)
        {
            foreach (var file in Directory.GetFiles(currentPath))
            {
                var relativePath = Path.GetRelativePath(basePath, file);
                var fileInfo = new FileInfo(file);
                structure.Add($"FILE|{relativePath}|{fileInfo.Length}");
            }

            foreach (var dir in Directory.GetDirectories(currentPath))
            {
                var relativePath = Path.GetRelativePath(basePath, dir);
                structure.Add($"DIR|{relativePath}");
                AddDirectoryContents(structure, basePath, dir);
            }
        }
    }
}
