using ClassicUO.Game.Data;
using ClassicUO.Game.Managers.VendorSearch;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.Game.Managers;

public class VendorSearchContextMenuTests
{
    private const uint PlayerSerial = 0x00000001;

    [Fact]
    public void ResolvesEnabledVendorSearchEntryForPlayer()
    {
        var data = new PopupMenuData(
            PlayerSerial,
            [new PopupMenuItem(VendorSearchWebManager.VendorSearchContextCliloc, 17, 0, 0, 0)]
        );

        VendorSearchWebManager
            .TryGetEnabledContextMenuIndex(data, PlayerSerial, out ushort index)
            .Should()
            .BeTrue();
        index.Should().Be(17);
    }

    [Fact]
    public void RejectsVendorSearchEntryForAnotherMobile()
    {
        var data = new PopupMenuData(
            0x00000002,
            [new PopupMenuItem(VendorSearchWebManager.VendorSearchContextCliloc, 17, 0, 0, 0)]
        );

        VendorSearchWebManager
            .TryGetEnabledContextMenuIndex(data, PlayerSerial, out _)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void RejectsDisabledVendorSearchEntry()
    {
        var data = new PopupMenuData(
            PlayerSerial,
            [new PopupMenuItem(VendorSearchWebManager.VendorSearchContextCliloc, 17, 0, 0, 0x01)]
        );

        VendorSearchWebManager
            .TryGetEnabledContextMenuIndex(data, PlayerSerial, out _)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void RejectsMenuWithoutVendorSearchEntry()
    {
        var data = new PopupMenuData(
            PlayerSerial,
            [new PopupMenuItem(3000001, 2, 0, 0, 0)]
        );

        VendorSearchWebManager
            .TryGetEnabledContextMenuIndex(data, PlayerSerial, out _)
            .Should()
            .BeFalse();
    }
}
