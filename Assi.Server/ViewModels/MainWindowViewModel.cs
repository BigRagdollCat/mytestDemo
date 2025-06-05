using Assi.DotNetty.ChatTransmission;
using Assi.Server.Models;
using Assi.Server.Services;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Hosting;
using System.Linq;
using SQLiteLibrary;
using Microsoft.EntityFrameworkCore;
using Assi.Services;
using Avalonia.Platform.Storage;
using Avalonia.Controls;
using System.IO;
using System.Threading.Tasks;
using SkiaSharp;
using Assi.DotNetty.ScreenTransmission;
using Assi.Server.Views;

namespace Assi.Server.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private static readonly Lazy<MainWindowViewModel> _instance = new(() => new MainWindowViewModel());
        public static MainWindowViewModel Instance => _instance.Value;

        public AvaloniaList<StudentCard> DisplayStudentCards { get; }
        public AvaloniaList<Group> Groups { get; set; }

        private FileServer fileServer { get; set; }

        #region 分组选中项
        private Group? _selectedGrop;
        public Group? SelectedGrop
        {
            set
            {
                _selectedGrop = value;
                OnPropertyChanged(nameof(SelectedGrop));
                if (SelectedGrop != null) 
                {
                    DisplayStudentCards.Clear();
                    foreach (var item in SelectedGrop?.StudentCards)
                    {
                        DisplayStudentCards.Add(item);
                    }
                }
            }
            get
            {
                return _selectedGrop;
            }
        }
        #endregion


        public MainWindowViewModel()
        {
            #region Command
            TeacherDemonstrationCommand = new RelayCommand(TeacherDemonstration);
            StudentDemonstrationCommand = new RelayCommand(StudentDemonstration);
            RemoteScreenBlackoutCommand = new RelayCommand(RemoteScreenBlackout);
            DistributeCommand = new RelayCommand(Distribute);
            ReceiveFilesCommand = new RelayCommand(ReceiveFiles);
            RemoteCommand = new RelayCommand(ReceiveFiles);
            ClassOverCommand = new RelayCommand(ClassOver);
            RestoreFromBackupCommand = new RelayCommand(RestoreFromBackup);
            CreateGroupCommand = new RelayCommand<object>(CreateGroup);
            SearchClientCommand = new RelayCommand(SearchClient);   
            #endregion

            #region 读取数据库数据
            
            Groups = new AvaloniaList<Group>();

            using (SQLiteBase sql = new SQLiteBase())
            {
                //for (int i = 0; i < 10; i++)
                //{
                //    sql.StudentCards.Add(new StudentCardInfo() { Ip = $"192.168.9.{100 + i}",MAC = $"ABCDEF{i}",Name = $"测试{i}",Index = i });
                //}
                //sql.SaveChanges();
                var group = sql.Groups.Select(grp => new Group(grp.Name,null)
                {
                    StudentCards = grp.Students.Select(std => new StudentCard(std.StudentIp,std.StudentCard.MAC)).ToList()
                }).ToList();

                foreach (var item in group)
                {
                    Groups.Add(item);
                };
                
                var students = sql.StudentCards.Select(sdc => new StudentCard(sdc.Ip,sdc.MAC)).ToList();
                for (int i = 0; i < students.Count; i++) 
                {
                    students[i].ItemIndex = i;
                }

                Groups.Insert(0, new Group("全部学生", students) { IsCheck = true });
            }
            #endregion

            recorder.OnEncodedFrame += async data =>
            {
                Console.WriteLine($"Received encoded frame, size: {data.Length} bytes");
                await App.Current.Services.GetService<VideoBroadcastServer>().BroadcastFrameAsync(data, 10089);
            };

            // 订阅错误事件
            recorder.OnEncodingError += ex =>
            {
                Console.WriteLine($"Encoding error: {ex.Message}");
            };

            DisplayStudentCards = new AvaloniaList<StudentCard>();
            SelectedGrop = Groups.FirstOrDefault();
        }

        /// <summary>
        /// 设置Ram占比
        /// </summary>
        public double SetRam
        {
            set
            {
                RamNum = value / 2;
            }
        }
        [ObservableProperty]
        private double _ramNum;
        /// <summary>
        /// 设置Cpu占比
        /// </summary>
        private double SetCpu
        {
            set
            {
                CpuNum = value / 2;
            }
        }
        [ObservableProperty]
        private double _cpuNum;

        #region 教师演示
        public ScreenRecorder recorder = new ScreenRecorder();
        public ICommand TeacherDemonstrationCommand { get; }
        public bool IsRecorder { get; set; } = false;
        private async void TeacherDemonstration()
        {
            IsRecorder = !IsRecorder;
            await App.Current.Services.GetService<EnhancedChatServer>().BroadcastAsync(new ChatInfoModel<object>()
            {
                MsgType = MsgType.System,
                Message = "_server_desktop",
                SendTimeSpan = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Body = IsRecorder
            }, 8089);
            if (IsRecorder)
            {
                recorder.Start(MainWindow.Width, MainWindow.Height);
            }
            else
            {
                recorder.Stop();
                recorder.Dispose();
            }
        }
        #endregion

        #region 学生演示

        public bool IsStudentRecorder { get; set; } = false;
        public ICommand StudentDemonstrationCommand { get; }
        private async void StudentDemonstration()
        {
            IsStudentRecorder = !IsStudentRecorder;
            await App.Current.Services.GetService<EnhancedChatServer>().BroadcastAsync(new ChatInfoModel<object>()
            {
                MsgType = MsgType.System,
                Message = "_client_desktop",
                SendTimeSpan = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Body = IsStudentRecorder
            }, 8089);
        }
        #endregion

        #region 黑屏管控
        public bool IsCloseDesktop { get; set; } = false;
        public ICommand RemoteScreenBlackoutCommand { get; }
        private async void RemoteScreenBlackout()
        {
            IsCloseDesktop = !IsCloseDesktop;
            await App.Current.Services.GetService<EnhancedChatServer>().BroadcastAsync(new ChatInfoModel<object>()
            {
                MsgType = MsgType.System,
                Message = "_close_desktop",
                SendTimeSpan = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Body = IsCloseDesktop
            }, 8089);
        }
        #endregion

        #region 下发文件
        public ICommand DistributeCommand { get; }

        private async void Distribute()
        {
            if (fileServer != null && fileServer.IsRun == true)
            {
                await fileServer.Stop();
            }
            else
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

                    // 提取父目录和文件名
                    string parentDir = Path.GetDirectoryName(fullPath); // 父目录路径
                    string fileName = Path.GetFileName(fullPath);       // 文件名（如 "output.txt"）

                    fileServer = new FileServer(parentDir);

                    fileServer.Start();


                    await App.Current.Services.GetService<EnhancedChatServer>().BroadcastAsync(new ChatInfoModel<string>()
                    {
                        MsgType = MsgType.System,
                        Message = "_file_upload",
                        SendTimeSpan = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Body = fileName
                    }, 8089);
                }
            }
        }
        #endregion

        #region 收取文件
        public ICommand ReceiveFilesCommand { get; }
        public async void ReceiveFiles()
        {
            if (fileServer != null && fileServer.IsRun == true)
            {
                await fileServer.Stop();
            }
            else
            {
                string path = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory,"download");
                fileServer = new FileServer(path);
                fileServer.Start();
                await App.Current.Services.GetService<EnhancedChatServer>().BroadcastAsync(new ChatInfoModel<string>()
                {
                    MsgType = MsgType.System,
                    Message = "_file_download",
                    SendTimeSpan = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                }, 8089);
            }
        }
        #endregion

        #region 远程命令
        public ICommand RemoteCommand { get; }
        public void Remote()
        {

        }
        #endregion

        #region 下课
        public ICommand ClassOverCommand { get; }
        public async void ClassOver()
        {
            await App.Current.Services.GetService<EnhancedChatServer>().BroadcastAsync(new ChatInfoModel<object>()
            {
                MsgType = MsgType.System,
                Message = "_close_client",
                SendTimeSpan = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }, 8089);
        }
        #endregion

        #region 备份还原
        public ICommand RestoreFromBackupCommand { get; }
        public void RestoreFromBackup()
        {

        }
        #endregion

        #region 客户端搜索
        public ICommand SearchClientCommand { get; }
        public async void SearchClient()
        {
            await App.Current.Services.GetService<EnhancedChatServer>().BroadcastAsync(new ChatInfoModel<object>()
            {
                MsgType = MsgType.System,
                Message = "_search_client",
                SendTimeSpan = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }, 8089);
        }
        #endregion

        #region 添加群组
        public ICommand CreateGroupCommand { get; }
        public async void CreateGroup(object sCards)
        {
            InputMsgWindow imw = new InputMsgWindow();
            await imw.ShowDialog(App.Current.Services.GetRequiredService<IMainWindowService>().getMainWindow());
            if (imw.result)
            {
                AvaloniaList<object> alls = (AvaloniaList<object>)sCards;
                Group Group = new Group(imw.resultStr);
                foreach (var item in alls)
                {
                    Group.StudentCards.Add((StudentCard)item);
                }
                Groups.Add(Group);

                Guid groupId = Guid.NewGuid();
                await App.Current._sqlite.Groups.AddAsync(new GroupInfo()
                {
                    Id = groupId,
                    Name = imw.resultStr,
                    Students = Group.StudentCards.Select(sdc=> new GroupStudent() { GroupId = groupId, StudentIp = sdc.Ip }).ToList()
                });

                await App.Current._sqlite.SaveChangesAsync();
            }
        }
        #endregion
    }
}
