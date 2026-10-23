using System;
using System.Linq;

namespace ShardsOfEternity.Core
{
    public enum TargetKind
    {
        /// <summary>Used on the caster's own tile.</summary>
        Self,
        AdjacentEnemy,
        /// <summary>A hero (including the caster) within <see cref="Ability.Range"/>.</summary>
        Ally,
        /// <summary>An enemy within range and line of sight.</summary>
        Enemy,
        /// <summary>Any open tile within range and line of sight.</summary>
        Tile,
        /// <summary>Any empty open tile on the map.</summary>
        AnyTile,
        /// <summary>The body of a fallen hero within <see cref="Ability.Range"/>.</summary>
        FallenAlly,
    }

    /// <summary>
    /// A hero ability. Each hero has 3 cooldown abilities plus an ultimate that charges as they fight
    /// (+20 at turn start in combat, +10 per hit dealt, +5 per hit taken).
    /// </summary>
    public sealed class Ability
    {
        public string Name;
        public string Description;
        public int Cooldown;
        /// <summary>Extra timeline delay added to the caster's next turn.</summary>
        public int Recovery;
        public int Range;
        /// <summary>Radius of the highlighted area around the target (1 = 3 by 3).</summary>
        public int Area;
        public bool IsUltimate;
        public TargetKind Target;
        /// <summary>Rain of Arrows: highlight every enemy the caster can see.</summary>
        public bool HighlightsSightFoes;

        /// <summary>Whether a candidate target is valid: (battle, caster, candidate).</summary>
        public Func<Battle, Unit, Unit, bool> Need;
        /// <summary>Resolves the ability: (battle, caster, target tile, unit on that tile or null).</summary>
        public Action<Battle, Unit, Point, Unit> Run;
        /// <summary>Exact outcome text shown before the player commits.</summary>
        public Func<Battle, Unit, Point, Unit, string> Preview;
    }

    public static class AbilityLibrary
    {
        public static Ability[] For(HeroClass cls)
        {
            switch (cls)
            {
                case HeroClass.Barbarian: return Barbarian();
                case HeroClass.Paladin: return Paladin();
                case HeroClass.Rogue: return Rogue();
                case HeroClass.Sorcerer: return Sorcerer();
                case HeroClass.Hunter: return Hunter();
                case HeroClass.Shaman: return Shaman();
                case HeroClass.Priest: return Priest();
                default: throw new ArgumentOutOfRangeException(nameof(cls));
            }
        }

        static string HitList(Battle b, Unit u, System.Collections.Generic.IEnumerable<Unit> foes, int? dmg = null) =>
            string.Join(", ", foes.Select(e => $"{e.Name} for {b.GetDamageInfo(u, e, dmg).Damage}"));

        static string Plural(int n, string word) => $"{n} {word}{(n == 1 ? "" : "s")}";

        static Ability[] Barbarian() => new[]
        {
            new Ability
            {
                Name = "Cleave", Cooldown = 2, Recovery = 30, Target = TargetKind.Self,
                Description = "Hit every adjacent enemy. Adds 30 recovery.",
                Need = (b, u, _) => b.FoesNear(u.Position, 1).Count > 0,
                Run = (b, u, p, t) => { foreach (var e in b.FoesNear(u.Position, 1)) b.Strike(u, e, u.Damage); },
                Preview = (b, u, p, t) => $"Hits {HitList(b, u, b.FoesNear(u.Position, 1))}. Adds 30 recovery.",
            },
            new Ability
            {
                Name = "Reckless Strike", Cooldown = 2, Target = TargetKind.AdjacentEnemy,
                Description = "Hit for double damage. Kael takes 4 damage.",
                Run = (b, u, p, t) =>
                {
                    b.Strike(u, t, u.Damage * 2);
                    if (u.Alive)
                    {
                        b.Note($"{u.Name} pays 4 HP for the blow.");
                        b.Hurt(u, 4, false);
                    }
                },
                Preview = (b, u, p, t) => $"Hits {t.Name} for {b.GetDamageInfo(u, t, u.Damage * 2).Damage}. {u.Name} takes 4 damage.",
            },
            new Ability
            {
                Name = "War Cry", Cooldown = 4, Target = TargetKind.Self,
                Description = "Allies within 3 tiles act 30 sooner.",
                Need = (b, u, _) => b.FriendsNear(u.Position, 3).Any(h => h != u),
                Run = (b, u, p, t) => { foreach (var h in b.FriendsNear(u.Position, 3).Where(h => h != u)) b.Hasten(h, 30); },
                Preview = (b, u, p, t) => $"{Battle.Names(b.FriendsNear(u.Position, 3).Where(h => h != u))} act 30 sooner.",
            },
            new Ability
            {
                Name = "Whirlwind", IsUltimate = true, Target = TargetKind.Self, Area = 2,
                Description = "Hit every enemy within 2 tiles for Damage +4.",
                Need = (b, u, _) => b.FoesNear(u.Position, 2).Count > 0,
                Run = (b, u, p, t) => { foreach (var e in b.FoesNear(u.Position, 2)) b.Strike(u, e, u.Damage + 4); },
                Preview = (b, u, p, t) => $"Hits {HitList(b, u, b.FoesNear(u.Position, 2), u.Damage + 4)}.",
            },
        };

        static Ability[] Paladin() => new[]
        {
            new Ability
            {
                Name = "Holy Smite", Cooldown = 2, Target = TargetKind.AdjacentEnemy,
                Description = "Hit and knock the target back 2 tiles.",
                Run = (b, u, p, t) =>
                {
                    b.Strike(u, t, u.Damage);
                    if (t.Alive) b.Knockback(u, t, 2);
                },
                Preview = (b, u, p, t) => $"Hits {t.Name} for {b.GetDamageInfo(u, t).Damage} and knocks it back {b.KnockbackPreview(u, t, 2)}.",
            },
            new Ability
            {
                Name = "Lay on Hands", Cooldown = 3, Target = TargetKind.Ally, Range = 1,
                Description = "Heal Bram or an adjacent ally for 12.",
                Need = (b, u, h) => h.Hp < h.MaxHp,
                Run = (b, u, p, t) => b.Heal(t, 12),
                Preview = (b, u, p, t) => $"Heals {t.Name} for {Math.Min(12, t.MaxHp - t.Hp)}.",
            },
            new Ability
            {
                Name = "Divine Shield", Cooldown = 3, Target = TargetKind.Self,
                Description = "+4 Armor until his next turn.",
                Run = (b, u, p, t) =>
                {
                    u.ArmorBuff = 4;
                    b.Note($"{u.Name} raises a divine shield.");
                },
                Preview = (b, u, p, t) => $"Armor goes from {u.Armor} to {u.Armor + 4} until {u.Name}'s next turn.",
            },
            new Ability
            {
                Name = "Judgment", IsUltimate = true, Target = TargetKind.Self, Area = 2,
                Description = "Hit every enemy within 2 tiles, and heal every ally within 2 tiles for 8.",
                Run = (b, u, p, t) =>
                {
                    foreach (var e in b.FoesNear(u.Position, 2)) b.Strike(u, e, u.Damage);
                    foreach (var h in b.FriendsNear(u.Position, 2)) b.Heal(h, 8);
                },
                Preview = (b, u, p, t) =>
                {
                    var foes = b.FoesNear(u.Position, 2);
                    return (foes.Count > 0 ? $"Hits {HitList(b, u, foes)}. " : "") +
                        $"Heals {Battle.Names(b.FriendsNear(u.Position, 2))} for up to 8.";
                },
            },
        };

        static Ability[] Rogue() => new[]
        {
            new Ability
            {
                Name = "Hamstring", Cooldown = 2, Target = TargetKind.AdjacentEnemy,
                Description = "Light hit that pushes the target 50 back on the timeline.",
                Run = (b, u, p, t) =>
                {
                    b.Strike(u, t, 3);
                    if (t.Alive)
                    {
                        t.NextTurnAt += 50;
                        b.Note($"{t.Name} is pushed 50 back on the timeline.");
                    }
                },
                Preview = (b, u, p, t) => $"Hits {t.Name} for {b.GetDamageInfo(u, t, 3).Damage} and pushes its next turn 50 later.",
            },
            new Ability
            {
                Name = "Poison Blade", Cooldown = 3, Target = TargetKind.AdjacentEnemy,
                Description = "Hit, then 3 poison damage at the start of the target's next 2 turns.",
                Run = (b, u, p, t) =>
                {
                    b.Strike(u, t, u.Damage);
                    if (t.Alive)
                    {
                        t.Poison = 2;
                        b.Note($"{t.Name} is poisoned.");
                    }
                },
                Preview = (b, u, p, t) => $"Hits {t.Name} for {b.GetDamageInfo(u, t).Damage}, then 3 poison damage on each of its next 2 turns.",
            },
            new Ability
            {
                Name = "Shadowstep", Cooldown = 3, Target = TargetKind.Enemy, Range = 5,
                Description = "Appear next to an enemy within 5 tiles and strike it.",
                Need = (b, u, e) => b.StepSpot(u, e).HasValue,
                Run = (b, u, p, t) =>
                {
                    b.Teleport(u, b.StepSpot(u, t).Value);
                    b.Strike(u, t, u.Damage);
                },
                Preview = (b, u, p, t) =>
                {
                    var info = b.GetDamageInfo(u, b.StepSpot(u, t).Value, t);
                    return $"Appears next to {t.Name} and hits for {info.Damage}, with a {Math.Round(info.CritChance * 100)}% chance of {info.CritDamage}.";
                },
            },
            new Ability
            {
                Name = "Eviscerate", IsUltimate = true, Target = TargetKind.AdjacentEnemy,
                Description = "A guaranteed critical hit.",
                Run = (b, u, p, t) =>
                {
                    int n = b.GetDamageInfo(u, t).CritDamage;
                    b.Note($"{u.Name} eviscerates {t.Name} for {n}, a critical.");
                    b.Hurt(t, n, true);
                },
                Preview = (b, u, p, t) => $"Hits {t.Name} for {b.GetDamageInfo(u, t).CritDamage}, always a critical.",
            },
        };

        static Ability[] Sorcerer() => new[]
        {
            new Ability
            {
                Name = "Firestorm", Cooldown = 3, Recovery = 40, Target = TargetKind.Tile, Range = 5, Area = 1,
                Description = "6 damage to everyone in a 3 by 3 area, allies too. Adds 40 recovery.",
                Run = (b, u, p, t) =>
                {
                    foreach (var v in b.Units.Where(v => v.Alive && Battle.InArea(v, p)).ToList()) b.Spell(v, 6, "Firestorm");
                },
                Preview = (b, u, p, t) => b.AreaPreview(p, 6, true) + " Adds 40 recovery.",
            },
            new Ability
            {
                Name = "Teleport", Cooldown = 3, Target = TargetKind.AnyTile,
                Description = "Appear on any open tile on the map.",
                Run = (b, u, p, t) =>
                {
                    b.Teleport(u, p);
                    b.Note($"{u.Name} teleports.");
                },
                Preview = (b, u, p, t) => "Teleport to this tile. Teleporting never gives free attacks.",
            },
            new Ability
            {
                Name = "Frost Nova", Cooldown = 3, Target = TargetKind.Self,
                Description = "3 damage to adjacent enemies, and they can't move on their next turn.",
                Need = (b, u, _) => b.FoesNear(u.Position, 1).Count > 0,
                Run = (b, u, p, t) =>
                {
                    foreach (var e in b.FoesNear(u.Position, 1))
                    {
                        b.Spell(e, 3, "Frost Nova");
                        if (e.Alive) e.Rooted = true;
                    }
                },
                Preview = (b, u, p, t) => $"Freezes {Battle.Names(b.FoesNear(u.Position, 1))}: 3 damage and no movement on their next turn.",
            },
            new Ability
            {
                Name = "Meteor", IsUltimate = true, Recovery = 40, Target = TargetKind.Tile, Range = 6, Area = 1,
                Description = "14 damage to every enemy in a 3 by 3 area. Allies are safe. Adds 40 recovery.",
                Run = (b, u, p, t) => { foreach (var e in b.FoesNear(p, 1)) b.Spell(e, 14, "Meteor"); },
                Preview = (b, u, p, t) => b.AreaPreview(p, 14, false) + " Adds 40 recovery.",
            },
        };

        static Ability[] Hunter() => new[]
        {
            new Ability
            {
                Name = "Pinning Shot", Cooldown = 2, Target = TargetKind.Enemy, Range = 6,
                Description = "Ranged hit. The target can't move on its next turn.",
                Run = (b, u, p, t) =>
                {
                    b.Strike(u, t, u.Damage);
                    if (t.Alive)
                    {
                        t.Rooted = true;
                        b.Note($"{t.Name} is pinned in place.");
                    }
                },
                Preview = (b, u, p, t) => $"Hits {t.Name} for {b.GetDamageInfo(u, t).Damage} and pins it for its next turn.",
            },
            new Ability
            {
                Name = "Hunter's Mark", Cooldown = 3, Target = TargetKind.Enemy, Range = 8,
                Description = "The target takes +3 damage from every hit until the end of its second turn.",
                Run = (b, u, p, t) =>
                {
                    t.Marked = 2;
                    b.Note($"{t.Name} is marked.");
                },
                Preview = (b, u, p, t) => $"Marks {t.Name}. Every hit on it deals +3 until the end of its second turn.",
            },
            new Ability
            {
                Name = "Volley", Cooldown = 3, Target = TargetKind.Tile, Range = 6, Area = 1,
                Description = "4 damage to every enemy in a 3 by 3 area.",
                Run = (b, u, p, t) => { foreach (var e in b.FoesNear(p, 1)) b.Spell(e, 4, "Volley"); },
                Preview = (b, u, p, t) => b.AreaPreview(p, 4, false),
            },
            new Ability
            {
                Name = "Rain of Arrows", IsUltimate = true, Target = TargetKind.Self, HighlightsSightFoes = true,
                Description = "7 damage to every enemy Wren can see within 8 tiles.",
                Need = (b, u, _) => b.SightFoes(u).Count > 0,
                Run = (b, u, p, t) => { foreach (var e in b.SightFoes(u)) b.Spell(e, 7, "Rain of Arrows"); },
                Preview = (b, u, p, t) => $"Hits {string.Join(", ", b.SightFoes(u).Select(e => $"{e.Name} for {b.SpellDamage(e, 7)}"))}.",
            },
        };

        static Ability[] Shaman() => new[]
        {
            new Ability
            {
                Name = "Chain Heal", Cooldown = 3, Recovery = 20, Target = TargetKind.Self,
                Description = "Heal Tova and every ally within 2 tiles for 7. Adds 20 recovery.",
                Need = (b, u, _) => b.FriendsNear(u.Position, 2).Any(h => h.Hp < h.MaxHp),
                Run = (b, u, p, t) => { foreach (var h in b.FriendsNear(u.Position, 2)) b.Heal(h, 7); },
                Preview = (b, u, p, t) => "Heals " + string.Join(", ", b.FriendsNear(u.Position, 2).Where(h => h.Hp < h.MaxHp)
                    .Select(h => $"{h.Name} for {Math.Min(7, h.MaxHp - h.Hp)}")) + ". Adds 20 recovery.",
            },
            new Ability
            {
                Name = "Chain Lightning", Cooldown = 2, Target = TargetKind.Enemy, Range = 4,
                Description = "5 damage to the target, then 4 to up to 2 more enemies within 2 tiles.",
                Run = (b, u, p, t) =>
                {
                    var chain = b.LightningChain(t);
                    for (int i = 0; i < chain.Count; i++) b.Spell(chain[i], i == 0 ? 5 : 4, "Chain Lightning");
                },
                Preview = (b, u, p, t) => "Hits " + string.Join(", then ",
                    b.LightningChain(t).Select((e, i) => $"{e.Name} for {b.SpellDamage(e, i == 0 ? 5 : 4)}")) + ".",
            },
            new Ability
            {
                Name = "Earthbind", Cooldown = 3, Target = TargetKind.Tile, Range = 5, Area = 1,
                Description = "Every enemy in a 3 by 3 area can't move on its next turn.",
                Run = (b, u, p, t) =>
                {
                    foreach (var e in b.FoesNear(p, 1))
                    {
                        e.Rooted = true;
                        b.Note($"{e.Name} is bound by the earth.");
                    }
                },
                Preview = (b, u, p, t) =>
                {
                    var foes = b.FoesNear(p, 1);
                    return foes.Count > 0 ? $"Pins {Battle.Names(foes)} for their next turn." : "No enemies in the area.";
                },
            },
            new Ability
            {
                Name = "Ancestral Call", IsUltimate = true, Target = TargetKind.Self,
                Description = "Heal every hero for 15. The others act 30 sooner.",
                Run = (b, u, p, t) =>
                {
                    foreach (var h in b.Heroes.ToList())
                    {
                        b.Heal(h, 15);
                        if (h != u) b.Hasten(h, 30);
                    }
                },
                Preview = (b, u, p, t) => "Heals every hero for up to 15, and the others act 30 sooner.",
            },
        };

        static Ability[] Priest() => new[]
        {
            new Ability
            {
                Name = "Holy Light", Cooldown = 2, Target = TargetKind.Ally, Range = 4,
                Description = "Heal Sera or an ally within 4 tiles for 10 and cure poison.",
                Need = (b, u, h) => h.Hp < h.MaxHp || h.Poison > 0,
                Run = (b, u, p, t) => b.Heal(t, 10, u),
                Preview = (b, u, p, t) => $"Heals {t.Name} for {Math.Min(10, t.MaxHp - t.Hp)}" +
                    (t.Poison > 0 ? " and cures the poison." : "."),
            },
            new Ability
            {
                Name = "Resurrection", Cooldown = 6, Recovery = 30, Target = TargetKind.FallenAlly, Range = 3,
                Description = "Bring a fallen ally within 3 tiles back at half HP. Adds 30 recovery.",
                Need = (b, u, h) => b.FreeTileNear(h.Position).HasValue,
                Run = (b, u, p, t) =>
                {
                    var h = b.FallenAt(p);
                    b.Revive(h, b.FreeTileNear(p).Value, Math.Max(1, h.MaxHp / 2));
                },
                Preview = (b, u, p, t) =>
                {
                    var h = b.FallenAt(p);
                    return $"{h.Name} gets back up with {Math.Max(1, h.MaxHp / 2)} HP and takes a turn soon after. Adds 30 recovery.";
                },
            },
            new Ability
            {
                Name = "Sanctuary", Cooldown = 3, Target = TargetKind.Self, Area = 2,
                Description = "Sera and every ally within 2 tiles get +2 Armor until their next turn.",
                Run = (b, u, p, t) =>
                {
                    foreach (var h in b.FriendsNear(u.Position, 2)) h.ArmorBuff = Math.Max(h.ArmorBuff, 2);
                    b.Note("A circle of light shields the squad.");
                },
                Preview = (b, u, p, t) => $"{Battle.Names(b.FriendsNear(u.Position, 2))} get +2 Armor until their next turn.",
            },
            new Ability
            {
                Name = "Mass Resurrection", IsUltimate = true, Recovery = 30, Target = TargetKind.Self,
                Description = "Every fallen hero gets back up next to Sera with a third of their HP. Everyone else heals for 10.",
                Need = (b, u, _) => b.FallenHeroes.Any() || b.Heroes.Any(h => h.Hp < h.MaxHp || h.Poison > 0),
                Run = (b, u, p, t) =>
                {
                    var standing = b.Heroes.ToList();
                    foreach (var h in b.FallenHeroes.ToList())
                    {
                        var spot = b.FreeTileNear(u.Position);
                        if (spot.HasValue) b.Revive(h, spot.Value, Math.Max(1, h.MaxHp / 3));
                    }
                    foreach (var h in standing) b.Heal(h, 10, u);
                },
                Preview = (b, u, p, t) =>
                {
                    var fallen = b.FallenHeroes.ToList();
                    return (fallen.Count > 0 ? $"Revives {Battle.Names(fallen)}. " : "") +
                        $"Heals {Battle.Names(b.Heroes)} for up to 10. Adds 30 recovery.";
                },
            },
        };
    }
}
