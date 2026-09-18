using System;
using System.Buffers.Binary;
using System.IO;
using ClassicUO.Game.Managers;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.Game.Managers;

public class DualBoxManagerTests
{
    [Theory]
    [InlineData(100, 100, 110, 110, true)]
    [InlineData(100, 100, 90, 100, true)]
    [InlineData(100, 100, 111, 100, false)]
    [InlineData(100, 100, 100, 89, false)]
    public void SyncRangeUsesInclusiveTileDistance(int x1, int y1, int x2, int y2, bool expected)
    {
        DualBoxManager.IsWithinSyncRange(x1, y1, x2, y2).Should().Be(expected);
    }

    [Fact]
    public void ProtocolRoundTripsMovementAndGroupState()
    {
        var expected = new DualBoxMessage
        {
            Type = DualBoxMessageType.Step,
            Protocol = DualBoxManager.ProtocolVersion,
            ServerName = "Shard",
            MapIndex = 2,
            Serial = 0x01020304,
            X = 120,
            Y = 345,
            Z = -7,
            Direction = 6,
            Ready = true,
            Sequence = 42,
            StartX = 119,
            StartY = 345,
            StartZ = -6,
            StartDirection = 5,
            Run = true,
            Error = "none",
            GroupSerials = [0x01020304, 0x05060708]
        };

        DualBoxMessage actual = DualBoxProtocol.Decode(DualBoxProtocol.Encode(expected));

        actual.Type.Should().Be(expected.Type);
        actual.Protocol.Should().Be(expected.Protocol);
        actual.ServerName.Should().Be(expected.ServerName);
        actual.MapIndex.Should().Be(expected.MapIndex);
        actual.Serial.Should().Be(expected.Serial);
        actual.X.Should().Be(expected.X);
        actual.Y.Should().Be(expected.Y);
        actual.Z.Should().Be(expected.Z);
        actual.Direction.Should().Be(expected.Direction);
        actual.Ready.Should().BeTrue();
        actual.Sequence.Should().Be(expected.Sequence);
        actual.StartX.Should().Be(expected.StartX);
        actual.StartY.Should().Be(expected.StartY);
        actual.StartZ.Should().Be(expected.StartZ);
        actual.StartDirection.Should().Be(expected.StartDirection);
        actual.Run.Should().BeTrue();
        actual.Error.Should().Be(expected.Error);
        actual.GroupSerials.Should().Equal(expected.GroupSerials);
    }

    [Fact]
    public void ProtocolRejectsAFrameShorterThanItsLengthPrefix()
    {
        byte[] frame = new byte[sizeof(int) + 2];
        BinaryPrimitives.WriteInt32BigEndian(frame, 10);

        Action decode = () => DualBoxProtocol.Decode(frame);

        decode.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void ProtocolRejectsAnOversizedFrame()
    {
        byte[] frame = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(frame, DualBoxProtocol.MaxMessageSize + 1);

        Action decode = () => DualBoxProtocol.Decode(frame);

        decode.Should().Throw<InvalidDataException>();
    }
}
