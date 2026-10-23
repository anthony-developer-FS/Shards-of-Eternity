using System;
using System.Linq;

namespace ShardsOfEternity.Core
{
    /// <summary>One hero's level and the XP banked toward the next one.</summary>
    public sealed class HeroProgress
    {
        public readonly string Id;
        public int Level = 1;
        /// <summary>XP toward the next level, not the lifetime total.</summary>
        public int Xp;

        public HeroProgress(string id) => Id = id;

        public bool IsMaxLevel => Level >= Progression.MaxLevel;
        public int XpToNext => Progression.XpToNext(Level);

        /// <summary>Adds XP and levels up as many times as it pays for. Returns the levels gained.</summary>
        public int AddXp(int amount)
        {
            int before = Level;
            if (amount <= 0 || IsMaxLevel) return 0;
            Xp += amount;
            while (!IsMaxLevel && Xp >= XpToNext)
            {
                Xp -= XpToNext;
                Level++;
            }
            if (IsMaxLevel) Xp = 0;
            return Level - before;
        }
    }

    /// <summary>
    /// Hero levels and the XP a run pays out. The squad shares the run's XP: every hero in it earns the full amount,
    /// fallen or not, so bringing a new class along never feels like a waste.
    /// </summary>
    public static class Progression
    {
        public const int MaxLevel = 20;
        public const int HealthPerLevel = 3;
        public const int LevelsPerDamage = 3;
        public const int LevelsPerArmor = 4;

        /// <summary>XP needed to go from <paramref name="level"/> to the next one: 100, 200, 300...</summary>
        public static int XpToNext(int level) => 100 * Math.Max(1, level);

        public static int BonusHealth(int level) => HealthPerLevel * (Math.Max(1, level) - 1);
        public static int BonusDamage(int level) => (Math.Max(1, level) - 1) / LevelsPerDamage;
        public static int BonusArmor(int level) => (Math.Max(1, level) - 1) / LevelsPerArmor;

        /// <summary>Adds a level's bonuses to a hero whose stats are still at their base values. Apply before gear.</summary>
        public static void ApplyTo(Unit u, int level)
        {
            int hp = BonusHealth(level);
            u.MaxHp += hp;
            u.Hp += hp;
            u.Damage += BonusDamage(level);
            u.Armor += BonusArmor(level);
        }

        /// <summary>What a level adds, for the UI: "+6 Health, +1 Damage".</summary>
        public static string BonusText(int level)
        {
            var parts = new[]
            {
                BonusHealth(level) > 0 ? $"+{BonusHealth(level)} Health" : null,
                BonusDamage(level) > 0 ? $"+{BonusDamage(level)} Damage" : null,
                BonusArmor(level) > 0 ? $"+{BonusArmor(level)} Armor" : null,
            }.Where(p => p != null).ToArray();
            return parts.Length == 0 ? "No bonus yet" : string.Join(", ", parts);
        }

        /// <summary>XP for a slain enemy. Reinforcements pay half, summoned imps very little, and deeper floors pay more.</summary>
        public static int KillXp(Unit enemy, int floor)
        {
            int xp;
            switch (enemy.Def.EnemyKind)
            {
                case EnemyKind.Ghoul: xp = 8; break;
                case EnemyKind.Imp: xp = 6; break;
                case EnemyKind.Hellhound: xp = 8; break;
                case EnemyKind.Cultist: xp = 10; break;
                case EnemyKind.RiftCaller: xp = 15; break;
                case EnemyKind.Hellknight: xp = 18; break;
                default: xp = enemy.Def.IsBoss ? 120 : 8; break;
            }
            if (enemy.SummonedBy != null) xp = 2;
            else if (enemy.IsReinforcement) xp /= 2;
            return (int)Math.Round(xp * FloorScale(floor));
        }

        public static int FloorClearXp(int floor) => 25 + 5 * Math.Max(1, floor);

        public static int VictoryXp(RunKind kind) => kind == RunKind.Raid ? 400 : 100;

        static double FloorScale(int floor) => 1 + 0.1 * Math.Max(0, floor - 1);

        /// <summary>XP earned on one floor so far: kills, the clear bonus, and the victory bonus on the last floor.</summary>
        public static int XpFor(Battle battle, RunKind kind)
        {
            if (battle == null) return 0;
            int floor = battle.Layout.Floor;
            int xp = battle.Slain.Sum(e => KillXp(e, floor));
            if (battle.FloorCleared) xp += FloorClearXp(floor);
            if (battle.Outcome == BattleOutcome.Victory) xp += VictoryXp(kind);
            return xp;
        }
    }
}
