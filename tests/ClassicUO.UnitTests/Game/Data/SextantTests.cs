using ClassicUO.Game.Data;
using Xunit;

namespace ClassicUO.UnitTests.Game.Data;

public class SextantTests
{
    [Theory]
    [InlineData("149° 9'S 21° 35'E", "9", "35")]
    [InlineData("149° 09'S 21° 35'E", "09", "35")]
    [InlineData("149° 9'S 21° 5'E", "9", "5")]
    [InlineData("149° 0'S 21° 0'E", "0", "0")]
    [InlineData("149o 9'S, 21o 35'E", "9", "35")]
    [InlineData("149o09'S,21o05'E", "09", "05")]
    [InlineData("149 9'S 21 35'E", "9", "35")]
    public void SextantMinutesAcceptPaddedAndUnpaddedInput(string text, string latitudeMinutes, string longitudeMinutes)
    {
        var match = Sextant.SextantCoordsRegex().Match(text);

        Assert.True(match.Success);
        Assert.Equal("149", match.Groups["LatDegrees"].Value);
        Assert.Equal(latitudeMinutes, match.Groups["LatMinutes"].Value);
        Assert.Equal("S", match.Groups["LatDirection"].Value);
        Assert.Equal("21", match.Groups["LongDegrees"].Value);
        Assert.Equal(longitudeMinutes, match.Groups["LongMinutes"].Value);
        Assert.Equal("E", match.Groups["LongDirection"].Value);
    }
}
