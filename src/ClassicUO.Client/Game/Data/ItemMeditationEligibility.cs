using System.Collections.Generic;

namespace ClassicUO.Game.Data
{
    /// <summary>Standard UO equipment eligibility; server-specific armor allowances are not sent to the client.</summary>
    internal static class ItemMeditationEligibility
    {
        // Armor materials: https://uo.com/wiki/ultima-online-wiki/skills/magery/
        // Graphics/allowances: https://github.com/ServUO/ServUO/tree/pub57/Scripts/Items/Equipment/Armor
        private static readonly HashSet<ushort> MedableArmor = new()
        {
            0x2B6E, 0x3165, // Circlet
            0x782E, // DragonTurtleHideArms
            0x782B, // DragonTurtleHideBustier
            0x782A, // DragonTurtleHideChest
            0x782D, // DragonTurtleHideHelm
            0x782C, // DragonTurtleHideLegs
            0xA40F, // ElegantCollar
            0x0301, // FemaleGargishLeatherArms
            0x0303, // FemaleGargishLeatherChest
            0x0310, // FemaleGargishLeatherKilt
            0x0305, // GargishLeatherLegs (female/male)
            0x2FCB, 0x3181, // FemaleLeafChest
            0x1C06, 0x1C07, // FemaleLeatherChest
            0x0403, 0x0404, 0x0405, 0x0406, 0x0407, 0x0408, 0x0409, 0x040A, // GargishClothArmor
            0x0302, // GargishLeatherArms
            0x0304, // GargishLeatherChest
            0x0311, // GargishLeatherKilt
            0x457E, 0x457F, // GargishLeatherWingArmor
            0x2B70, 0x3167, // GemmedCirclet
            0x2FC8, 0x317E, // LeafArms
            0x2FC5, 0x317B, // LeafChest
            0x2FC6, // LeafGloves
            0x2FC7, // LeafGorget
            0x2FC9, 0x317F, // LeafLegs
            0x2FCA, 0x3180, // LeafTonlet
            0x13C5, 0x13CD, // LeatherArms
            0x1C0A, 0x1C0B, // LeatherBustierArms
            0x1DB9, 0x1DBA, // LeatherCap
            0x13CC, 0x13D3, // LeatherChest
            0x27C6, // LeatherDo
            0x13C6, 0x13CE, 0x26B0, // LeatherGloves
            0x13C7, // LeatherGorget
            0x278A, // LeatherHaidate
            0x277E, // LeatherHiroSode
            0x2776, // LeatherJingasa
            0x13CB, 0x13D2, // LeatherLegs
            0x277A, // LeatherMempo
            0x278E, // LeatherNinjaHood
            0x2793, // LeatherNinjaJacket
            0x2792, // LeatherNinjaMitts
            0x2791, // LeatherNinjaPants
            0x1C00, 0x1C01, // LeatherShorts
            0x1C08, 0x1C09, // LeatherSkirt
            0x2786, // LeatherSuneate
            0x2B6F, 0x3166, // RoyalCirclet
            0x7823, // TigerPeltBustier
            0x7822, // TigerPeltChest
            0x7829, // TigerPeltCollar
            0x7828, // TigerPeltHelm
            0x7824, // TigerPeltLegs
            0x7826, // TigerPeltLongSkirt
            0x7825, // TigerPeltShorts
            0x7827, // TigerPeltSkirt
            // Hats (including cloth ninja hoods and kasa)
            0x141B, 0x1540, 0x1544, 0x1545, 0x1547, 0x1549, 0x154B,
            0x1713, 0x1714, 0x1715, 0x1716, 0x1717, 0x1718, 0x1719, 0x171A, 0x171B, 0x171C,
            0x2305, 0x2306, 0x278F, 0x2798, 0x27DA, 0x27E3, 0x781A,
            0x2FB8, // ElvenGlasses
            0x4644, 0x4645, // GargishGlasses
            0xA410, // AssassinsCowl
            0xA411, // MagesHood
        };

        private static readonly HashSet<ushort> NonMedableArmor = new()
        {
            0x140C, // Bascinet
            0x144E, 0x1453, // BoneArms
            0x144F, 0x1454, // BoneChest
            0x1450, 0x1455, // BoneGloves
            0x1451, 0x1456, // BoneHelm
            0x1452, 0x1457, // BoneLegs
            0x13BF, 0x13C4, // ChainChest
            0x13BB, 0x13C0, // ChainCoif
            0x2774, // ChainHatsuburi
            0x13BE, 0x13C3, // ChainLegs
            0x1408, // CloseHelm
            0x2778, // DecorativePlateKabuto
            0x2657, 0x2658, // DragonArms
            0x2641, 0x2642, // DragonChest
            0x2643, 0x2644, // DragonGloves
            0x2645, 0x2646, // DragonHelm
            0x2647, 0x2648, // DragonLegs
            0x0307, // FemaleGargishPlateArms
            0x0309, // FemaleGargishPlateChest
            0x030B, // FemaleGargishPlateKilt
            0x030D, // FemaleGargishPlateLegs
            0x0283, // FemaleGargishStoneArms
            0x0285, // FemaleGargishStoneChest
            0x0287, // FemaleGargishStoneKilt
            0x0289, // FemaleGargishStoneLegs
            0x1C04, 0x1C05, // FemalePlateChest
            0x1C02, 0x1C03, // FemaleStuddedChest
            0x2B6D, 0x3164, // FemaleWoodlandChest
            0x0308, // GargishPlateArms
            0x030A, // GargishPlateChest
            0x030C, // GargishPlateKilt
            0x030E, // GargishPlateLegs
            0x0284, // GargishStoneArms
            0x0286, // GargishStoneChest
            0x0288, // GargishStoneKilt
            0x028A, // GargishStoneLegs
            0x2777, // HeavyPlateJingasa
            0x140A, // Helmet
            0x2B74, 0x316B, // HideChest
            0x2B79, 0x3170, // HideFemaleChest
            0x2B75, 0x316C, // HideGloves
            0x2B76, 0x316D, // HideGorget
            0x2B78, 0x316F, // HidePants
            0x2B77, 0x316E, // HidePauldrons
            0x2781, // LightPlateJingasa
            0x140E, // NorseHelm
            0x1F0B, // OrcHelm
            0x1410, 0x1417, // PlateArms
            0x2785, // PlateBattleKabuto
            0x1415, 0x1416, // PlateChest
            0x277D, // PlateDo
            0x1414, 0x1418, // PlateGloves
            0x1413, // PlateGorget
            0x278D, // PlateHaidate
            0x2775, // PlateHatsuburi
            0x1412, // PlateHelm
            0x2780, // PlateHiroSode
            0x1411, 0x141A, // PlateLegs
            0x2779, // PlateMempo
            0x2788, // PlateSuneate
            0x2B71, 0x3168, // RavenHelm
            0x13EE, 0x13EF, // RingmailArms
            0x13EC, 0x13ED, // RingmailChest
            0x13EB, 0x13F2, // RingmailGloves
            0x13F0, 0x13F1, // RingmailLegs
            0x2784, // SmallPlateJingasa
            0x2789, // StandardPlateKabuto
            0x13D4, 0x13DC, // StuddedArms
            0x1C0C, 0x1C0D, // StuddedBustierArms
            0x13DB, 0x13E2, // StuddedChest
            0x27C7, // StuddedDo
            0x13D5, 0x13DD, // StuddedGloves
            0x13D6, // StuddedGorget
            0x278B, // StuddedHaidate
            0x277F, // StuddedHiroSode
            0x13DA, 0x13E1, // StuddedLegs
            0x279D, // StuddedMempo
            0x27D2, // StuddedSuneate
            0x2B72, 0x3169, // VultureHelm
            0x2B73, 0x316A, // WingedHelm
            0x2B6C, 0x3163, // WoodlandArms
            0x2B67, 0x315E, // WoodlandChest
            0x2B6A, 0x3161, // WoodlandGloves
            0x2B69, 0x3160, // WoodlandGorget
            0x2B6B, 0x3162, // WoodlandLegs
        };

        /// <summary>Returns null for held items, non-equipment and unrecognized armor types.</summary>
        internal static bool? Get(ushort graphic, byte layer, bool mageArmor)
        {
            if (graphic == 0 || !IsEquipmentLayer(layer))
                return null;

            if (layer is (byte)Layer.OneHanded or (byte)Layer.TwoHanded)
            {
                // The filter describes armor material; Spell Channeling on held gear is separate.
                return null;
            }

            // Mage Armor makes otherwise non-medable armor medable.
            if (mageArmor)
                return true;

            if (MedableArmor.Contains(graphic))
                return true;

            if (NonMedableArmor.Contains(graphic))
                return false;

            // Standard necklaces share a layer with gorgets. Gargish amulets allow meditation
            // even though their server base class is armor (including the stone amulet).
            // https://github.com/ServUO/ServUO/tree/pub57/Scripts/Items/Equipment/Jewelry
            if (graphic is 0x1085 or 0x1088 or 0x1089 or 0x1F08 or 0x1F05 or 0xA349
                or 0x4210 or 0x4D0B or 0x4D0A)
                return true;

            // Clothing/jewelry layers are safe; unknown graphics on armor layers stay unknown.
            return layer switch
            {
                (byte)Layer.Helmet or (byte)Layer.Gloves or (byte)Layer.Neck or
                (byte)Layer.Torso or (byte)Layer.Arms or (byte)Layer.Legs => null,
                _ => true
            };
        }

        private static bool IsEquipmentLayer(byte layer) => layer is
            (byte)Layer.OneHanded or (byte)Layer.TwoHanded or (byte)Layer.Shoes or
            (byte)Layer.Pants or (byte)Layer.Shirt or (byte)Layer.Helmet or
            (byte)Layer.Gloves or (byte)Layer.Ring or (byte)Layer.Talisman or
            (byte)Layer.Neck or (byte)Layer.Waist or (byte)Layer.Torso or
            (byte)Layer.Bracelet or (byte)Layer.Tunic or (byte)Layer.Earrings or
            (byte)Layer.Arms or (byte)Layer.Cloak or (byte)Layer.Robe or
            (byte)Layer.Skirt or (byte)Layer.Legs;
    }
}
