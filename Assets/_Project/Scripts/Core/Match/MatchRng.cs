using System;

namespace CatanRoguelike.Core.Match
{
    /// <summary>
    /// Tiny deterministic RNG (xorshift64*, seeded through splitmix64). Unlike System.Random its stream is defined
    /// here, so it is identical on Mono, IL2CPP and .NET, and its whole state is one serializable ulong.
    /// </summary>
    [Serializable]
    public sealed class MatchRng
    {
        public ulong State { get; set; }

        public MatchRng()
        {
            State = 0x9E3779B97F4A7C15UL;
        }

        public MatchRng(int seed, ulong salt = 0)
        {
            ulong z = unchecked((ulong)(uint)seed + 0x9E3779B97F4A7C15UL * (salt + 1));
            z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
            z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
            z ^= z >> 31;
            State = z == 0 ? 0x2545F4914F6CDD1DUL : z;
        }

        public ulong NextULong()
        {
            ulong x = State;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            State = x;
            return unchecked(x * 0x2545F4914F6CDD1DUL);
        }

        /// <summary>Uniform in [0, maxExclusive).</summary>
        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return (int)((NextULong() >> 33) % (ulong)maxExclusive);
        }

        public int RollDie() => Next(6) + 1;
    }
}
