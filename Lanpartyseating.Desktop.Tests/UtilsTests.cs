using Lanpartyseating.Desktop.Business;
using Lanpartyseating.Desktop.Config;
using Microsoft.Extensions.Options;

namespace Lanpartyseating.Desktop.Tests;

public class UtilsTests
{
    private readonly Utils _utils = new(Options.Create(new DebugOptions()));
    
    [Theory]
    [InlineData("LAN-GAMING-01", 1, true)]
    [InlineData("LAN-GAMING-01", 2, false)]
    [InlineData("LAN-GAMING-1", 1, true)]
    [InlineData("LAN-GAMING-70", 70, true)]
    public void Test_When_ForThisStationCalled_Then_MatchingStationNumberAndHostnameReturnTrue(string hostname, int stationNumber, bool expectedResult)
    {
        // Act
        var result = _utils.ForThisStation(stationNumber, hostname);
        
        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Theory]
    [InlineData("LAN-GAMING-01", 1, true)]
    [InlineData("LAN-GAMING-01", 2, true)]
    [InlineData("LAN-GAMING-1", 1, true)]
    [InlineData("LAN-GAMING-70", 70, true)]
    public void Test_When_ForThisStationCalled_And_ReactToAllStationsEnabled_Then_ReturnTrue(string hostname, int stationNumber, bool expectedResult)
    {
        // Create servicecollection with debug options true
        var utils = new Utils(Options.Create(new DebugOptions { ReactToAllStations = true }));
        
        // Act
        var result = utils.ForThisStation(stationNumber, hostname);
        
        // Assert
        Assert.Equal(expectedResult, result);
    }
    
    
}