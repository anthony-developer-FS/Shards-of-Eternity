using System;
using System.Collections.Generic;
using System.Linq;

namespace ShardsOfEternity.Core
{
    public enum BattleOutcome
    {
        None,
        /// <summary>The final floor's boss and its room are cleared: the run is won.</summary>
        Victory,
        Defeat,
        /// <summary>A hero walked through the open exit. Load the next floor.</summary>
        FloorComplete,
    }

    /// <summary>
    /// Turn budget rules. Classic: one move and one action per turn. Action shards: a pool of shards per turn,
    /// where moving, attacking and using an ability each cost one, in any mix (move twice, attack twice...).
    /// </summary>
    public sealed class TurnRules
    {
        public bool UseActionShards;
        public int ShardsPerTurn = 2;
        /// <summary>Most shards a hero can hold. Above <see cref="ShardsPerTurn"/>, unspent shards bank for later turns.</summary>
        public int ShardCap = 2;

        public static TurnRules Classic => new TurnRules();
    }

    /// <summary>Walls that will appear and disappear at the next wall shift.</summary>
    public sealed class WallShift
    {
        public readonly HashSet<Point> Add = new HashSet<Point>();
        public readonly HashSet<Point> Remove = new HashSet<Point>();
    }

    /// <summary>
    /// One combat room: the board, the units, the timeline and the rift pressure clock.
    /// Pure C# with no Unity dependency, so it can be unit tested and reused by any front end.
    /// Rules resolve instantly and are reported as <see cref="BattleEvent"/>s for the view to animate.
    /// </summary>
    public sealed partial class Battle
    {
        /// <summary>Timeline points per round of rift pressure.</summary>
        public const int RoundLength = 80;
        /// <summary>Pressure level of the first wall shift. Shifts repeat every 4 levels, telegraphed one level ahead.</summary>
        public const int FirstShift = 6;

        public const int UltimateMax = 100;

        readonly GridMap baseMap;
        readonly Random rng;
        readonly List<BattleEvent> events = new List<BattleEvent>();
        int nextUnitId;
        double combatStart;

        public RoomLayout Layout { get; }
        /// <summary>The live board. Walls change when the room shifts.</summary>
        public GridMap Map { get; }
        public List<Unit> Units { get; } = new List<Unit>();
        public Unit Current { get; private set; }
        public double Clock { get; private set; }
        public bool InCombat { get; private set; }
        public int Pressure { get; private set; }
        public WallShift PendingShift { get; private set; }
        public bool ExitOpen { get; private set; }
        /// <summary>Every enemy is dead. On a floor before the last one, the squad now walks to the exit.</summary>
        public bool FloorCleared { get; private set; }
        /// <summary>Reinforcement waves called so far on this floor.</summary>
        public int WavesCalled { get; private set; }
        public TurnRules Rules { get; set; } = TurnRules.Classic;
        /// <summary>When on, slain enemies drop gear onto their tile. Runs turn this on.</summary>
        public bool LootEnabled { get; set; }
        /// <summary>Items lying on the floor, waiting for a hero to walk over them.</summary>
        public Dictionary<Point, List<GearItem>> GroundLoot { get; } = new Dictionary<Point, List<GearItem>>();
        /// <summary>Items heroes picked up on this floor.</summary>
        public List<GearItem> PickedUp { get; } = new List<GearItem>();
        public int GroundLootCount => GroundLoot.Values.Sum(l => l.Count);
        public BattleOutcome Outcome { get; private set; }
        public bool IsOver => Outcome != BattleOutcome.None;
        /// <summary>Newest first, like the prototype's log.</summary>
        public List<string> Log { get; } = new List<string>();

        public int Width => Map.Width;
        public int Height => Map.Height;

        /// <summary>Optional override for crit rolls, so tests can force or forbid crits.</summary>
        public Func<double> Roll;

        public Battle(RoomLayout layout, IEnumerable<string> squad, int seed)
        {
            Layout = layout;
            baseMap = layout.Map.Clone();
            Map = layout.Map.Clone();
            rng = new Random(seed);
            Roll = rng.NextDouble;

            int slot = 0;
            foreach (var id in squad)
            {
                if (slot >= layout.HeroSlots.Count) break;
                AddUnit(Roster.Hero(id), layout.HeroSlots[slot++]);
            }
            foreach (var spawn in layout.Enemies)
                AddUnit(Roster.Enemy(spawn.Kind), spawn.Position);
        }

        public Unit AddUnit(UnitDef def, Point position)
        {
            var u = new Unit(nextUnitId++, def, position);
            if (u.IsEnemy) ScaleForFloor(u, Layout.Floor);
            Units.Add(u);
            return u;
        }

        /// <summary>Deeper floors make enemies tougher: +8% Health per floor and +1 Damage every 3 floors.</summary>
        public static void ScaleForFloor(Unit u, int floor)
        {
            int depth = Math.Max(0, floor - 1);
            u.MaxHp = u.Hp = (int)Math.Round(u.Def.Hp * (1 + 0.08 * depth));
            u.Damage = u.Def.Damage + depth / 3;
        }

        /// <summary>
        /// Brings heroes' damage taken, deaths and ultimate charge over from the previous floor. Cooldowns and shards
        /// reset. Damage carries rather than raw HP, so gear that raises Health between floors adds to current HP too.
        /// </summary>
        public void CarryOver(IEnumerable<Unit> previousHeroes)
        {
            foreach (var old in previousHeroes)
            {
                var hero = Units.FirstOrDefault(u => u.IsHero && u.Def == old.Def);
                if (hero == null) continue;
                hero.Alive = old.Alive;
                hero.Hp = old.Alive ? Math.Max(1, hero.MaxHp - (old.MaxHp - old.Hp)) : 0;
                hero.Ultimate = old.Ultimate;
            }
        }

        // ---------- loot ----------

        void DropLoot(Unit enemy)
        {
            if (!LootEnabled) return;
            foreach (var item in LootTable.RollDrops(rng, enemy, Layout.Floor))
            {
                var p = enemy.Position;
                if (!GroundLoot.TryGetValue(p, out var pile)) GroundLoot[p] = pile = new List<GearItem>();
                pile.Add(item);
                Say($"{enemy.Name} drops {item.Name}.");
                Emit(new BattleEvent { Type = BattleEventType.LootDropped, Unit = enemy, To = p, Item = item });
            }
        }

        /// <summary>Heroes pick up everything on a tile they walk through or land on.</summary>
        void PickUp(Unit u, Point p)
        {
            if (!u.IsHero || !GroundLoot.TryGetValue(p, out var pile)) return;
            GroundLoot.Remove(p);
            foreach (var item in pile)
            {
                PickedUp.Add(item);
                Say($"{u.Name} picks up {item.Name}.");
                Emit(new BattleEvent { Type = BattleEventType.LootPickedUp, Unit = u, To = p, Item = item });
            }
        }

        /// <summary>Begins the battle and starts the first turn.</summary>
        public void Start()
        {
            Say(Layout.HasBoss
                ? "The rift's heart. Something large is waiting on the far side."
                : "Your squad enters the rift. The enemies on the far side are asleep.");
            NextTurn();
        }

        /// <summary>Returns and clears the events produced since the last call.</summary>
        public List<BattleEvent> DrainEvents()
        {
            var list = new List<BattleEvent>(events);
            events.Clear();
            return list;
        }

        void Emit(BattleEvent e) => events.Add(e);

        /// <summary>Adds a line to the battle log. Abilities use this to describe their effects.</summary>
        public void Note(string text) => Say(text);

        void Say(string text)
        {
            Log.Insert(0, text);
            if (Log.Count > 40) Log.RemoveAt(Log.Count - 1);
            Emit(new BattleEvent { Type = BattleEventType.Log, Text = text });
        }

        public IEnumerable<Unit> Heroes => Units.Where(u => u.Alive && u.IsHero);
        public IEnumerable<Unit> Enemies => Units.Where(u => u.Alive && u.IsEnemy);

        // ---------- turn flow ----------

        static int TurnOrder(Unit a, Unit b, double ta, double tb)
        {
            int c = ta.CompareTo(tb);
            if (c != 0) return c;
            c = b.Speed.CompareTo(a.Speed);
            if (c != 0) return c;
            c = a.IsEnemy.CompareTo(b.IsEnemy);
            return c != 0 ? c : a.Id.CompareTo(b.Id);
        }

        List<Unit> ActiveUnits()
        {
            var live = Units.Where(u => u.Alive && (u.IsHero || u.Awake)).ToList();
            live.Sort((a, b) => TurnOrder(a, b, a.NextTurnAt, b.NextTurnAt));
            return live;
        }

        void NextTurn()
        {
            while (!IsOver)
            {
                var live = ActiveUnits();
                if (live.Count == 0) return;
                var u = live[0];
                Clock = u.NextTurnAt;
                Current = u;
                TickPressure();
                if (IsOver) return;

                u.Moved = false;
                u.Acted = false;
                for (int i = 0; i < u.Cooldowns.Length; i++) u.Cooldowns[i] = Math.Max(0, u.Cooldowns[i] - 1);
                u.ArmorBuff = 0;
                if (u.IsHero && InCombat) ChargeUltimate(u, 20);
                if (u.IsHero && Rules.UseActionShards) u.Shards = Math.Min(Rules.ShardCap, u.Shards + Rules.ShardsPerTurn);

                if (u.Poison > 0)
                {
                    u.Poison--;
                    Say($"Poison eats at {u.Name} for 3.");
                    Hurt(u, 3, false);
                    if (IsOver) return;
                    if (!u.Alive) continue;
                }

                Emit(new BattleEvent { Type = BattleEventType.TurnStarted, Unit = u });
                return;
            }
        }

        /// <summary>Ends the current unit's turn and starts the next one on the timeline.</summary>
        public void EndTurn()
        {
            var u = Current;
            if (u != null)
            {
                if (u.Alive) u.NextTurnAt += u.TurnInterval + u.Recovery;
                u.Recovery = 0;
                if (u.Marked > 0) u.Marked--;
            }
            NextTurn();
        }

        /// <summary>True when the current hero has nothing left to spend, so the turn should end on its own.</summary>
        public bool CurrentTurnSpent => Current != null && (!Current.Alive || (Current.IsHero && !CanMove(Current) && !CanAct(Current)));

        /// <summary>Whether the hero still has a move this turn (a shard, or the classic move).</summary>
        public bool CanMove(Unit u) => Rules.UseActionShards ? u.Shards > 0 : !u.Moved;

        /// <summary>Whether the hero can still attack or use an ability this turn.</summary>
        public bool CanAct(Unit u) => Rules.UseActionShards ? u.Shards > 0 : !u.Acted;

        void SpendMove(Unit u)
        {
            u.Moved = true;
            if (Rules.UseActionShards) u.Shards = Math.Max(0, u.Shards - 1);
        }

        void SpendAction(Unit u)
        {
            u.Acted = true;
            if (Rules.UseActionShards) u.Shards = Math.Max(0, u.Shards - 1);
        }

        public struct TimelineEntry
        {
            public Unit Unit;
            public double At;
        }

        /// <summary>The next <paramref name="count"/> turns, starting with the current one.</summary>
        public List<TimelineEntry> Forecast(int count)
        {
            var live = ActiveUnits().Select(u => new TimelineEntry { Unit = u, At = u.NextTurnAt }).ToList();
            var output = new List<TimelineEntry>();
            bool first = true;
            while (output.Count < count && live.Count > 0)
            {
                live.Sort((a, b) => TurnOrder(a.Unit, b.Unit, a.At, b.At));
                var f = live[0];
                output.Add(f);
                f.At += f.Unit.TurnInterval + (first && f.Unit == Current ? f.Unit.Recovery : 0);
                live[0] = f;
                first = false;
            }
            return output;
        }

        void ChargeUltimate(Unit u, int amount) => u.Ultimate = Math.Min(UltimateMax, u.Ultimate + amount);

        // ---------- waking ----------

        void CheckWake()
        {
            if (InCombat) return;
            var heroes = Heroes.ToList();
            bool seen = Units.Any(e => e.Alive && e.IsEnemy && !e.Awake &&
                heroes.Any(h => Point.Distance(e.Position, h.Position) <= 5 && LineOfSight(h.X, h.Y, e.X, e.Y)));
            if (seen) WakeAll();
        }

        void WakeAll()
        {
            if (InCombat) return;
            InCombat = true;
            combatStart = Clock;
            foreach (var u in Units)
            {
                if (u.IsEnemy && u.Alive && !u.Awake)
                {
                    u.Awake = true;
                    u.NextTurnAt = Clock + u.TurnInterval;
                }
            }
            Say("The room wakes. Enemies join the timeline.");
            Emit(new BattleEvent { Type = BattleEventType.RoomWoke });
        }

        void CheckEnd()
        {
            if (IsOver) return;
            if (!Heroes.Any())
            {
                Outcome = BattleOutcome.Defeat;
                Say("Your squad has fallen.");
            }
            else if (InCombat && !FloorCleared && !Enemies.Any())
            {
                FloorCleared = true;
                ExitOpen = true;
                PendingShift = null;
                if (Layout.IsFinalFloor)
                {
                    Outcome = BattleOutcome.Victory;
                    // The run is over, so the squad gathers whatever is still on the floor.
                    foreach (var pile in GroundLoot.Values) PickedUp.AddRange(pile);
                    GroundLoot.Clear();
                    Say(Layout.HasBoss ? "The rift lord falls. The rift is sealed." : "Room cleared. The rift door opens.");
                }
                else
                {
                    Say("Room cleared. The rift door opens. Walk a hero through it to go deeper.");
                    Emit(new BattleEvent { Type = BattleEventType.FloorCleared });
                }
            }
            if (IsOver) Emit(new BattleEvent { Type = BattleEventType.BattleOver });
        }

        // ---------- rift pressure ----------

        void TickPressure()
        {
            if (!InCombat || FloorCleared) return;
            int p = (int)Math.Floor((Clock - combatStart) / RoundLength);
            while (Pressure < p && !IsOver)
            {
                Pressure++;
                OnPressure(Pressure);
            }
        }

        void OnPressure(int p)
        {
            if (p == 2) Say("Rift pressure 2. The walls begin to hum.");
            if (p >= FirstShift - 1 && (p - FirstShift + 1) % 4 == 0) PlanShift(p);
            if (p >= FirstShift && (p - FirstShift) % 4 == 0) DoShift();
        }

        /// <summary>A short description of the next rift event, for the HUD.</summary>
        public string NextPressureEvent()
        {
            if (FloorCleared) return "The rift is quiet";
            if (!InCombat) return "Rises once the room wakes";
            int p = Pressure;
            int nextShift = p < FirstShift ? FirstShift : FirstShift + 4 * (int)Math.Ceiling((p - FirstShift + 1) / 4.0);
            return $"Wall shift at {nextShift}";
        }

        /// <summary>True while reinforcements can arrive: the room is awake and not yet cleared.</summary>
        public bool ReinforcementsActive => InCombat && !FloorCleared && !IsOver;

        Point? FreeNear(Point c)
        {
            for (int r = 0; r < 5; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int x = c.X + dx, y = c.Y + dy;
                        if (Map.InBounds(x, y) && !IsWall(x, y) && Map[x, y] != TileType.Spikes && Map[x, y] != TileType.Exit &&
                            UnitAt(x, y) == null)
                            return new Point(x, y);
                    }
            return null;
        }

        /// <summary>
        /// Sends a reinforcement wave out of the rift spawn. The front end calls this on a real-time countdown
        /// (see <see cref="ReinforcementSchedule"/>), between turns.
        /// </summary>
        public void CallReinforcements()
        {
            if (!ReinforcementsActive) return;
            var spawn = Map.Find(TileType.Spawn);
            if (spawn == null) return;
            WavesCalled++;
            var kinds = WavesCalled > 1 ? Layout.Theme.LaterWaves : Layout.Theme.FirstWave;
            foreach (var kind in kinds)
            {
                var pos = FreeNear(spawn.Value);
                if (pos == null) continue;
                var u = AddUnit(Roster.Enemy(kind), pos.Value);
                u.Awake = true;
                u.IsReinforcement = true;
                u.NextTurnAt = Clock + u.TurnInterval;
                Emit(new BattleEvent { Type = BattleEventType.Spawned, Unit = u });
            }
            Say("Reinforcements claw out of the rift.");
        }

        bool StaticWall(int x, int y) => Map.IsBorder(x, y) || baseMap[x, y] == TileType.Pillar;

        HashSet<Point> InteriorWalls()
        {
            var set = new HashSet<Point>();
            for (int y = 1; y < Height - 1; y++)
                for (int x = 1; x < Width - 1; x++)
                    if (Map[x, y] == TileType.Wall) set.Add(new Point(x, y));
            return set;
        }

        /// <summary>True if every open interior tile can still be reached from the exit.</summary>
        bool Connected(HashSet<Point> walls) =>
            RoomGenerator.AllOpenTilesConnected(Map.Width, Map.Height, (x, y) => StaticWall(x, y) || walls.Contains(new Point(x, y)),
                Map.Find(TileType.Exit));

        bool OkWallTile(int x, int y)
        {
            if (StaticWall(x, y)) return false;
            var t = Map[x, y];
            if (t == TileType.Spikes || t == TileType.Spawn || t == TileType.Exit || UnitAt(x, y) != null) return false;
            if (GroundLoot.ContainsKey(new Point(x, y))) return false;
            var exit = Map.Find(TileType.Exit);
            var spawn = Map.Find(TileType.Spawn);
            var p = new Point(x, y);
            return (exit == null || Point.Distance(p, exit.Value) > 1) && (spawn == null || Point.Distance(p, spawn.Value) > 1);
        }

        void PlanShift(int p)
        {
            var old = InteriorWalls();
            for (int tries = 0; tries < 300; tries++)
            {
                var walls = new HashSet<Point>();
                int segments = 0, guard = 0;
                while (segments < 3 && guard++ < 60)
                {
                    int x = 1 + rng.Next(Width - 2), y = 1 + rng.Next(Height - 2);
                    bool horizontal = rng.NextDouble() < .5;
                    int length = 2 + rng.Next(2);
                    var tiles = new List<Point>();
                    bool ok = true;
                    for (int i = 0; i < length; i++)
                    {
                        int tx = x + (horizontal ? i : 0), ty = y + (horizontal ? 0 : i);
                        var tp = new Point(tx, ty);
                        if (tx >= Width - 1 || ty >= Height - 1 || !OkWallTile(tx, ty) || walls.Contains(tp)) { ok = false; break; }
                        tiles.Add(tp);
                    }
                    if (!ok) continue;
                    foreach (var t in tiles) walls.Add(t);
                    segments++;
                }
                if (segments < 3 || !Connected(walls)) continue;

                var shift = new WallShift();
                foreach (var w in walls) if (!old.Contains(w)) shift.Add.Add(w);
                foreach (var w in old) if (!walls.Contains(w)) shift.Remove.Add(w);
                PendingShift = shift;
                Say($"Rift pressure {p}. The glowing tiles will shift next round.");
                Emit(new BattleEvent { Type = BattleEventType.ShiftPlanned });
                return;
            }
        }

        void DoShift()
        {
            var shift = PendingShift;
            if (shift == null) return;
            PendingShift = null;
            foreach (var p in shift.Remove) Map[p] = TileType.Floor;
            var added = new List<Point>();
            foreach (var p in shift.Add)
            {
                if (UnitAt(p.X, p.Y) != null) continue;
                Map[p] = TileType.Wall;
                added.Add(p);
            }
            if (!Connected(InteriorWalls()))
                foreach (var p in added) Map[p] = TileType.Floor;
            Say("The room shifts. Walls grind into new places.");
            Emit(new BattleEvent { Type = BattleEventType.Shifted });
        }
    }
}
