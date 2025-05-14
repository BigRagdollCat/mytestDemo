using Assi.DotNetty.ChatTransmission;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows.Input;

namespace Assi.Server.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        public IAvaloniaReadOnlyList<StudentCardInfo> StudentCardInfos { get; }


        public MainWindowViewModel()
        {
            TeacherDemonstrationCommand = new RelayCommand(TeacherDemonstration);
            StudentDemonstrationCommand = new RelayCommand(StudentDemonstration);
            RemoteScreenBlackoutCommand = new RelayCommand(RemoteScreenBlackout);
            DistributeCommand = new RelayCommand(Distribute);
            ReceiveFilesCommand = new RelayCommand(ReceiveFiles);
            RemoteCommand = new RelayCommand(ReceiveFiles);
            ClassOverCommand = new RelayCommand(ClassOver);
            RestoreFromBackupCommand = new RelayCommand(RestoreFromBackup);
            

            StudentCardInfos = new AvaloniaList<StudentCardInfo>()
            {
                new StudentCardInfo(){ ItemIndex = 1 },
                new StudentCardInfo(){ ItemIndex = 2 },
                new StudentCardInfo(){ ItemIndex = 3 },
                new StudentCardInfo(){ ItemIndex = 4 },
                new StudentCardInfo(){ ItemIndex = 5 },
                new StudentCardInfo(){ ItemIndex = 6 },
                new StudentCardInfo(){ ItemIndex = 7 },
                new StudentCardInfo(){ ItemIndex = 8 },
                new StudentCardInfo(){ ItemIndex = 9 },
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
        public double _ramNum;
        /// <summary>
        /// 设置Cpu占比
        /// </summary>
        public double SetCpu
        {
            set
            {
                CpuNum = value / 2;
            }
        }
        [ObservableProperty]
        public double _cpuNum;


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
    }
}
