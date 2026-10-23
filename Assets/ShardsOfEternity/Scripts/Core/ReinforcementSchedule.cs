using System;
using System.Linq;

namespace ShardsOfEternity.Core
{
    /// <summary>
    /// How long, in seconds of unpaused play, before each reinforcement wave on a floor. The last delay repeats,
    /// so the default gives 10 minutes before the first wave, 5 before the second, then a wave every 2 minutes.
    /// </summary>
    public sealed class ReinforcementSchedule
    {
        public static readonly float[] DefaultDelays = { 600f, 300f, 120f };

        readonly float[] delays;

        public ReinforcementSchedule(params float[] delays)
        {
            this.delays = delays != null && delays.Length > 0 && delays.All(d => d > 0) ? delays.ToArray() : DefaultDelays.ToArray();
        }

        /// <summary>Seconds to wait before wave <paramref name="waveIndex"/> (0 = the first wave on this floor).</summary>
        public float SecondsBefore(int waveIndex) => delays[Math.Min(Math.Max(0, waveIndex), delays.Length - 1)];
    }
}
