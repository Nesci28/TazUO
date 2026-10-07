using ClassicUO.Common.Enums;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.UnitTests.Fixtures;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.Game.Managers;

[Collection(CurrentProfileCollection.Name)]
public class CounterBarMacroIconTest
{
    private readonly World _world = new();

    [Fact]
    public void GreaterHealThenTargetSelf_UsesGreaterHealIconAndKeepsMacroSlot()
    {
        var macro = new Macro("Heal self");
        macro.PushToBack(new MacroObject(MacroType.CastSpell, MacroSubType.GreaterHeal));
        macro.PushToBack(new MacroObject(MacroType.TargetSelf, MacroSubType.MSC_NONE));
        CounterBarSlot slot = AddMacro(macro);

        slot.GetIconGraphic(_world).Should().Be(0x08DC);
        slot.Type.Should().Be(CounterBarSlotType.Macro);
        slot.MacroName.Should().Be("Heal self");
        slot.TryGetTooltip(_world, out string tooltip).Should().BeTrue();
        tooltip.Should().Be("Heal self");
        ((MacroObject)macro.Items.Next).Code.Should().Be(MacroType.TargetSelf);
    }

    [Theory]
    [InlineData(MacroType.CastSpell, MacroSubType.Clumsy, 1)]
    [InlineData(MacroType.CastSpell, MacroSubType.WaterElemental, 64)]
    [InlineData(MacroType.CastSpell, MacroSubType.AnimateDead, 101)]
    [InlineData(MacroType.CastSpell, MacroSubType.Exorcism, 117)]
    [InlineData(MacroType.CastSpell, MacroSubType.CleanseByFire, 201)]
    [InlineData(MacroType.CastSpell, MacroSubType.SacredJourney, 210)]
    [InlineData(MacroType.CastSpell, MacroSubType.HonorableExecution, 401)]
    [InlineData(MacroType.CastSpell, MacroSubType.MomentumStrike, 406)]
    [InlineData(MacroType.CastSpell, MacroSubType.FocusAttack, 501)]
    [InlineData(MacroType.CastSpell, MacroSubType.MirrorImage, 508)]
    [InlineData(MacroType.CastSpell, MacroSubType.ArcaneCircle, 601)]
    [InlineData(MacroType.CastSpell, MacroSubType.ArcaneEmpowerment, 616)]
    [InlineData(MacroType.CastSpell, MacroSubType.NetherBolt, 678)]
    [InlineData(MacroType.CastSpell, MacroSubType.RisingColossus, 693)]
    [InlineData(MacroType.CastMasterySpell, MacroSubType.Inspire, 701)]
    [InlineData(MacroType.CastMasterySpell, MacroSubType.Boarding, 745)]
    public void CastStep_UsesSpellIconAcrossSchools(MacroType type, MacroSubType subCode, int spellId)
    {
        CounterBarSlot slot = AddMacro(Macro.CreateFastMacro("Cast", type, subCode));
        SpellDefinition spell = SpellDefinition.FullIndexGetSpell(spellId);

        spell.GumpIconSmallID.Should().NotBe(0);
        slot.GetIconGraphic(_world).Should().Be((ushort)spell.GumpIconSmallID);

        if (type == MacroType.CastSpell)
            MacroManager.GetCastSpellIndex(subCode).Should().Be(spellId);
    }

    [Fact]
    public void MultipleCastSteps_UsesFirstSpellAfterNonCastSteps()
    {
        var macro = new Macro("Heal then cure");
        macro.PushToBack(new MacroObject(MacroType.LastObject, MacroSubType.MSC_NONE));
        macro.PushToBack(new MacroObject(MacroType.CastSpell, MacroSubType.GreaterHeal));
        macro.PushToBack(new MacroObject(MacroType.CastSpell, MacroSubType.Cure));

        AddMacro(macro).GetIconGraphic(_world).Should().Be(0x08DC);
    }

    [Fact]
    public void CastInsideLoop_UsesSpellBeforeLaterTopLevelCast()
    {
        var macro = new Macro("Loop heal");
        var loop = new MacroLoopContainer();
        loop.Items.AddLast(new MacroObject(MacroType.TargetSelf, MacroSubType.MSC_NONE));
        loop.Items.AddLast(new MacroObject(MacroType.CastSpell, MacroSubType.GreaterHeal));
        macro.PushToBack(loop);
        macro.PushToBack(new MacroObject(MacroType.CastSpell, MacroSubType.Cure));

        AddMacro(macro).GetIconGraphic(_world).Should().Be(0x08DC);
    }

    [Fact]
    public void CustomIcon_TakesPrecedenceOverCastStep()
    {
        Macro macro = Macro.CreateFastMacro("Custom heal", MacroType.CastSpell, MacroSubType.GreaterHeal);
        macro.Graphic = 0x1234;

        AddMacro(macro).GetIconGraphic(_world).Should().Be(0x1234);
    }

    [Fact]
    public void ZeroCustomGraphic_FallsBackToSpellIcon()
    {
        Macro macro = Macro.CreateFastMacro("Heal", MacroType.CastSpell, MacroSubType.GreaterHeal);
        macro.Graphic = 0;

        AddMacro(macro).GetIconGraphic(_world).Should().Be(0x08DC);
    }

    [Theory]
    [InlineData(MacroType.TargetSelf, MacroSubType.MSC_NONE)]
    [InlineData(MacroType.LastSpell, MacroSubType.MSC_NONE)]
    [InlineData(MacroType.CastSpell, MacroSubType.DEPRECATED)]
    [InlineData(MacroType.CastSpell, MacroSubType.Hostile)]
    [InlineData(MacroType.CastMasterySpell, MacroSubType.GreaterHeal)]
    public void MacroWithoutValidCastStep_HasNoIcon(MacroType type, MacroSubType subCode)
    {
        AddMacro(Macro.CreateFastMacro("No spell", type, subCode)).GetIconGraphic(_world).Should().Be(0);
    }

    [Fact]
    public void RestoredSlot_ResolvesCurrentMacroAndFollowsEdits()
    {
        Macro macro = Macro.CreateFastMacro("Heal", MacroType.CastSpell, MacroSubType.GreaterHeal);
        _world.Macros.PushToBack(macro);
        var slot = new CounterBarSlot { Type = CounterBarSlotType.Macro, MacroName = "Heal" };

        slot.GetIconGraphic(_world).Should().Be(0x08DC);

        ((MacroObject)macro.Items).SubCode = MacroSubType.Cure;
        slot.GetIconGraphic(_world).Should().Be((ushort)SpellDefinition.FullIndexGetSpell(11).GumpIconSmallID);

        macro.Clear();
        slot.GetIconGraphic(_world).Should().Be(0);

        _world.Macros.Remove(macro);
        slot.GetIconGraphic(_world).Should().Be(0);
    }

    [Fact]
    public void MissingMacroOrWorld_HasNoIcon()
    {
        var slot = new CounterBarSlot { Type = CounterBarSlotType.Macro, MacroName = "Missing" };

        slot.GetIconGraphic(_world).Should().Be(0);
        slot.GetIconGraphic(null).Should().Be(0);
    }

    private CounterBarSlot AddMacro(Macro macro)
    {
        _world.Macros.PushToBack(macro);
        return CounterBarSlot.FromMacro(macro);
    }
}
