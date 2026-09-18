using ClassicUO.Network;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.Network;

public sealed class BrowserNetClientTests
{
    [Fact]
    public void BrowserTransportReportsConnectionState()
    {
        using var client = new AsyncNetClient();

        client.AttachBrowserTransport();
        client.IsConnected.Should().BeFalse();

        client.SetBrowserTransportConnected(true);
        client.IsConnected.Should().BeTrue();

        client.DetachBrowserTransport();
        client.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void BrowserTransportQueuesOutgoingPackets()
    {
        using var client = new AsyncNetClient();
        client.AttachBrowserTransport();
        client.SetBrowserTransportConnected(true);

        client.Send(new byte[] { 0x01, 0x02, 0x03 });

        client.TryDequeueBrowserOutgoing(out byte[] packet).Should().BeTrue();
        packet.Should().Equal(0x01, 0x02, 0x03);
        client.TryDequeueBrowserOutgoing(out _).Should().BeFalse();
    }

    [Fact]
    public void BrowserTransportFeedsIncomingMessagesThroughNormalQueue()
    {
        using var client = new AsyncNetClient();

        client.OnBrowserDataReceived(new byte[] { 0xA0, 0xB1 });

        client.TryDequeuePacket(out byte[] packet).Should().BeTrue();
        packet.Should().Equal(0xA0, 0xB1);
    }
}
