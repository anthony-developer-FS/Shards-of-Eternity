using System;
using System.Collections.Generic;
using System.Linq;

namespace ShardsOfEternity.Core
{
    /// <summary>The 13 Diablo-style gear slots.</summary>
    public enum GearSlot
    {
        Head,
        Shoulders,
        Amulet,
        Chest,
        Wrists,
        Hands,
        Waist,
        Legs,
        Feet,
        Ring1,
        Ring2,
        MainHand,
        OffHand,
    }

    public enum Rarity
    {
        Common,
        Magic,
        Rare,
        Legendary,
    }

    public enum StatType
    {
        Damage,
        Armor,
        MaxHp,
        /// <summary>Attack Speed: more turns on the timeline.</summary>
        Speed,
        CritChance,
        CritDamage,
        Move,
    }

    public struct StatRoll
    {
        public StatType Stat;
        /// <summary>Whole points, or a fraction for crit chance and crit damage (0.05 = 5%).</summary>
        public double Value;

        public StatRoll(StatType stat, double value)
        {
            Stat = stat;
            Value = value;
        }

        public bool IsPercent => Stat == StatType.CritChance || Stat == StatType.CritDamage;

        public override string ToString()
        {
            string amount = IsPercent ? $"+{Math.Round(Value * 100)}%" : $"+{Value:0}";
            return $"{amount} {Gear.StatName(Stat)}";
        }
    }

    public sealed class GearItem
    {
        public string Name;
        public GearSlot Slot;
        public Rarity Rarity;
        /// <summary>The floor it dropped on. Higher floors roll bigger numbers.</summary>
        public int ItemLevel;
        public List<StatRoll> Stats = new List<StatRoll>();

        public double Total(StatType stat) => Stats.Where(s => s.Stat == stat).Sum(s => s.Value);

        public override string ToString() => $"{Name} ({Rarity} {Gear.SlotName(Slot)}, level {ItemLevel})";
    }

    /// <summary>What one hero has equipped. Two ring slots; every other slot holds one item.</summary>
    public sealed class Loadout
    {
        readonly Dictionary<GearSlot, GearItem> items = new Dictionary<GearSlot, GearItem>();

        public GearItem this[GearSlot slot] => items.TryGetValue(slot, out var item) ? item : null;

        public IEnumerable<GearItem> Items => items.Values;

        /// <summary>Equips an item and returns whatever it replaced, or null. Rings fill an empty ring slot first.</summary>
        public GearItem Equip(GearItem item)
        {
            var slot = item.Slot;
            if (Gear.IsRing(slot)) slot = this[GearSlot.Ring1] == null ? GearSlot.Ring1 : this[GearSlot.Ring2] == null ? GearSlot.Ring2 : GearSlot.Ring1;
            var old = this[slot];
            items[slot] = item;
            return old;
        }

        public GearItem Unequip(GearSlot slot)
        {
            var old = this[slot];
            items.Remove(slot);
            return old;
        }

        public double Total(StatType stat) => items.Values.Sum(i => i.Total(stat));

        /// <summary>The item this one would replace if equipped now, for comparisons.</summary>
        public GearItem WouldReplace(GearItem item)
        {
            if (!Gear.IsRing(item.Slot)) return this[item.Slot];
            if (this[GearSlot.Ring1] == null || this[GearSlot.Ring2] == null) return null;
            return this[GearSlot.Ring1];
        }
    }

    public static class Gear
    {
        public static bool IsRing(GearSlot slot) => slot == GearSlot.Ring1 || slot == GearSlot.Ring2;

        public static string SlotName(GearSlot slot)
        {
            switch (slot)
            {
                case GearSlot.Ring1:
                case GearSlot.Ring2: return "Ring";
                case GearSlot.MainHand: return "Main hand";
                case GearSlot.OffHand: return "Off hand";
                default: return slot.ToString();
            }
        }

        public static string StatName(StatType stat)
        {
            switch (stat)
            {
                case StatType.MaxHp: return "Health";
                case StatType.Speed: return "Attack Speed";
                case StatType.CritChance: return "Crit Chance";
                case StatType.CritDamage: return "Crit Damage";
                default: return stat.ToString();
            }
        }

        /// <summary>0xRRGGBB colors for loot beams and item names: white, blue, yellow, orange.</summary>
        public static uint RarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Magic: return 0x6F8CFF;
                case Rarity.Rare: return 0xFFE14D;
                case Rarity.Legendary: return 0xFF8A1F;
                default: return 0xE8E8E8;
            }
        }

        /// <summary>Adds a loadout's stats to a hero whose stats are still at their base values.</summary>
        public static void ApplyTo(Unit u, Loadout loadout)
        {
            if (loadout == null) return;
            int hp = (int)loadout.Total(StatType.MaxHp);
            u.MaxHp += hp;
            u.Hp += hp;
            u.Damage += (int)loadout.Total(StatType.Damage);
            u.Armor += (int)loadout.Total(StatType.Armor);
            u.Speed += (int)loadout.Total(StatType.Speed);
            u.Move += (int)loadout.Total(StatType.Move);
            u.Crit = Math.Min(1, u.Crit + loadout.Total(StatType.CritChance));
            u.CritDamage += loadout.Total(StatType.CritDamage);
            u.NextTurnAt = u.TurnInterval;
        }
    }

    /// <summary>Rolls drops: which enemies drop, at what rarity, and with what stats.</summary>
    public static class LootTable
    {
        /// <summary>Stats each slot can roll. The first entry is guaranteed on main hands (Damage).</summary>
        static readonly Dictionary<GearSlot, StatType[]> SlotStats = new Dictionary<GearSlot, StatType[]>
        {
            [GearSlot.Head] = new[] { StatType.MaxHp, StatType.Armor, StatType.CritChance },
            [GearSlot.Shoulders] = new[] { StatType.Armor, StatType.MaxHp, StatType.Damage },
            [GearSlot.Amulet] = new[] { StatType.CritDamage, StatType.CritChance, StatType.Damage, StatType.Speed },
            [GearSlot.Chest] = new[] { StatType.MaxHp, StatType.Armor, StatType.Damage },
            [GearSlot.Wrists] = new[] { StatType.CritChance, StatType.Armor, StatType.Speed },
            [GearSlot.Hands] = new[] { StatType.Speed, StatType.CritChance, StatType.Damage },
            [GearSlot.Waist] = new[] { StatType.MaxHp, StatType.Armor, StatType.Damage },
            [GearSlot.Legs] = new[] { StatType.MaxHp, StatType.Armor, StatType.Speed },
            [GearSlot.Feet] = new[] { StatType.Speed, StatType.Armor, StatType.MaxHp },
            [GearSlot.Ring1] = new[] { StatType.CritChance, StatType.CritDamage, StatType.Damage, StatType.Speed },
            [GearSlot.MainHand] = new[] { StatType.Damage, StatType.CritChance, StatType.CritDamage, StatType.Speed },
            [GearSlot.OffHand] = new[] { StatType.Armor, StatType.Damage, StatType.CritChance, StatType.MaxHp },
        };

        static readonly Dictionary<GearSlot, string[]> BaseNames = new Dictionary<GearSlot, string[]>
        {
            [GearSlot.Head] = new[] { "Helm", "Hood", "Crown" },
            [GearSlot.Shoulders] = new[] { "Pauldrons", "Mantle", "Spaulders" },
            [GearSlot.Amulet] = new[] { "Amulet", "Talisman", "Pendant" },
            [GearSlot.Chest] = new[] { "Hauberk", "Robe", "Cuirass" },
            [GearSlot.Wrists] = new[] { "Bracers", "Vambraces", "Cuffs" },
            [GearSlot.Hands] = new[] { "Gauntlets", "Gloves", "Grips" },
            [GearSlot.Waist] = new[] { "Belt", "Girdle", "Sash" },
            [GearSlot.Legs] = new[] { "Greaves", "Leggings", "Breeches" },
            [GearSlot.Feet] = new[] { "Boots", "Sabatons", "Treads" },
            [GearSlot.Ring1] = new[] { "Ring", "Band", "Signet" },
            [GearSlot.MainHand] = new[] { "Blade", "Axe", "Mace", "Bow", "Staff" },
            [GearSlot.OffHand] = new[] { "Shield", "Tome", "Orb", "Quiver" },
        };

        static readonly string[] MagicPrefixes = { "Glowing", "Keen", "Sturdy", "Swift", "Runed", "Gleaming" };
        static readonly string[] RareNames = { "Grim", "Dread", "Storm", "Ash", "Blood", "Veil", "Rift", "Shard" };
        static readonly string[] RareSuffixes = { "Bite", "Ward", "Call", "Song", "Fang", "Mark", "Shroud", "Edge" };

        static readonly Dictionary<GearSlot, string> Legendaries = new Dictionary<GearSlot, string>
        {
            [GearSlot.Head] = "Crown of the Shattered Sky",
            [GearSlot.Shoulders] = "Mantle of the Last Watch",
            [GearSlot.Amulet] = "Heart of Aethra",
            [GearSlot.Chest] = "Veilwoven Hauberk",
            [GearSlot.Wrists] = "Chains of the Rift Lord",
            [GearSlot.Hands] = "Grasp of Eternity",
            [GearSlot.Waist] = "Girdle of Broken Oaths",
            [GearSlot.Legs] = "Riftwalker Greaves",
            [GearSlot.Feet] = "Stride of the Unbound",
            [GearSlot.Ring1] = "Band of the First Shard",
            [GearSlot.MainHand] = "Veilbreaker",
            [GearSlot.OffHand] = "Aegis of Dawn",
        };

        /// <summary>Chance that a slain enemy drops an item. Reinforcements drop half as often.</summary>
        public static double DropChance(Unit enemy)
        {
            double chance;
            switch (enemy.Def.EnemyKind)
            {
                case EnemyKind.Ghoul: chance = .25; break;
                case EnemyKind.Imp: chance = .3; break;
                case EnemyKind.Hellknight: chance = .6; break;
                default: chance = 1; break;
            }
            return enemy.IsReinforcement ? chance / 2 : chance;
        }

        /// <summary>Items a slain enemy drops: usually none or one; the boss always drops two at Rare or better.</summary>
        public static List<GearItem> RollDrops(Random rng, Unit enemy, int floor)
        {
            var drops = new List<GearItem>();
            if (enemy.Def.IsBoss)
            {
                for (int i = 0; i < 2; i++)
                    drops.Add(RollItem(rng, floor, rng.NextDouble() < .4 ? Rarity.Legendary : Rarity.Rare));
            }
            else if (rng.NextDouble() < DropChance(enemy))
            {
                drops.Add(RollItem(rng, floor, RollRarity(rng, floor)));
            }
            return drops;
        }

        /// <summary>Rarity weights shift toward better items on deeper floors.</summary>
        public static Rarity RollRarity(Random rng, int floor)
        {
            int f = Math.Max(0, floor - 1);
            double common = Math.Max(20, 55 - 3 * f), magic = 30, rare = 12 + 1.5 * f, legendary = 3 + 0.5 * f;
            double roll = rng.NextDouble() * (common + magic + rare + legendary);
            if ((roll -= common) < 0) return Rarity.Common;
            if ((roll -= magic) < 0) return Rarity.Magic;
            if (roll - rare < 0) return Rarity.Rare;
            return Rarity.Legendary;
        }

        public static GearItem RollItem(Random rng, int floor, Rarity rarity, GearSlot? slot = null)
        {
            var s = slot ?? (GearSlot)rng.Next(Enum.GetValues(typeof(GearSlot)).Length);
            if (s == GearSlot.Ring2) s = GearSlot.Ring1;
            var pool = SlotStats[s];
            int count = Math.Min(pool.Length, (int)rarity + 1);

            var chosen = new List<StatType>();
            if (s == GearSlot.MainHand) chosen.Add(StatType.Damage);
            foreach (var stat in pool.OrderBy(_ => rng.Next()))
            {
                if (chosen.Count >= count) break;
                if (!chosen.Contains(stat)) chosen.Add(stat);
            }
            if (rarity == Rarity.Legendary && s == GearSlot.Feet) chosen.Add(StatType.Move);

            var item = new GearItem { Slot = s, Rarity = rarity, ItemLevel = Math.Max(1, floor) };
            foreach (var stat in chosen) item.Stats.Add(new StatRoll(stat, RollValue(rng, stat, item.ItemLevel, rarity)));
            item.Name = NameFor(rng, s, rarity);
            return item;
        }

        static double RollValue(Random rng, StatType stat, int level, Rarity rarity)
        {
            double scale = (1 + (level - 1) * 0.15) * (rarity == Rarity.Legendary ? 1.5 : rarity == Rarity.Rare ? 1.25 : rarity == Rarity.Magic ? 1.1 : 1);
            double Range(double min, double max) => min + rng.NextDouble() * (max - min);
            switch (stat)
            {
                case StatType.Damage: return Math.Max(1, Math.Round(Range(1, 2.5) * scale));
                case StatType.Armor: return Math.Max(1, Math.Round(Range(1, 2.2) * scale));
                case StatType.MaxHp: return Math.Max(1, Math.Round(Range(3, 6.5) * scale));
                case StatType.Speed: return Math.Max(1, Math.Round(Range(0.8, 2.2) * scale));
                case StatType.CritChance: return Math.Round(Range(0.02, 0.05) * scale, 2);
                case StatType.CritDamage: return Math.Round(Range(0.10, 0.25) * scale, 2);
                default: return 1;
            }
        }

        static string NameFor(Random rng, GearSlot slot, Rarity rarity)
        {
            var bases = BaseNames[slot];
            string baseName = bases[rng.Next(bases.Length)];
            switch (rarity)
            {
                case Rarity.Legendary: return Legendaries[slot];
                case Rarity.Rare: return $"{RareNames[rng.Next(RareNames.Length)]}{RareSuffixes[rng.Next(RareSuffixes.Length)].ToLowerInvariant()} {baseName}";
                case Rarity.Magic: return $"{MagicPrefixes[rng.Next(MagicPrefixes.Length)]} {baseName}";
                default: return baseName;
            }
        }
    }
}
