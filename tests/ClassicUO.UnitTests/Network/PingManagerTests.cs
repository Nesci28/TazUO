using System.Net;
using System.Threading.Tasks;
using ClassicUO.Network;
using Xunit;

namespace ClassicUO.UnitTests.Network;

public class PingManagerTests
{
    [Fact]
    public void LastPing_ReportsLatestSampleFromTheActiveSourceAndClearsOnReset()
    {
        uint ticks = 100;
        uint? tcpPing = null;
        byte sequence = 0;
        var manager = new PingManager(
            () => true,
            value => sequence = value,
            () => tcpPing,
            () => IPAddress.Any,
            () => ticks,
            _ => Task.FromResult<uint?>(null));

        Assert.Equal(0u, manager.LastPing);
        manager.SendPing();
        ticks = 140;
        Assert.True(manager.ProtocolPingReceived(sequence));

        ticks = 200;
        manager.SendPing();
        ticks = 260;
        Assert.True(manager.ProtocolPingReceived(sequence));
        Assert.Equal(50u, manager.Ping);
        Assert.Equal(60u, manager.LastPing);

        ticks = 6000;
        tcpPing = 20;
        manager.SendPing();
        Assert.Equal(20u, manager.Ping);
        Assert.Equal(20u, manager.LastPing);

        manager.Reset();
        Assert.Equal(0u, manager.LastPing);
    }
}
