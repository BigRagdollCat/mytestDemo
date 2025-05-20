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

namespace Assi.Server.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        public AvaloniaList<StudentCardInfo> DisplayStudentCardInfos { get; }
        public List<StudentCardInfo> StudentCardInfos { get; }

        public AvaloniaList<GroupInfo> GroupInfos { get; set; }

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
            #endregion


            StudentCardInfos = new List<StudentCardInfo>();

            DisplayStudentCardInfos = new AvaloniaList<StudentCardInfo>()
            {
                new StudentCardInfo(""){ ItemIndex = 1 },
                new StudentCardInfo(""){ ItemIndex = 2 },
                new StudentCardInfo(""){ ItemIndex = 3 },
                new StudentCardInfo(""){ ItemIndex = 4 },
                new StudentCardInfo(""){ ItemIndex = 5 },
                new StudentCardInfo(""){ ItemIndex = 6 },
                new StudentCardInfo(""){ ItemIndex = 7 },
                new StudentCardInfo(""){ ItemIndex = 8 },
                new StudentCardInfo(""){ ItemIndex = 9 },
            };
            GroupInfos = new AvaloniaList<GroupInfo>()
            {

            };
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
        public ICommand TeacherDemonstrationCommand { get; }
        private void TeacherDemonstration()
        {

        }
        #endregion

        #region 学生演示
        public ICommand StudentDemonstrationCommand { get; }
        private void StudentDemonstration()
        {

        }
        #endregion

        #region 黑屏管控
        public ICommand RemoteScreenBlackoutCommand { get; }
        private async void RemoteScreenBlackout()
        {

            await App.Current.Services.GetService<EnhancedChatServer>().SendMessageAsync("", 0, new ChatInfoModel()
            {
                MsgType = MsgType.System,
                Message = "_close_desktop",
                SendTimeSpan = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
        }
        #endregion

        #region 下发文件
        public ICommand DistributeCommand { get; }

        private void Distribute()
        {

        }
        #endregion

        #region 收取文件
        public ICommand ReceiveFilesCommand { get; }
        public void ReceiveFiles()
        {

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
        public void ClassOver()
        {

        }
        #endregion

        #region 备份还原
        public ICommand RestoreFromBackupCommand { get; }
        public void RestoreFromBackup()
        {

        }
        #endregion


        #region 添加群众
        public ICommand CreateGroupCommand { get; }
        public async void CreateGroup(object sCards)
        {
            InputMsgWindow imw = new InputMsgWindow();
            await imw.ShowDialog(App.Current.Services.GetRequiredService<IMainWindowService>().getMainWindow());
            AvaloniaList<object> alls = (AvaloniaList<object>)sCards;
            GroupInfo groupInfo = new GroupInfo("", GroupInfos.Count);
            foreach (var item in alls)
            {
                groupInfo.StudentCards.Add((StudentCardInfo)item);
            }
            GroupInfos.Add(groupInfo);
        }
        #endregion
    }
}
