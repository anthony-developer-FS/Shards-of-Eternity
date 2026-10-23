using System;
using System.Collections.Generic;
using System.Linq;

namespace ShardsOfEternity.Core
{
    /// <summary>What a run paid out, for the end screen.</summary>
    public sealed class RunResult
    {
        public bool Won;
        public int Xp;
        public int Kills;
        public int Floor;
        public readonly List<HeroLevelUp> Heroes = new List<HeroLevelUp>();
        /// <summary>Items found this run that went into the vault or stayed equipped.</summary>
        public readonly List<GearItem> Kept = new List<GearItem>();
        /// <summary>Items found this run that were lost because the squad fell while they sat in the stash.</summary>
        public readonly List<GearItem> Lost = new List<GearItem>();
    }

    public struct HeroLevelUp
    {
        public string Id;
        public int LevelBefore;
        public int LevelAfter;
        public int Xp;
        public int XpToNext;

        public bool LeveledUp => LevelAfter > LevelBefore;
    }

    /// <summary>
    /// Everything that lasts between runs: hero levels, the vault of spare gear, each hero's equipped gear and a few
    /// lifetime stats. A run starts from a copy of it and pays back into it when it ends (<see cref="Settle"/>).
    /// </summary>
    public sealed class Profile
    {
        public const int FormatVersion = 1;

        /// <summary>Most items the vault holds. The oldest, weakest items go first when it overflows.</summary>
        public const int VaultCap = 200;

        /// <summary>Victory keeps every find. On a defeat, finds still in the stash are lost unless this is on.</summary>
        public bool KeepStashOnDefeat;

        readonly Dictionary<string, HeroProgress> heroes = new Dictionary<string, HeroProgress>();

        public Dictionary<string, Loadout> Loadouts { get; } = new Dictionary<string, Loadout>();
        /// <summary>Spare gear, equipped from the Armory between runs.</summary>
        public List<GearItem> Vault { get; } = new List<GearItem>();

        public int RunsPlayed;
        public int RunsWon;
        public int DeepestFloor;
        public int EnemiesSlain;

        public IEnumerable<HeroProgress> Heroes => heroes.Values;

        public HeroProgress Hero(string id)
        {
            if (!heroes.TryGetValue(id, out var h)) heroes[id] = h = new HeroProgress(id);
            return h;
        }

        public Loadout LoadoutFor(string heroId)
        {
            if (!Loadouts.TryGetValue(heroId, out var l)) Loadouts[heroId] = l = new Loadout();
            return l;
        }

        /// <summary>Equips a vault item on a hero; anything it replaces goes back to the vault.</summary>
        public void Equip(string heroId, GearItem item)
        {
            if (!Vault.Remove(item)) return;
            var old = LoadoutFor(heroId).Equip(item);
            if (old != null) Vault.Add(old);
        }

        public void Unequip(string heroId, GearSlot slot)
        {
            var old = LoadoutFor(heroId).Unequip(slot);
            if (old != null) Vault.Add(old);
        }

        public void Discard(GearItem item) => Vault.Remove(item);

        /// <summary>Starts a run with the squad's levels and a copy of their equipped gear.</summary>
        public DungeonRun StartRun(RunKind kind, IEnumerable<string> squad, int seed, Theme theme = null, bool handmadeFirstFloor = false,
            TurnRules rules = null)
        {
            var run = new DungeonRun(kind, squad, seed, theme, handmadeFirstFloor);
            if (rules != null) run.Rules = rules;
            foreach (var id in run.Squad)
            {
                run.Levels[id] = Hero(id).Level;
                run.Loadouts[id] = LoadoutFor(id).Clone();
            }
            return run;
        }

        /// <summary>
        /// Pays a finished (or abandoned) run back into the profile: XP to every hero in the squad, their gear as it
        /// was at the end, and the stash into the vault. A defeat loses this run's finds that were never equipped.
        /// Gear the heroes brought with them always comes back. Paying out a run twice does nothing.
        /// </summary>
        public RunResult Settle(DungeonRun run)
        {
            if (run == null || run.Settled) return null;
            run.Settled = true;
            run.CollectLoot();
            var result = new RunResult { Won = run.Won, Xp = run.Xp, Kills = run.Kills, Floor = run.Floor };
            var found = new HashSet<GearItem>(run.Found);
            bool keepStash = result.Won || KeepStashOnDefeat;

            foreach (var id in run.Squad)
            {
                var loadout = run.LoadoutFor(id).Clone();
                Loadouts[id] = loadout;
                result.Kept.AddRange(loadout.Items.Where(found.Contains));
            }
            foreach (var item in run.Stash)
            {
                if (!found.Contains(item) || keepStash)
                {
                    Vault.Add(item);
                    if (found.Contains(item)) result.Kept.Add(item);
                }
                else result.Lost.Add(item);
            }
            TrimVault();

            foreach (var id in run.Squad)
            {
                var hero = Hero(id);
                int before = hero.Level;
                hero.AddXp(result.Xp);
                result.Heroes.Add(new HeroLevelUp { Id = id, LevelBefore = before, LevelAfter = hero.Level, Xp = hero.Xp, XpToNext = hero.XpToNext });
            }

            RunsPlayed++;
            if (result.Won) RunsWon++;
            DeepestFloor = Math.Max(DeepestFloor, run.Floor);
            EnemiesSlain += result.Kills;
            return result;
        }

        void TrimVault()
        {
            if (Vault.Count <= VaultCap) return;
            var drop = Vault.Select((item, index) => (item, index))
                .OrderBy(x => x.item.Rarity).ThenBy(x => x.item.ItemLevel).ThenBy(x => x.index)
                .Take(Vault.Count - VaultCap).Select(x => x.item).ToList();
            foreach (var item in drop) Vault.Remove(item);
        }

        // ---------- save format ----------

        public string ToJson()
        {
            var root = new Dictionary<string, object>
            {
                ["version"] = FormatVersion,
                ["stats"] = new Dictionary<string, object>
                {
                    ["runsPlayed"] = RunsPlayed,
                    ["runsWon"] = RunsWon,
                    ["deepestFloor"] = DeepestFloor,
                    ["enemiesSlain"] = EnemiesSlain,
                },
                ["heroes"] = heroes.Values.OrderBy(h => h.Id).ToDictionary(h => h.Id,
                    h => (object)new Dictionary<string, object> { ["level"] = h.Level, ["xp"] = h.Xp }),
                ["loadouts"] = Loadouts.Where(kv => kv.Value.Items.Any()).OrderBy(kv => kv.Key).ToDictionary(kv => kv.Key,
                    kv => (object)kv.Value.Slots.OrderBy(s => s.Key).ToDictionary(s => s.Key.ToString(), s => (object)ItemToJson(s.Value))),
                ["vault"] = Vault.Select(ItemToJson).ToList(),
            };
            return Json.Write(root);
        }

        static Dictionary<string, object> ItemToJson(GearItem item) => new Dictionary<string, object>
        {
            ["name"] = item.Name,
            ["slot"] = item.Slot.ToString(),
            ["rarity"] = item.Rarity.ToString(),
            ["level"] = item.ItemLevel,
            ["stats"] = item.Stats.Select(s => (object)new Dictionary<string, object> { ["stat"] = s.Stat.ToString(), ["value"] = s.Value }).ToList(),
        };

        /// <summary>Reads a save. Throws <see cref="FormatException"/> if the text is not a profile this version can read.</summary>
        public static Profile FromJson(string text)
        {
            try
            {
                var root = Json.Parse(text) as Dictionary<string, object> ?? throw new FormatException("The save is not a JSON object.");
                int version = Int(root, "version");
                if (version < 1 || version > FormatVersion) throw new FormatException($"Unknown save version {version}.");

                var p = new Profile();
                if (root.TryGetValue("stats", out var statsValue) && statsValue is Dictionary<string, object> stats)
                {
                    p.RunsPlayed = Int(stats, "runsPlayed");
                    p.RunsWon = Int(stats, "runsWon");
                    p.DeepestFloor = Int(stats, "deepestFloor");
                    p.EnemiesSlain = Int(stats, "enemiesSlain");
                }
                foreach (var kv in Obj(root, "heroes"))
                {
                    var h = (Dictionary<string, object>)kv.Value;
                    var hero = p.Hero(kv.Key);
                    hero.Level = Math.Max(1, Math.Min(Progression.MaxLevel, Int(h, "level")));
                    hero.Xp = Math.Max(0, Int(h, "xp"));
                }
                foreach (var kv in Obj(root, "loadouts"))
                {
                    var loadout = p.LoadoutFor(kv.Key);
                    foreach (var slot in (Dictionary<string, object>)kv.Value)
                        loadout.Put(Enum<GearSlot>(slot.Key), ItemFromJson((Dictionary<string, object>)slot.Value));
                }
                if (root.TryGetValue("vault", out var vault) && vault is List<object> items)
                    foreach (var item in items) p.Vault.Add(ItemFromJson((Dictionary<string, object>)item));
                return p;
            }
            catch (Exception e) when (e is InvalidCastException || e is KeyNotFoundException || e is NullReferenceException || e is ArgumentException)
            {
                throw new FormatException("The save file is damaged: " + e.Message, e);
            }
        }

        static GearItem ItemFromJson(Dictionary<string, object> o)
        {
            var item = new GearItem
            {
                Name = (string)o["name"],
                Slot = Enum<GearSlot>((string)o["slot"]),
                Rarity = Enum<Rarity>((string)o["rarity"]),
                ItemLevel = Int(o, "level"),
            };
            if (o.TryGetValue("stats", out var stats) && stats is List<object> list)
                foreach (Dictionary<string, object> s in list)
                    item.Stats.Add(new StatRoll(Enum<StatType>((string)s["stat"]), (double)s["value"]));
            return item;
        }

        static int Int(Dictionary<string, object> o, string key) =>
            o.TryGetValue(key, out var v) && v is double d ? (int)Math.Round(d) : 0;

        static Dictionary<string, object> Obj(Dictionary<string, object> o, string key) =>
            o.TryGetValue(key, out var v) && v is Dictionary<string, object> d ? d : new Dictionary<string, object>();

        static T Enum<T>(string name) where T : struct =>
            System.Enum.TryParse<T>(name, out var value) && System.Enum.IsDefined(typeof(T), value)
                ? value
                : throw new FormatException($"Unknown {typeof(T).Name} '{name}'.");
    }
}
