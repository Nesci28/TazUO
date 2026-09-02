// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Configuration;

namespace ClassicUO.Game.Data
{
    public enum SpellBookType
    {
        Magery,
        Necromancy,
        Chivalry,
        Bushido = 4,
        Ninjitsu,
        Spellweaving,
        Mysticism,
        Mastery,
        Druidic, //custom for uo eventine could be used for others implementing it
        Cleric, //custom for uo eventine could be used for others implementing it
        Unknown = 0xFF
    }

    internal static class SpellBookDefinition
    {
        #region MacroSubType Offsets
        // Offset for MacroSubType
        private const int MAGERY_SPELLS_OFFSET = 61;
        private const int NECRO_SPELLS_OFFSET = 125;
        private const int CHIVAL_SPELLS_OFFSETS = 142;
        private const int BUSHIDO_SPELLS_OFFSETS = 152;
        private const int NINJITSU_SPELLS_OFFSETS = 158;
        private const int SPELLWEAVING_SPELLS_OFFSETS = 166;
        private const int MYSTICISM_SPELLS_OFFSETS = 182;
        private const int MASTERY_SPELLS_OFFSETS = 198;

        #endregion

        public static int GetSpellsGroup(int spellID)
        {
            int spellsGroup = spellID / 100;

            switch (spellsGroup)
            {
                case (int)SpellBookType.Magery:
                    return MAGERY_SPELLS_OFFSET;
                case (int)SpellBookType.Necromancy:
                    return NECRO_SPELLS_OFFSET;
                case (int)SpellBookType.Chivalry:
                    return CHIVAL_SPELLS_OFFSETS;
                case (int)SpellBookType.Bushido:
                    return BUSHIDO_SPELLS_OFFSETS;
                case (int)SpellBookType.Ninjitsu:
                    return NINJITSU_SPELLS_OFFSETS;
                case (int)SpellBookType.Spellweaving:
                    // Mysticicsm Spells Id starts from 678 and Spellweaving ends at 618
                    if (spellID > 620)
                    {
                        return MYSTICISM_SPELLS_OFFSETS;
                    }
                    return SPELLWEAVING_SPELLS_OFFSETS;
                case (int)SpellBookType.Mastery - 1:
                    return MASTERY_SPELLS_OFFSETS;
            }
            return -1;
        }

        /// <summary>
        /// Returns the cliloc used by spellbooks for a spell's description, or 0 when the
        /// full spell ID does not have a known description.
        /// </summary>
        public static int GetSpellDescriptionCliloc(int spellID)
        {
            return spellID switch
            {
                >= 1 and <= 64 => 1061290 + (spellID - 1),
                >= 101 and <= 117 => 1061390 + (spellID - 101),
                >= 201 and <= 210 => 1061490 + (spellID - 201),
                >= 302 and <= 321 when Settings.GlobalSettings.CustomServer == Settings.CustomServers.Eventine =>
                    1136632 + (spellID - 302),
                >= 342 and <= 353 when Settings.GlobalSettings.CustomServer == Settings.CustomServers.Eventine =>
                    1136654 + (spellID - 342),
                >= 401 and <= 406 => 1063263 + (spellID - 401),
                >= 501 and <= 508 => 1063279 + (spellID - 501),
                >= 601 and <= 616 => 1072042 + (spellID - 601),
                >= 678 and <= 693 => 1095193 + (spellID - 678),
                >= 701 and <= 745 => SpellsMastery.GetSpellTooltipCliloc(spellID - 700),
                _ => 0
            };
        }
    }
}
