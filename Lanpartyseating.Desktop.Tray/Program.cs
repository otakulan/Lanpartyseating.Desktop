using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Lanpartyseating.Desktop.Tray;

static class Program
{
    [DllImport("shell32.dll", SetLastError = true)]
    static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);
    
    static async Task Main(string[] args)
    {
        // This code snippet uses the Shell32 API to create a shortcut and set the AppUserModelID
        var appUserModelId = "Otakuthon.Lanpartyseating.Desktop"; // Replace with your actual ID
        SetCurrentProcessExplicitAppUserModelID(appUserModelId);
        
        var hostBuilder = Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddSingleton<TrayIcon>();
                services.AddHostedService<TrayIconService>();
                services.AddHostedService<ToastNotificationService>();
            });
        await hostBuilder.RunConsoleAsync();;
    }
}