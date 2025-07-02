using Assi.DotNetty.ChatTransmission;
using Assi.DotNetty.FileTransmission;
using Assi.DotNetty.UdpFileTransmission;
using Assi.Services;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assi.Student.Services
{
    public class FileClientService
    {
        UDPFileClient client { get; set; }
        public bool IsRun { get; set; } = false;

        public FileClientService(string Ip)
        {
            client = new UDPFileClient(Ip, 9090);
        }


        public async Task Start(string fileName)
        {
            //IsRun = true;
            //await client.(fileName, startOffset: 0); // 从头下载
            //await client.StartDownload();
            await client.StartAsync("Test1");
        }


        public async Task UploadStart()
        {
            var topLevel = TopLevel.GetTopLevel(App.Current.MainTopLevel);
            var storageProvider = topLevel.StorageProvider;

            // 异步的保存文件。
            var resultFile = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择上传的文件",
                AllowMultiple = false, // 是否允许多选
                FileTypeFilter = new List<FilePickerFileType> { FilePickerFileTypes.All } // 显示所有文件类型
            });
            if (resultFile != null && resultFile.Count() > 0)
            {
                string fullPath = resultFile[0].Path.LocalPath;
                IsRun = true;
                await client.UploadAsync(fullPath);
            }
        }

        public void Stop() 
        {
            client.Stop();
        }
    }
}
