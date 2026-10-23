using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShardsOfEternity.Core;
using UnityEngine;

namespace ShardsOfEternity.Game
{
    /// <summary>What the current hero can do right now, for highlighting and input.</summary>
    public sealed class HeroOptions
    {
        public List<MoveOption> Moves = new List<MoveOption>();
        public List<Unit> Attacks = new List<Unit>();
        public List<Point> AbilityTargets = new List<Point>();
        public HashSet<Point> Area = new HashSet<Point>();
    }

    /// <summary>
    /// Runs the battle loop: plays enemy turns, turns pointer and keyboard input into hero commands,
    /// and plays each command's events through the <see cref="BoardView"/> before taking more input.
    /// </summary>
    public sealed class BattleController : MonoBehaviour
    {
        public Camera Camera;
        public BoardView Board;
        public BattleHud Hud;

        public List<string> Squad = new List<string>(Roster.DefaultSquad);
        public RunKind RunKind = RunKind.Dungeon;
        /// <summary>Floor 1 uses the handmade prototype room instead of a generated one.</summary>
        public bool HandmadeFirstFloor;
        public int Seed;
        /// <summary>Experimental: spend action shards on any mix of moves and actions.</summary>
        public bool UseActionShards;
        /// <summary>With action shards, unspent shards carry over to later turns, up to 4.</summary>
        public bool BankShards;
        /// <summary>Seconds of unpaused play between reinforcement waves once a room is awake.</summary>
        public float ReinforcementInterval = 120f;

        public DungeonRun Run { get; private set; }
        public Battle Battle => Run?.Battle;
        public bool Busy { get; private set; }
        public bool Paused { get; private set; }
        /// <summary>Between floors: the squad equips gear before going deeper.</summary>
        public bool InCamp { get; private set; }
        public float ReinforcementCountdown { get; private set; }
        /// <summary>Index of the ability being aimed, or -1 when moving and attacking.</summary>
        public int AbilityIndex { get; private set; } = -1;
        public Point? Hover { get; private set; }
        /// <summary>Touch input picks a target first and confirms on a second tap.</summary>
        public Point? Pending { get; private set; }
        public HeroOptions Options { get; private set; }

        public bool SquadReady => Squad.Count == 4;

        public void NewRun()
        {
            StopAllCoroutines();
            SetPaused(false);
            InCamp = false;
            if (!SquadReady)
            {
                Run = null;
                Options = null;
                return;
            }
            int seed = Seed != 0 ? Seed : Random.Range(1, int.MaxValue);
            Run = new DungeonRun(RunKind, Squad, seed, handmadeFirstFloor: HandmadeFirstFloor)
            {
                Rules = new TurnRules { UseActionShards = UseActionShards, ShardsPerTurn = 2, ShardCap = BankShards ? 4 : 2 },
            };
            Run.NextFloor();
            BeginFloor();
        }

        void BeginFloor()
        {
            Board.Build(Battle);
            AbilityIndex = -1;
            Pending = null;
            Hover = null;
            ReinforcementCountdown = ReinforcementInterval;
            StartCoroutine(Advance());
        }

        /// <summary>Leaves the between-floor camp and loads the next floor with the new gear applied.</summary>
        public void LeaveCamp()
        {
            if (!InCamp) return;
            InCamp = false;
            StartCoroutine(Descend());
        }

        IEnumerator Descend()
        {
            Busy = true;
            Refresh();
            yield return new WaitForSeconds(0.8f);
            Run.NextFloor();
            BeginFloor();
        }

        public void SetPaused(bool paused)
        {
            Paused = paused;
            Time.timeScale = paused ? 0f : 1f;
        }

        void Update()
        {
            if (Camera != null && Board != null) Board.FrameCamera(Camera);
            if (Battle == null) return;
            if (Input.GetKeyDown(KeyCode.P)) SetPaused(!Paused);
            if (Paused) return;
            TickReinforcements();
            HandleKeys();
            HandlePointer();
        }

        /// <summary>Real-time countdown. Time.deltaTime is 0 while paused, so pausing stops it.</summary>
        void TickReinforcements()
        {
            if (!Battle.ReinforcementsActive) return;
            ReinforcementCountdown = Mathf.Max(0, ReinforcementCountdown - Time.deltaTime);
            // Waves land between actions: while something is animating, the turn loop picks them up instead.
            if (ReinforcementCountdown <= 0 && !Busy) StartCoroutine(ReinforceNow());
        }

        IEnumerator ReinforceNow()
        {
            Busy = true;
            yield return CallReinforcementsIfDue();
            Busy = false;
            Refresh();
        }

        IEnumerator CallReinforcementsIfDue()
        {
            if (ReinforcementCountdown > 0 || !Battle.ReinforcementsActive) yield break;
            ReinforcementCountdown = ReinforcementInterval;
            Battle.CallReinforcements();
            yield return PlayEvents();
        }

        // ---------- turn loop ----------

        /// <summary>Plays pending events, then runs enemy turns until a hero needs orders or the battle ends.</summary>
        IEnumerator Advance()
        {
            Busy = true;
            Refresh();
            yield return PlayEvents();
            while (!Battle.IsOver && Battle.Current != null && Battle.Current.IsEnemy)
            {
                yield return CallReinforcementsIfDue();
                Board.SyncUnits();
                Refresh();
                yield return new WaitForSeconds(0.35f);
                Battle.RunEnemyTurn();
                yield return PlayEvents();
                if (Battle.IsOver) break;
                yield return new WaitForSeconds(0.25f);
                Battle.EndTurn();
                yield return PlayEvents();
            }
            Busy = false;
            Board.SyncUnits();
            Refresh();
        }

        /// <summary>After a hero command: animate it, then end the turn if the hero has both moved and acted.</summary>
        IEnumerator AfterHeroCommand()
        {
            Busy = true;
            Pending = null;
            Refresh();
            yield return PlayEvents();
            Board.SyncUnits();
            if (Battle.Outcome == BattleOutcome.FloorComplete)
            {
                // Make camp: the HUD shows the equip screen until the player continues.
                Run.CollectLoot();
                InCamp = true;
                Refresh();
                yield break;
            }
            if (!Battle.IsOver && Battle.CurrentTurnSpent)
            {
                Refresh();
                yield return new WaitForSeconds(0.3f);
                Battle.EndTurn();
                yield return Advance();
                yield break;
            }
            Busy = false;
            Refresh();
        }

        IEnumerator PlayEvents()
        {
            var events = Battle.DrainEvents();
            foreach (var e in events) yield return Board.Play(e);
            Board.SyncUnits();
        }

        /// <summary>Recomputes the current hero's options and recolors the board.</summary>
        public void Refresh()
        {
            Options = ComputeOptions();
            Board.RefreshTiles(Options, Pending ?? Hover);
        }

        bool CanCommand => Battle != null && !Busy && !Paused && !Battle.IsOver && Battle.Current != null && Battle.Current.IsHero;

        HeroOptions ComputeOptions()
        {
            if (!CanCommand) return null;
            var u = Battle.Current;
            var options = new HeroOptions();
            if (AbilityIndex >= 0)
            {
                options.AbilityTargets = Battle.AbilityTargets(u, AbilityIndex);
                var a = u.Abilities[AbilityIndex];
                var focus = a.Target == TargetKind.Self ? u.Position : Pending ?? Hover;
                if (a.Area > 0 && focus.HasValue && options.AbilityTargets.Contains(focus.Value))
                    for (int dy = -a.Area; dy <= a.Area; dy++)
                        for (int dx = -a.Area; dx <= a.Area; dx++)
                            options.Area.Add(new Point(focus.Value.X + dx, focus.Value.Y + dy));
                if (a.HighlightsSightFoes)
                    foreach (var e in Battle.SightFoes(u)) options.Area.Add(e.Position);
            }
            else
            {
                if (Battle.CanMove(u)) options.Moves = Battle.MoveOptions(u);
                options.Attacks = Battle.AttackTargets(u);
            }
            return options;
        }

        // ---------- commands ----------

        public void OnTile(Point p, bool confirmImmediately)
        {
            if (!CanCommand || Options == null) return;
            var u = Battle.Current;
            bool confirmed = confirmImmediately || Pending == p;

            if (AbilityIndex >= 0)
            {
                if (!Options.AbilityTargets.Contains(p))
                {
                    Pending = null;
                    Refresh();
                    return;
                }
                if (confirmed || u.Abilities[AbilityIndex].Target == TargetKind.Self) UseAbility(AbilityIndex, p);
                else SetPending(p);
                return;
            }

            var target = Battle.UnitAt(p);
            if (target != null && Options.Attacks.Contains(target))
            {
                if (confirmed)
                {
                    Battle.TryAttack(target);
                    StartCoroutine(AfterHeroCommand());
                }
                else SetPending(p);
                return;
            }

            if (Options.Moves.Any(m => m.Target == p))
            {
                Battle.TryMove(p);
                StartCoroutine(AfterHeroCommand());
                return;
            }

            Pending = null;
            Refresh();
        }

        void SetPending(Point p)
        {
            Pending = p;
            Refresh();
        }

        void UseAbility(int i, Point p)
        {
            AbilityIndex = -1;
            if (Battle.TryUseAbility(i, p)) StartCoroutine(AfterHeroCommand());
            else Refresh();
        }

        /// <summary>Selects an ability to aim. Pressing a selected self-target ability again uses it; otherwise it cancels.</summary>
        public void ToggleAbility(int i)
        {
            if (!CanCommand) return;
            var u = Battle.Current;
            if (i < 0 || i >= u.Abilities.Length) return;
            if (AbilityIndex == i)
            {
                if (u.Abilities[i].Target == TargetKind.Self && Battle.AbilityTargets(u, i).Count > 0)
                {
                    UseAbility(i, u.Position);
                    return;
                }
                AbilityIndex = -1;
            }
            else if (Battle.AbilityTargets(u, i).Count > 0)
            {
                AbilityIndex = i;
            }
            Pending = null;
            Refresh();
        }

        public void EndTurn()
        {
            if (!CanCommand) return;
            AbilityIndex = -1;
            Pending = null;
            Battle.EndTurn();
            StartCoroutine(Advance());
        }

        public void Cancel()
        {
            AbilityIndex = -1;
            Pending = null;
            if (Battle != null) Refresh();
        }

        // ---------- input ----------

        void HandleKeys()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) ToggleAbility(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) ToggleAbility(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) ToggleAbility(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) ToggleAbility(3);
            if (Input.GetKeyDown(KeyCode.E)) EndTurn();
            if (Input.GetKeyDown(KeyCode.Escape)) Cancel();
        }

        void HandlePointer()
        {
            if (Camera == null) return;
            Vector2 pointer = Input.mousePosition;
            bool overHud = Hud != null && Hud.IsOverHud(pointer);
            bool touch = Input.touchCount > 0;

            // Mouse hover previews; touch uses tap-to-preview, tap-again-to-confirm instead.
            if (!touch)
            {
                var hover = overHud ? null : Board.ScreenToTile(Camera, pointer);
                if (hover != Hover)
                {
                    Hover = hover;
                    if (!Busy) Refresh();
                }
            }

            if (!Input.GetMouseButtonDown(0) || overHud || Busy) return;
            var tile = Board.ScreenToTile(Camera, pointer);
            if (tile.HasValue) OnTile(tile.Value, confirmImmediately: !touch);
        }
    }
}
