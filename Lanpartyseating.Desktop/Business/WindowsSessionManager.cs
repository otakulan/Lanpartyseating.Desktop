using System.Diagnostics;
using System.Runtime.InteropServices;
using Lanpartyseating.Desktop.Config;
using Microsoft.Extensions.Options;
using Microsoft.Win32;

namespace Lanpartyseating.Desktop.Business;

public class WindowsSessionManager : ISessionManager
{
    private readonly SeatingOptions _options;

    public WindowsSessionManager(IOptions<SeatingOptions> options)
    {
        _options = options.Value;
    }
    public void SignInGamerAccount()
    {
        LoginInteractiveSession(_options.GamerAccountUsername, _options.GamerAccountPassword);
    }

    public void SignInTournamentAccount()
    {
        LoginInteractiveSession(_options.TournamentAccountUsername, _options.TournamentAccountPassword);
    }

    public void SignOut()
    {
        LogoffInteractiveSession();
    }
    
    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSLogoffSession(IntPtr hServer, int sessionId, bool bWait);

    [DllImport("Kernel32.dll", SetLastError = true)]
    [return:MarshalAs(UnmanagedType.U4)]
    private static extern int WTSGetActiveConsoleSessionId();
    
    internal void LoginInteractiveSession(string username, string password)
    {
        var winlogonRegPath = @"Software\Microsoft\Windows NT\CurrentVersion\Winlogon";

        // Enable autologon
        Registry.SetValue($@"HKEY_LOCAL_MACHINE\{winlogonRegPath}", "AutoAdminLogon", 1, RegistryValueKind.DWord);

        // Don't autologon as soon as the session is logged out
        Registry.SetValue($@"HKEY_LOCAL_MACHINE\{winlogonRegPath}", "ForceAutoLogon", 0, RegistryValueKind.DWord);

        // Set autologon username
        Registry.SetValue($@"HKEY_LOCAL_MACHINE\{winlogonRegPath}", "DefaultUserName", username, RegistryValueKind.String);

        // Set autologon password
        Registry.SetValue($@"HKEY_LOCAL_MACHINE\{winlogonRegPath}", "DefaultPassword", password, RegistryValueKind.String);

        // Trigger autologon on next winlogon start
        Registry.LocalMachine.DeleteSubKeyTree($@"{winlogonRegPath}\AutoLogonChecked", false);
        
        // Kill winlogon
        var processes = Process.GetProcessesByName("winlogon");
        foreach (var process in processes)
        {
            process.Kill();
        }
    }
    
    internal void LogoffInteractiveSession()
    {
        var sessionId = WTSGetActiveConsoleSessionId();
        WTSLogoffSession(IntPtr.Zero, sessionId, false);
    }
}