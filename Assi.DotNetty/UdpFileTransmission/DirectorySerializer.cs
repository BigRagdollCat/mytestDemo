using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Linq;

using System.Text;
using System.Text.Json;

namespace Assi.DotNetty.UdpFileTransmission
{
    public static class DirectorySerializer
    {
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
                    LocalPath = file,
                    DirID = node.DirID
                });
            }

            return node;
        }

        public static List<byte[]> SerializeDirectoryStructure(
            DirectoryNode root,
            int maxChunkSize = RUDPProtocol.ConservativePayloadSize)
        {
            var flatStructure = FlattenDirectory(root);
            var json = JsonSerializer.Serialize(flatStructure);
            var bytes = Encoding.UTF8.GetBytes(json);

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

        private static List<object> FlattenDirectory(DirectoryNode node)
        {
            var result = new List<object>();

            result.Add(new
            {
                Type = "DIRECTORY",
                DirID = node.DirID,
                Name = node.Name
            });

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

            foreach (var child in node.Children)
            {
                result.AddRange(FlattenDirectory(child));
            }

            return result;
        }
    }
}
