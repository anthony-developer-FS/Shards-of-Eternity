namespace ShardsOfEternity.Core
{
    /// <summary>A hero or enemy on the board during a battle.</summary>
    public sealed class Unit
    {
        public readonly int Id;
        public readonly UnitDef Def;

        // Stats are copied from the def so gear and buffs can change them later.
        public int MaxHp;
        public int Damage;
        public int Armor;
        public int Speed;
        public int Move;
        public int Range;
        public double Crit;
        public double CritDamage;

        public int X;
        public int Y;
        public int Hp;
        public bool Alive = true;
        public bool Awake;

        /// <summary>Timeline position of this unit's next turn. Lowest goes first.</summary>
        public double NextTurnAt;
        /// <summary>Extra timeline delay added when this turn ends, from heavy abilities.</summary>
        public int Recovery;

        public bool Moved;
        public bool Acted;
        /// <summary>Action shards left this turn, when <see cref="TurnRules.UseActionShards"/> is on.</summary>
        public int Shards;

        public readonly int[] Cooldowns = new int[4];
        /// <summary>Ultimate charge, 0 to 100.</summary>
        public int Ultimate;
        public readonly Ability[] Abilities;

        // Status effects
        public bool Rooted;
        public int Poison;
        public int Marked;
        public int ArmorBuff;

        public Unit(int id, UnitDef def, Point position)
        {
            Id = id;
            Def = def;
            MaxHp = Hp = def.Hp;
            Damage = def.Damage;
            Armor = def.Armor;
            Speed = def.Speed;
            Move = def.Move;
            Range = def.Range;
            Crit = def.Crit;
            CritDamage = def.CritDamage;
            X = position.X;
            Y = position.Y;
            Awake = !def.IsEnemy;
            NextTurnAt = def.IsEnemy ? double.PositiveInfinity : TurnInterval;
            Abilities = def.IsEnemy ? null : AbilityLibrary.For(def.HeroClass);
        }

        public string Name => Def.Name;
        public string Glyph => Def.Glyph;
        public bool IsEnemy => Def.IsEnemy;
        public bool IsHero => !Def.IsEnemy;
        public Point Position => new Point(X, Y);
        public double TurnInterval => 1000.0 / Speed;

        public bool Is(HeroClass cls) => IsHero && Def.HeroClass == cls;

        /// <summary>Rogues never give free attacks and ignore spikes.</summary>
        public bool Slips => Is(HeroClass.Rogue);
        /// <summary>Sorcerers float over spikes.</summary>
        public bool Levitates => Is(HeroClass.Sorcerer);

        public int EffectiveArmor => Armor + ArmorBuff;

        public override string ToString() => $"{Name}#{Id}@{Position}";
    }
}
