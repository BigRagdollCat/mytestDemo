using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.DotNetty.UdpFileTransmission
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.Json;

    namespace Assi.DotNetty.UdpFileTransmission
    {
        public static class DirectorySerializer
        {
            // 构建目录树
            public static DirectoryNode BuildDirectoryTree(string path)
            {
                var node = new DirectoryNode
                {
                    Name = Path.GetFileName(path)
                };

                foreach (var dir in Directory.GetDirectories(path))
                {
                    node.Children.Add(BuildDirectoryTree(dir));
                }

                foreach (var file in Directory.GetFiles(path))
                {
                    node.Files.Add(new FileItem
                    {
                        Name = Path.GetFileName(file),
                        Size = new FileInfo(file).Length,
                        LocalPath = file
                    });
                }

                return node;
            }

            // 序列化目录结构并分片
            public static List<byte[]> SerializeDirectoryStructure(DirectoryNode root, int maxChunkSize = RUDPProtocol.MaxPayloadSize)
            {
                // 平铺目录结构
                var flatStructure = FlattenDirectory(root);
                var json = JsonSerializer.Serialize(flatStructure);
                var bytes = Encoding.UTF8.GetBytes(json);

                // 分块
                var chunks = new List<byte[]>();
                for (int i = 0; i < bytes.Length; i += maxChunkSize)
                {
                    int chunkSize = Math.Min(maxChunkSize, bytes.Length - i);
                    byte[] chunk = new byte[chunkSize];
                    Array.Copy(bytes, i, chunk, 0, chunkSize);
                    chunks.Add(chunk);
                }

                return chunks;
            }

            // 平铺目录结构
            private static List<object> FlattenDirectory(DirectoryNode node)
            {
                var result = new List<object>();

                // 添加当前节点信息
                result.Add(new
                {
                    Type = "DIRECTORY",
                    DirID = node.DirID,
                    Name = node.Name
                });

                // 添加当前目录的文件
                foreach (var file in node.Files)
                {
                    result.Add(new
                    {
                        Type = "FILE",
                        DirID = node.DirID,
                        Name = file.Name,
                        Size = file.Size
                    });
                }

                // 递归添加子目录
                foreach (var child in node.Children)
                {
                    result.AddRange(FlattenDirectory(child));
                }

                return result;
            }
        }
    }
}
