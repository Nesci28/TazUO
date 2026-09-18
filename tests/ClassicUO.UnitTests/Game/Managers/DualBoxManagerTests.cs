using System;
using System.Buffers.Binary;
using System.IO;
using ClassicUO.Common.Enums;
using ClassicUO.Game.Managers;
using ClassicUO.UnitTests.Fixtures;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.Game.Managers;

[Collection(MainThreadCollection.Name)]
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
            Mounted = true,
            MountSequence = 12,
            MacroSequence = 9,
            MacroDefinition = "<macro name=\"Heal\" />",
            TargetKind = DualBoxTargetKind.Entity,
            TargetSerial = 0x0A0B0C0D,
            TargetGraphic = 0x1234,
            TargetX = 222,
            TargetY = 333,
            TargetZ = -4,
            StartX = 119,
            StartY = 345,
            StartZ = -6,
            StartDirection = 5,
            Run = true,
            ActionSequence = 14,
            WarMode = true,
            AttackSerial = 0x01010101,
            GumpSequence = 15,
            GumpServerSerial = 0x10203040,
            GumpButton = 7,
            GumpSwitches = [3, 5],
            GumpEntries = [new DualBoxGumpEntry { Index = 2, Text = "hello" }],
            Command = "{\"action\":\"heal\"}",
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
        actual.Mounted.Should().BeTrue();
        actual.MountSequence.Should().Be(expected.MountSequence);
        actual.MacroSequence.Should().Be(expected.MacroSequence);
        actual.MacroDefinition.Should().Be(expected.MacroDefinition);
        actual.TargetKind.Should().Be(expected.TargetKind);
        actual.TargetSerial.Should().Be(expected.TargetSerial);
        actual.TargetGraphic.Should().Be(expected.TargetGraphic);
        actual.TargetX.Should().Be(expected.TargetX);
        actual.TargetY.Should().Be(expected.TargetY);
        actual.TargetZ.Should().Be(expected.TargetZ);
        actual.StartX.Should().Be(expected.StartX);
        actual.StartY.Should().Be(expected.StartY);
        actual.StartZ.Should().Be(expected.StartZ);
        actual.StartDirection.Should().Be(expected.StartDirection);
        actual.Run.Should().BeTrue();
        actual.ActionSequence.Should().Be(expected.ActionSequence);
        actual.WarMode.Should().BeTrue();
        actual.AttackSerial.Should().Be(expected.AttackSerial);
        actual.GumpSequence.Should().Be(expected.GumpSequence);
        actual.GumpServerSerial.Should().Be(expected.GumpServerSerial);
        actual.GumpButton.Should().Be(expected.GumpButton);
        actual.GumpSwitches.Should().Equal(expected.GumpSwitches);
        actual.GumpEntries.Should().ContainSingle();
        actual.GumpEntries[0].Index.Should().Be(expected.GumpEntries[0].Index);
        actual.GumpEntries[0].Text.Should().Be(expected.GumpEntries[0].Text);
        actual.Command.Should().Be(expected.Command);
        actual.Error.Should().Be(expected.Error);
        actual.GroupSerials.Should().Equal(expected.GroupSerials);
    }

    [Fact]
    public void ProtocolRoundTripsMountState()
    {
        var expected = new DualBoxMessage
        {
            Type = DualBoxMessageType.MountState,
            Mounted = true,
            MountSequence = 73
        };

        DualBoxMessage actual = DualBoxProtocol.Decode(DualBoxProtocol.Encode(expected));

        actual.Type.Should().Be(DualBoxMessageType.MountState);
        actual.Mounted.Should().BeTrue();
        actual.MountSequence.Should().Be(73);
    }

    [Fact]
    public void ProtocolRoundTripsPartyInvite()
    {
        var expected = new DualBoxMessage
        {
            Type = DualBoxMessageType.PartyInvite,
            Serial = 0x01020304
        };

        DualBoxMessage actual = DualBoxProtocol.Decode(DualBoxProtocol.Encode(expected));

        actual.Type.Should().Be(DualBoxMessageType.PartyInvite);
        actual.Serial.Should().Be(expected.Serial);
    }

    [Fact]
    public void GumpEntryTextIsLimitedToTheProtocolSafeLength()
    {
        string text = new('x', DualBoxManager.MaxGumpEntryTextLength + 10);

        string actual = DualBoxManager.TruncateGumpEntry(text);

        actual.Should().HaveLength(DualBoxManager.MaxGumpEntryTextLength);
    }

    [Fact]
    public void MacroDefinitionRoundTripsActionsWithoutUsingClientProfileState()
    {
        var source = new Macro("Shared heal");
        source.PushToBack(new MacroObjectString(MacroType.Say, MacroSubType.MSC_NONE, "sync me"));
        source.PushToBack(new MacroObject(MacroType.WaitForTarget, MacroSubType.MSC_NONE));

        string definition = MacroManager.SerializeMacroDefinition(source);
        Macro copy = MacroManager.DeserializeMacroDefinition(definition);

        copy.Name.Should().Be(source.Name);
        var first = copy.Items.Should().BeOfType<MacroObjectString>().Subject;
        first.Code.Should().Be(MacroType.Say);
        first.Text.Should().Be("sync me");
        var second = first.Next.Should().BeOfType<MacroObject>().Subject;
        second.Code.Should().Be(MacroType.WaitForTarget);
    }

    [Theory]
    [InlineData((int)CursorTarget.Object, true)]
    [InlineData((int)CursorTarget.Position, true)]
    [InlineData((int)CursorTarget.MultiPlacement, true)]
    [InlineData((int)CursorTarget.CallbackTarget, false)]
    [InlineData((int)CursorTarget.SetTargetClientSide, false)]
    public void OnlyServerTargetCursorsAreSynchronized(int cursor, bool expected)
    {
        DualBoxManager.IsSynchronizableTargetCursor((CursorTarget)cursor).Should().Be(expected);
    }

    [Theory]
    [InlineData(100u, 200u, false)]
    [InlineData(200u, 200u, true)]
    [InlineData(201u, 200u, true)]
    [InlineData(5u, 0xFFFFFFF0u, true)]
    public void TargetDeadlineComparisonHandlesTickWraparound(uint now, uint deadline, bool expected)
    {
        DualBoxManager.HasDeadlinePassed(now, deadline).Should().Be(expected);
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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyScriptCommandsAreNotDispatched(string command)
    {
        bool dispatched = false;

        void Handler(string _) => dispatched = true;

        DualBoxManager.Instance.ScriptCommandReceived += Handler;

        try
        {
            DualBoxManager.Instance.DispatchScriptCommand(command);
        }
        finally
        {
            DualBoxManager.Instance.ScriptCommandReceived -= Handler;
        }

        dispatched.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, false, (int)DualBoxMountAction.None)]
    [InlineData(true, true, (int)DualBoxMountAction.None)]
    [InlineData(true, false, (int)DualBoxMountAction.Mount)]
    [InlineData(false, true, (int)DualBoxMountAction.Dismount)]
    public void MountActionMatchesDesiredState(
        bool desiredMounted,
        bool actualMounted,
        int expected
    )
    {
        DualBoxManager.GetMountAction(desiredMounted, actualMounted)
            .Should().Be((DualBoxMountAction)expected);
    }

    [Theory]
    [InlineData(false, false, false, false, true)]
    [InlineData(false, false, true, true, true)]
    [InlineData(true, true, true, true, true)]
    [InlineData(true, true, false, false, false)]
    [InlineData(true, false, false, false, true)]
    public void MountAcknowledgementWaitsForAnInFlightAction(
        bool actionInFlight,
        bool actionTargetMounted,
        bool desiredMounted,
        bool actualMounted,
        bool expected
    )
    {
        DualBoxManager.CanAcknowledgeMountState(
            actionInFlight,
            actionTargetMounted,
            desiredMounted,
            actualMounted
        ).Should().Be(expected);
    }

    [Theory]
    [InlineData(0x01020304u, 0u, 0x05060708u, false, true)]
    [InlineData(0x01020304u, 0x01020304u, 0x05060708u, false, true)]
    [InlineData(0x01020304u, 0x090A0B0Cu, 0x05060708u, false, false)]
    [InlineData(0x01020304u, 0u, 0x01020304u, false, false)]
    [InlineData(0x01020304u, 0u, 0x05060708u, true, false)]
    public void PartyInviteRequiresTheMasterToLeadAndAClientOutsideTheParty(
        uint masterSerial,
        uint leaderSerial,
        uint clientSerial,
        bool clientAlreadyInParty,
        bool expected
    )
    {
        DualBoxManager.CanInvitePartyClient(
            masterSerial,
            leaderSerial,
            clientSerial,
            clientAlreadyInParty
        ).Should().Be(expected);
    }
}
