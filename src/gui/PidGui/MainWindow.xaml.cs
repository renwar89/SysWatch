using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using PidGui.Components;
using MudBlazor.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using PidGui.Services;
using System.Threading;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace PidGui
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private static readonly Uri AppUri = new("http://127.0.0.1:5555/");
        private readonly TaskCompletionSource _serverReady = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public MainWindow()
        {
            this.InitializeComponent();
            StartServer();
            _ = NavigateWhenReadyAsync();
        }

        private void StartServer()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var contentRoot = AppContext.BaseDirectory;
                    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
                    {
                        ContentRootPath = contentRoot,
                        WebRootPath = Path.Combine(contentRoot, "wwwroot")
                    });

                    builder.Services.AddRazorComponents()
                        .AddInteractiveServerComponents();
                    builder.Services.AddMudServices();
                    builder.Services.AddSingleton<ProcessScannerService>();
                    builder.Services.AddSingleton<NetworkScannerService>();

                    builder.WebHost.UseUrls(AppUri.ToString());
                    var app = builder.Build();

                    app.UseStaticFiles(new StaticFileOptions
                    {
                        FileProvider = new PhysicalFileProvider(Path.Combine(contentRoot, "wwwroot"))
                    });

                    app.UseRouting();
                    app.UseAntiforgery();
                    app.MapRazorComponents<PidGui.Components.App>()
                        .AddInteractiveServerRenderMode();

                    app.Lifetime.ApplicationStarted.Register(() => _serverReady.TrySetResult());
                    await app.RunAsync();
                }
                catch (Exception ex)
                {
                    _serverReady.TrySetException(ex);
                    LogServerError(ex);
                }
            });
        }

        private async Task NavigateWhenReadyAsync()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await _serverReady.Task.WaitAsync(cts.Token);
            }
            catch
            {
                // If startup fails or times out, still attempt navigation to surface the error.
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                if (RootWebview.Source != AppUri)
                {
                    RootWebview.Source = AppUri;
                }
            });
        }

        private static void LogServerError(Exception ex)
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PidGui");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "server.log");
                File.AppendAllText(path, $"{DateTime.Now:O} {ex}\n");
            }
            catch
            {
                // Swallow logging failures.
            }
        }
    }
}
