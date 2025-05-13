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
                    var port = 8099; // 手动传入端口号
                    return new EnhancedChatServer(port, Environment.ProcessorCount);
                });
                services.AddSingleton<EnhancedFileServer>(sp =>
                {
                    var port = 9099; // 手动传入端口号
                    return new EnhancedFileServer(port, Environment.ProcessorCount);
                });
                services.AddSingleton<ChatService>();
                services.AddSingleton<EnhancedFileClient>();
                // 注册其他服务...
                services.AddHostedService<WorkBackgroundService>();
                return services.BuildServiceProvider();
            }
            catch (Exception ex)
            {
                // 记录配置错误
                Console.Error.WriteLine($"服务配置失败: {ex.Message}");
                throw;
            }
        }

        public override void OnFrameworkInitializationCompleted()
        {


            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                #region 启动HostService
                Task.Run(() =>
                {
                    WorkBackgroundService backgroundService = (WorkBackgroundService)Services.GetRequiredService<IHostedService>();
                    backgroundService.OnChatInfo += Services.GetRequiredService<ChatService>().ChatRun;
                    // 获取并启动 HostedService
                    backgroundService.StartAsync(CancellationToken.None);

                    // 注册关闭时的清理逻辑
                    desktop.Exit += async (sender, args) =>
                    {
                        await backgroundService.StopAsync(CancellationToken.None);
                    };
                });
                #endregion

                // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                DisableAvaloniaDataAnnotationValidation();
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };
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