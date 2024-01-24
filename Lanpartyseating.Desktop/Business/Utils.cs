using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Lanpartyseating.Desktop.Config;
using Microsoft.Extensions.Options;
using Microsoft.Win32;

namespace Lanpartyseating.Desktop.Business;

public class Utils
{
    private readonly IOptions<DebugOptions> _debugOptions;

    public Utils(IOptions<DebugOptions> debugOptions)
    {
        _debugOptions = debugOptions;
    }
    
    
    public bool ForThisStation(int stationNumber, string hostname)
    {
        if (_debugOptions.Value.ReactToAllStations)
        {
            return true;
        }
        
        var regex = new Regex(@"^LAN-GAMING-(\d+)$");
        var match = regex.Match(hostname);
        if (!match.Success)
        {
            return false;
        }
        var stationNumberFromHostname = Convert.ToInt32(match.Groups[1].Value);
        return stationNumberFromHostname == stationNumber;
    }
}