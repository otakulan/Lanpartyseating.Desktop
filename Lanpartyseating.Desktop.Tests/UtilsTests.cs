using Lanpartyseating.Desktop.Business;

namespace Lanpartyseating.Desktop.Tests;

public class UtilsTests
{
    private readonly Utils _utils = new();

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
}