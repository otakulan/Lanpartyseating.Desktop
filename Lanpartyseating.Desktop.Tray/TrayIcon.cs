using Microsoft.Extensions.Hosting;

namespace Lanpartyseating.Desktop.Tray;

public class TrayIcon
{
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    public NotifyIcon _trayIcon { get; private set; }
    
    public TrayIcon(IHostApplicationLifetime hostApplicationLifetime)
    {
        _hostApplicationLifetime = hostApplicationLifetime;
    }

    /// <summary>
    /// Must be called from STAThread
    /// </summary>
    public void Initialize()
    {
        _trayIcon = new NotifyIcon
        {
            Icon = new Icon(typeof(Program), "trayicon.ico"),
            Visible = true,
            Text = "Lanparty Seating Desktop Client"
        };

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Exit", null, OnTrayIconExit!);
        _trayIcon.ContextMenuStrip = trayMenu;
    }
    
    public void UpdateText(string newText)
    {
        if (_trayIcon != null)
        {
            _trayIcon.Text = newText;
        }
    }
    
    private void OnTrayIconExit(object sender, EventArgs e)
    {
        _hostApplicationLifetime.StopApplication();
    }
}