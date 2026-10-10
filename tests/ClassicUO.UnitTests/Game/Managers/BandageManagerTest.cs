using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using Xunit;

namespace ClassicUO.UnitTests.Game.Managers;

public class BandageManagerTest
{
    [Theory]
    [InlineData(NotorietyFlag.Innocent)]
    [InlineData(NotorietyFlag.Ally)]
    public void IsFriendlyPartyPet_AcceptsFriendlyNonHumanMobile(NotorietyFlag notoriety)
    {
        var mobile = new Mobile(null, 1) { NotorietyFlag = notoriety };

        Assert.True(BandageManager.IsFriendlyPartyPet(mobile, CreateParty()));
    }

    [Fact]
    public void IsFriendlyPartyPet_RejectsOwnedPet()
    {
        var mobile = new Mobile(null, 1)
        {
            IsRenamable = true,
            NotorietyFlag = NotorietyFlag.Ally
        };

        Assert.False(BandageManager.IsFriendlyPartyPet(mobile, CreateParty()));
    }

    [Fact]
    public void IsFriendlyPartyPet_RejectsHumanAlly()
    {
        var mobile = new Mobile(null, 1) { NotorietyFlag = NotorietyFlag.Ally };
        typeof(Mobile).GetProperty(nameof(Mobile.IsHuman))!.SetValue(mobile, true);

        Assert.False(BandageManager.IsFriendlyPartyPet(mobile, CreateParty()));
    }

    [Fact]
    public void IsFriendlyPartyPet_RejectsMobileWhenPlayerIsNotGrouped()
    {
        var mobile = new Mobile(null, 1) { NotorietyFlag = NotorietyFlag.Innocent };

        Assert.False(BandageManager.IsFriendlyPartyPet(mobile, new PartyManager(null)));
    }

    [Theory]
    [InlineData(NotorietyFlag.Unknown)]
    [InlineData(NotorietyFlag.Gray)]
    [InlineData(NotorietyFlag.Criminal)]
    [InlineData(NotorietyFlag.Enemy)]
    [InlineData(NotorietyFlag.Murderer)]
    [InlineData(NotorietyFlag.Invulnerable)]
    public void IsFriendlyPartyPet_RejectsNonFriendlyCreature(NotorietyFlag notoriety)
    {
        var mobile = new Mobile(null, 1) { NotorietyFlag = notoriety };

        Assert.False(BandageManager.IsFriendlyPartyPet(mobile, CreateParty()));
    }

    [Fact]
    public void FriendlyPetBandaging_DefaultsToDisabled()
    {
        var profile = new ClassicUO.Configuration.Profile();

        Assert.False(profile.BandageAgentBandageFriendlyPets);
    }

    private static PartyManager CreateParty() => new(null) { Leader = 2 };
}
