using Assi.DotNetty.ChatTransmission;
using Assi.Student.ViewModels;
using Assi.Student.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Assi.Services;
using Assi.Student.Services;

namespace Assi.Student
{
    public partial class App : Application
    {
        /// <summary>
        /// Gets the current <see cref="App"/> instance in use
        /// </summary>
        public new static App Current => (App)Application.Current;
        private TrayIcon _trayIcon;
        private DispatcherTimer _flashTimer;
        private bool _isFlashing = false;
        private Bitmap _iconNormal = LocalService.LoadFromResource(new Uri("avares://Assi.Student/Resources/assiLogo.png"));
        private Bitmap _iconAlternate = LocalService.LoadFromResource(new Uri("avares://Assi.Student/Resources/NotiyNull.png"));
        private int _iconIndex = 0;

        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> instance to resolve application services.
        /// </summary>
        public IServiceProvider Services { get; private set; }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }
        public override void RegisterServices()
        {
            base.RegisterServices();
            Services = ConfigureServices();
        }
        private static IServiceProvider ConfigureServices()
        {
            try
            {
                var services = new ServiceCollection();
                services.AddSingleton<EnhancedChatServer>(sp =>
                {
                    var port = 8089; // �ֶ�����˿ں�
                    return new EnhancedChatServer(port, Environment.ProcessorCount);
                });
                services.AddSingleton<ChatService>();
                // ע����������...
                services.AddHostedService<WorkBackgroundService>();
                return services.BuildServiceProvider();
            }
            catch (Exception ex)
            {
                // ��¼���ô���
                Console.Error.WriteLine($"��������ʧ��: {ex.Message}");
                throw;
            }
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                #region ͼ�깦������

                // ��ȡ����ͼ��
                _trayIcon = TrayIcon.GetIcons(this)[0];
                _trayIcon.Icon = new WindowIcon(_iconNormal);

                // ��ʼ����ʱ��
                _flashTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                _flashTimer.Tick += FlashTimer_Tick;

                #endregion

                #region ����HostService

                Task.Run(() =>
                {
                    WorkBackgroundService backgroundService = (WorkBackgroundService)Services.GetRequiredService<IHostedService>();
                    backgroundService.OnChatInfo += Services.GetRequiredService<ChatService>().ChatRun;
                    // ��ȡ������ HostedService
                    backgroundService.StartAsync(CancellationToken.None);

                    // ע��ر�ʱ�������߼�
                    desktop.Exit += async (sender, args) =>
                    {
                        await backgroundService.StopAsync(CancellationToken.None);
                    };
                });

                #endregion

                // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                DisableAvaloniaDataAnnotationValidation();
                //desktop.MainWindow = new MainWindow
                //{
                //    DataContext = new MainWindowViewModel(),
                //};
                desktop.MainWindow = new ChatViewWindow
                {
                    DataContext = new ChatWindowViewModel()
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        #region ͼ����˸����
        private void FlashTimer_Tick(object? sender, EventArgs e)
        {
            _flashTimer?.Stop();
            if (_isFlashing)
            {
                _trayIcon.Icon = new WindowIcon(_iconIndex == 0 ? _iconAlternate : _iconNormal);
                _flashTimer?.Start();
            }
        }

        public void StartFlashing()
        {
            if (!_isFlashing)
            {
                _isFlashing = true;
                _flashTimer.Start();
            }
        }

        public void StopFlashing()
        {
            _isFlashing = false;
            _trayIcon.Icon = new WindowIcon(_iconNormal);
        }
        #endregion

        private void DisableAvaloniaDataAnnotationValidation()
        {
            // Get an array of plugins to remove
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            // remove each entry found
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }

        private void NativeMenuItem_Click(object? sender, System.EventArgs e)
        {
            System.Environment.Exit(0);
        }
    }
}