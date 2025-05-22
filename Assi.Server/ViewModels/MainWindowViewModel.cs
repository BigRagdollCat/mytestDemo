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

namespace Assi.Server.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        public AvaloniaList<StudentCard> DisplayStudentCards { get; }
        public AvaloniaList<Group> Groups { get; set; }

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
            #endregion

            #region 读取数据库数据
            Groups = new AvaloniaList<Group>();

            using (SQLiteBase sql = new SQLiteBase())
            {
                var group = sql.Groups.Select(grp => new Group(grp.Name,null)
                {
                    StudentCards = grp.Students.Select(std => new StudentCard(std.StudentIp)).ToList()
                }).ToList();

                foreach (var item in group)
                {
                    Groups.Add(item);
                };
                
                var students = sql.StudentCards.Select(sdc => new StudentCard(sdc.Ip)).ToList();
                for (int i = 0; i < students.Count; i++) 
                {
                    students[i].ItemIndex = i;
                }

                Groups.Insert(0, new Group("全部学生", students) { IsCheck = true });
            }
            #endregion
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
            if (imw.result)
            {
                AvaloniaList<object> alls = (AvaloniaList<object>)sCards;
                Group Group = new Group(imw.resultStr);
                foreach (var item in alls)
                {
                    Group.StudentCards.Add((StudentCard)item);
                }
                Groups.Add(Group);
            }
        }
        #endregion
    }
}
