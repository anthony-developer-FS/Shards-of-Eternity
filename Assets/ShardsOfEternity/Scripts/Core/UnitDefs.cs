using System.Collections.Generic;

namespace ShardsOfEternity.Core
{
    public enum Race
    {
        Human,
        // Angel and Demon races come later, each with their own classes.
    }

    public enum HeroClass
    {
        Barbarian,
        Paladin,
        Rogue,
        Sorcerer,
        Hunter,
        Shaman,
        Priest,
    }

    public enum EnemyKind
    {
        Ghoul,
        Imp,
        Hellknight,
        /// <summary>Boss of a run's final floor.</summary>
        RiftLord,
    }

    /// <summary>Base stats for a hero or enemy. Gear and buffs are applied on the runtime <see cref="Unit"/>.</summary>
    public sealed class UnitDef
    {
        public string Id;
        public string Name;
        public string Glyph;
        public string Rule;
        public bool IsEnemy;
        public bool IsBoss;
        public Race Race;
        public HeroClass HeroClass;
        public EnemyKind EnemyKind;

        public int Hp;
        public int Damage;
        public int Armor;
        /// <summary>Attack Speed. A unit's turn comes round every 1000 / Speed timeline points.</summary>
        public int Speed;
        public int Move;
        public int Range;
        public double Crit;
        /// <summary>Bonus damage on a crit, as a fraction: 1 means a crit deals double.</summary>
        public double CritDamage;

        public string Subtitle => IsEnemy ? "Enemy" : $"{Race} · {HeroClass}";
    }

    /// <summary>The roster from the browser prototype. Numbers match prototype/index.html.</summary>
    public static class Roster
    {
        public static readonly IReadOnlyList<UnitDef> Heroes = new[]
        {
            Hero("kael", "Kael", HeroClass.Barbarian, "B", 30, 8, 2, 11, 4, 1, .15, 1,
                "Charge: up to 12 tiles in a straight line if it ends next to an enemy."),
            Hero("bram", "Bram", HeroClass.Paladin, "P", 36, 6, 4, 8, 3, 1, .1, 1,
                "Hold the line: enemies must stop when they step next to him."),
            Hero("nyra", "Nyra", HeroClass.Rogue, "R", 22, 6, 1, 15, 4, 1, .3, 1.5,
                "Light step: ignores spikes and never gives free attacks when leaving an enemy."),
            Hero("ilse", "Ilse", HeroClass.Sorcerer, "S", 18, 7, 0, 10, 3, 5, .1, 1,
                "Levitate: floats over spikes. Teleport is one of her abilities."),
            Hero("wren", "Wren", HeroClass.Hunter, "H", 20, 6, 1, 12, 3, 6, .2, 1,
                "Steady aim: +2 damage on attacks made before moving this turn."),
            Hero("tova", "Tova", HeroClass.Shaman, "Sh", 24, 5, 2, 9, 3, 3, .1, 1,
                "Spirit swap: instead of walking, trade places with an ally next to her."),
            Hero("sera", "Sera", HeroClass.Priest, "Pr", 22, 5, 1, 10, 3, 3, .1, 1,
                "Mercy: her heals also cure poison. Resurrection brings fallen allies back."),
        };

        public static readonly IReadOnlyList<UnitDef> Enemies = new[]
        {
            Enemy(EnemyKind.Ghoul, "Ghoul", "g", 16, 6, 0, 9, 4, 1, "Melee. Shambles toward the nearest hero."),
            Enemy(EnemyKind.Imp, "Imp", "i", 10, 5, 0, 13, 4, 4, "Ranged, 4 tiles. Backs away from melee."),
            Enemy(EnemyKind.Hellknight, "Hellknight", "K", 30, 9, 3, 6, 3, 1, "Slow, armored and hits hard."),
            Boss(EnemyKind.RiftLord, "Rift Lord", "L", 80, 9, 3, 9, 3, 1, "Boss. Cleaves every hero next to it."),
        };

        public static readonly string[] DefaultSquad = { "kael", "bram", "nyra", "ilse" };

        public static UnitDef Hero(string id)
        {
            foreach (var h in Heroes) if (h.Id == id) return h;
            throw new KeyNotFoundException(id);
        }

        public static UnitDef Enemy(EnemyKind kind)
        {
            foreach (var e in Enemies) if (e.EnemyKind == kind) return e;
            throw new KeyNotFoundException(kind.ToString());
        }

        static UnitDef Boss(EnemyKind kind, string name, string glyph, int hp, int dmg, int arm, int spd, int move, int range,
            string rule)
        {
            var def = Enemy(kind, name, glyph, hp, dmg, arm, spd, move, range, rule);
            def.IsBoss = true;
            return def;
        }

        static UnitDef Hero(string id, string name, HeroClass cls, string glyph, int hp, int dmg, int arm, int spd, int move,
            int range, double crit, double critDmg, string rule) => new UnitDef
        {
            Id = id, Name = name, HeroClass = cls, Race = Race.Human, Glyph = glyph, Hp = hp, Damage = dmg, Armor = arm,
            Speed = spd, Move = move, Range = range, Crit = crit, CritDamage = critDmg, Rule = rule,
        };

        static UnitDef Enemy(EnemyKind kind, string name, string glyph, int hp, int dmg, int arm, int spd, int move, int range,
            string rule) => new UnitDef
        {
            Id = kind.ToString().ToLowerInvariant(), Name = name, EnemyKind = kind, IsEnemy = true, Glyph = glyph, Hp = hp,
            Damage = dmg, Armor = arm, Speed = spd, Move = move, Range = range, Crit = .1, CritDamage = 1, Rule = rule,
        };
    }
}
