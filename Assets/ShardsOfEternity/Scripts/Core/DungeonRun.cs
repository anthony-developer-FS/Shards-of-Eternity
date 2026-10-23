using System.Collections.Generic;
using System.Linq;

namespace ShardsOfEternity.Core
{
    public enum RunKind
    {
        /// <summary>3 to 4 floors.</summary>
        Dungeon,
        /// <summary>12 floors.</summary>
        Raid,
    }

    /// <summary>
    /// A descent through several floors. Each floor is a fresh generated room; the squad's health, deaths and
    /// ultimate charge carry over. The last floor holds the boss, and beating it wins the run.
    /// </summary>
    public sealed class DungeonRun
    {
        public const int RaidFloors = 12;

        readonly int seed;
        readonly List<string> squad;
        readonly bool handmadeFirstFloor;

        public RunKind Kind { get; }
        /// <summary>The theme of the first floor. Raids move to the next theme every <see cref="FloorsPerTheme"/> floors.</summary>
        public Theme Theme { get; }
        public const int FloorsPerTheme = 3;

        /// <summary>Theme of a 1-based floor: dungeons keep one theme, raids rotate through them all.</summary>
        public Theme ThemeFor(int floor)
        {
            if (Kind != RunKind.Raid) return Theme;
            int start = System.Array.IndexOf(Theme.All, Theme);
            if (start < 0) return Theme;
            return Theme.All[(start + (floor - 1) / FloorsPerTheme) % Theme.All.Length];
        }
        public int FloorCount { get; }
        /// <summary>1-based number of the floor being played.</summary>
        public int Floor { get; private set; }
        public Battle Battle { get; private set; }
        public TurnRules Rules { get; set; } = TurnRules.Classic;
        /// <summary>Items picked up and not equipped.</summary>
        public List<GearItem> Stash { get; } = new List<GearItem>();
        /// <summary>Every item picked up this run, in order, for the end screen.</summary>
        public List<GearItem> Found { get; } = new List<GearItem>();
        /// <summary>Equipped gear by hero id. Applied to the heroes at the start of each floor.</summary>
        public Dictionary<string, Loadout> Loadouts { get; } = new Dictionary<string, Loadout>();

        public IReadOnlyList<string> Squad => squad;

        public Loadout LoadoutFor(string heroId)
        {
            if (!Loadouts.TryGetValue(heroId, out var l)) Loadouts[heroId] = l = new Loadout();
            return l;
        }

        /// <summary>Moves what the squad picked up on the current floor into the stash. Safe to call more than once.</summary>
        public void CollectLoot()
        {
            if (Battle == null) return;
            Stash.AddRange(Battle.PickedUp);
            Found.AddRange(Battle.PickedUp);
            Battle.PickedUp.Clear();
        }

        /// <summary>Equips a stash item on a hero; anything it replaces goes back to the stash.</summary>
        public void Equip(string heroId, GearItem item)
        {
            if (!Stash.Remove(item)) return;
            var old = LoadoutFor(heroId).Equip(item);
            if (old != null) Stash.Add(old);
        }

        public void Unequip(string heroId, GearSlot slot)
        {
            var old = LoadoutFor(heroId).Unequip(slot);
            if (old != null) Stash.Add(old);
        }

        public bool IsFinalFloor => Floor == FloorCount;

        /// <param name="handmadeFirstFloor">Use the browser prototype's room for floor 1.</param>
        public DungeonRun(RunKind kind, IEnumerable<string> squad, int seed, Theme theme = null, bool handmadeFirstFloor = false)
        {
            this.seed = seed;
            this.squad = squad.ToList();
            this.handmadeFirstFloor = handmadeFirstFloor;
            var rng = new System.Random(seed);
            Kind = kind;
            Theme = theme ?? Theme.All[rng.Next(Theme.All.Length)];
            FloorCount = kind == RunKind.Raid ? RaidFloors : 3 + rng.Next(2);
        }

        /// <summary>Builds and starts the next floor. Returns false if the run is already on its last floor.</summary>
        public bool NextFloor()
        {
            if (Floor >= FloorCount) return false;
            CollectLoot();
            var previous = Battle;
            Floor++;
            int floorSeed = unchecked(seed + Floor * 7919);
            RoomLayout room;
            if (Floor == 1 && handmadeFirstFloor && !IsFinalFloor) room = RoomLayout.Prototype();
            else room = RoomGenerator.Generate(floorSeed, ThemeFor(Floor), bossFloor: IsFinalFloor, floor: Floor);
            room.IsFinalFloor = IsFinalFloor;
            room.Floor = Floor;

            Battle = new Battle(room, squad, floorSeed) { Rules = Rules, LootEnabled = true };
            foreach (var hero in Battle.Units.Where(u => u.IsHero)) Gear.ApplyTo(hero, LoadoutFor(hero.Def.Id));
            if (previous != null) Battle.CarryOver(previous.Units.Where(u => u.IsHero));
            Battle.Start();
            return true;
        }
    }
}
