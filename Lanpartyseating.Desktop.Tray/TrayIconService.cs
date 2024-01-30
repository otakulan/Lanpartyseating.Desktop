using Microsoft.Extensions.Hosting;

namespace Lanpartyseating.Desktop.Tray;

public class TrayIconService : BackgroundService
{
    private readonly TrayIcon _trayIcon;

    public TrayIconService(TrayIcon trayIcon)
    {
        _trayIcon = trayIcon;
    }
    
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Create a dedicated STA thread for the tray icon
        var staThread = new Thread(() =>
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            _trayIcon.Initialize();
            using var trayIcon = _trayIcon._trayIcon;
            
            Application.Run();
        });

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();

        stoppingToken.Register(Application.Exit);

        return Task.CompletedTask;
    }
}