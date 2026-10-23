using System.Collections.Generic;

namespace ShardsOfEternity.Core
{
    public enum BattleEventType
    {
        Log,
        TurnStarted,
        /// <summary>Unit walked or charged along <see cref="BattleEvent.Path"/>.</summary>
        Moved,
        /// <summary>Unit appeared at <see cref="BattleEvent.To"/> without walking (Teleport, Shadowstep).</summary>
        Teleported,
        /// <summary>Unit and <see cref="BattleEvent.Other"/> traded places.</summary>
        Swapped,
        /// <summary>Unit was pushed one tile to <see cref="BattleEvent.To"/>.</summary>
        KnockedBack,
        /// <summary>Unit attacked <see cref="BattleEvent.Other"/>. Damage follows as its own event.</summary>
        Attacked,
        AbilityUsed,
        Damaged,
        Healed,
        Died,
        Spawned,
        RoomWoke,
        /// <summary>A wall shift was telegraphed; see <see cref="Battle.PendingShift"/>.</summary>
        ShiftPlanned,
        /// <summary><see cref="BattleEvent.Item"/> fell to the ground at <see cref="BattleEvent.To"/>.</summary>
        LootDropped,
        /// <summary>A hero picked up <see cref="BattleEvent.Item"/> at <see cref="BattleEvent.To"/>.</summary>
        LootPickedUp,
        /// <summary>Every enemy on a floor before the last is dead and the exit is open.</summary>
        FloorCleared,
        /// <summary>The walls moved. Rebuild the board.</summary>
        Shifted,
        BattleOver,
    }

    /// <summary>
    /// Something that happened in the battle. The rules resolve instantly; the view plays these back in order
    /// to animate them, then snaps to the battle state.
    /// </summary>
    public sealed class BattleEvent
    {
        public BattleEventType Type;
        public Unit Unit;
        public Unit Other;
        public Point To;
        public List<Point> Path;
        public int Amount;
        public bool Crit;
        public string Text;
        public GearItem Item;

        public override string ToString() => $"{Type} {Unit?.Name} {Other?.Name} {Amount} {Text}";
    }
}
