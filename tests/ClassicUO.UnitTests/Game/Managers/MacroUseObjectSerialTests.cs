using System.Xml;
using ClassicUO.Common.Enums;
using ClassicUO.Game.Managers;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.Game.Managers
{
    public class MacroUseObjectSerialTests
    {
        [Fact]
        public void CreateUseObject_CreatesStringActionWithExistingDefault()
        {
            MacroObject action = Macro.Create(MacroType.UseObject);

            action.Should().BeOfType<MacroObjectString>();
            action.SubCode.Should().Be(MacroSubType.BestHealPotion);
            action.SubMenuType.Should().Be(1);
        }

        [Fact]
        public void UseObjectSubTypes_AppendSerialWithoutRenumberingExistingValues()
        {
            MacroSubType[] subTypes = Macro.GetSubTypesByCode(MacroType.UseObject);

            subTypes[0].Should().Be(MacroSubType.BestHealPotion);
            subTypes[^2].Should().Be(MacroSubType.SpellStone);
            subTypes[^1].Should().Be(MacroSubType.Serial);
            ((int)MacroSubType.LookForwards).Should().Be(240);
            ((int)MacroSubType.Boarding).Should().Be(286);
            ((int)MacroSubType.Serial).Should().Be(287);
        }

        [Fact]
        public void LoadLegacyUseObjectWithoutText_UpgradesActionToStringAction()
        {
            var document = new XmlDocument();
            document.LoadXml(
                $"""
                 <macro key="0" alt="False" ctrl="False" shift="False">
                   <actions>
                     <action code="{(int)MacroType.UseObject}" subcode="{(int)MacroSubType.BestHealPotion}" submenutype="1" />
                   </actions>
                 </macro>
                 """
            );
            var macro = new Macro("legacy");

            macro.Load(document.DocumentElement);

            ((MacroObject)macro.Items).Should().BeOfType<MacroObjectString>();
        }

        [Fact]
        public void LoadSerialUseObject_PreservesSerialText()
        {
            var document = new XmlDocument();
            document.LoadXml(
                $"""
                 <macro key="0" alt="False" ctrl="False" shift="False">
                   <actions>
                     <action code="{(int)MacroType.UseObject}" subcode="{(int)MacroSubType.Serial}" submenutype="1" text="0x40001234" />
                   </actions>
                 </macro>
                 """
            );
            var macro = new Macro("serial");

            macro.Load(document.DocumentElement);

            var action = (MacroObjectString)macro.Items;
            action.SubCode.Should().Be(MacroSubType.Serial);
            action.Text.Should().Be("0x40001234");
        }

        [Fact]
        public void LoadLegacyLoopUseObjectWithoutText_UpgradesActionToStringAction()
        {
            var document = new XmlDocument();
            document.LoadXml(
                $"""
                 <loop loopcount="1" delaybetween="0">
                   <action code="{(int)MacroType.UseObject}" subcode="{(int)MacroSubType.BestHealPotion}" submenutype="1" />
                 </loop>
                 """
            );

            MacroLoopContainer container = MacroLoopContainer.Load(document.DocumentElement);

            container.Items.First.Value.Should().BeOfType<MacroObjectString>();
        }
    }
}
