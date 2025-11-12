using System;

namespace Shared.Battle
{
    [Flags]
    public enum UnitClassType : short
    {
        Unknown = 0,

        Hero = 1 << 0,
        Minion = 1 << 1,
        Tower = 1 << 2,
        Neutral = 1 << 3,
        Summoned = 1 << 4,

        Glyph = 1 << 5,
        Corpse = 1 << 6,

        Supervisor = 1 << 7,

        Boss = 1 << 8,
        Destroyable = 1 << 9,
    }

    [Flags]
    public enum UnitClassMask : short
    {
        Unknown = 0,

        DeadHero = UnitClassType.Hero | UnitClassType.Corpse,
        DeadMinion = UnitClassType.Minion & UnitClassType.Corpse,
        DeadTower = UnitClassType.Tower & UnitClassType.Corpse,
        DeadNeutral = UnitClassType.Neutral & UnitClassType.Corpse,
        DeadSummoned = UnitClassType.Summoned & UnitClassType.Corpse,
        DeadGlyph = UnitClassType.Glyph & UnitClassType.Corpse,

        Unit = UnitClassType.Hero | UnitClassType.Minion | UnitClassType.Neutral | UnitClassType.Summoned | UnitClassType.Tower | UnitClassType.Boss | UnitClassType.Destroyable,
        UnitNotMinion = UnitClassType.Hero | UnitClassType.Neutral | UnitClassType.Summoned | UnitClassType.Tower | UnitClassType.Boss,
        UnitNotBuilding = UnitClassType.Hero | UnitClassType.Minion | UnitClassType.Neutral | UnitClassType.Summoned | UnitClassType.Boss,
        IgnoreBush = UnitClassType.Minion | UnitClassType.Glyph | UnitClassType.Neutral | UnitClassType.Summoned,
        Unmovable = UnitClassType.Glyph | UnitClassType.Tower,
        NotAUnit = UnitClassType.Glyph | UnitClassType.Supervisor,
        Dead = UnitClassType.Corpse,
        PseudoUnit = NotAUnit | Dead,
    }
}
