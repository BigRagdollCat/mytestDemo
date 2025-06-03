using Assi.Services;
using Assi.DotNetty.ChatTransmission;
using Assi.Server.ViewModels;
using Assi.Server.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using Assi.Server.Services;
using Assi.DotNetty.FileTransmission;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using System.Threading;
using SQLiteLibrary;
using SQLitePCL;
using System.IO;
using Avalonia.Controls;

namespace Assi.Server
{
    public partial class App : Application
    {
        /// <summary>
        /// Gets the current <see cref="App"/> instance in use
        /// </summary>
        public new static App Current => (App)Application.Current;
        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> instance to resolve application services.
        /// </summary>
        public IServiceProvider Services { get; private set; }

        public SQLiteBase _sqlite { get; set; }

        public TopLevel MainTopLevel { get; private set; }


        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }
        public override void RegisterServices()
        {
            base.RegisterServices();
            // ע��ȫ�� ViewModel ����Դ��
            _sqlite = new SQLiteBase();
            _sqlite.Database.EnsureCreated();
            Services = ConfigureServices();
            Resources["MainWindowViewModel"] = MainWindowViewModel.Instance;
        }
        private static IServiceProvider ConfigureServices()
        {
            try
            {
                var services = new ServiceCollection();
                services.AddSingleton<EnhancedChatServer>(sp =>
                {
                    var port = 8099; // �ֶ�����˿ں�
                    return new EnhancedChatServer(port, Environment.ProcessorCount);
                });
                services.AddSingleton<ChatService>();
                services.AddSingleton<EnhancedFileClient>();
                // ע����������...
                services.AddHostedService<WorkBackgroundService>();
                services.AddSingleton<IMainWindowService,MainWindowService>();
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
                var mainWindows = new MainWindow
                {
                    DataContext = MainWindowViewModel.Instance,
                };
                var windowService = Services.GetRequiredService<IMainWindowService>() as MainWindowService;
                windowService?.Initialize(mainWindows);
                MainTopLevel = TopLevel.GetTopLevel(mainWindows);

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
                
                desktop.MainWindow = mainWindows;
            }

            base.OnFrameworkInitializationCompleted();
        }

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
    }
}