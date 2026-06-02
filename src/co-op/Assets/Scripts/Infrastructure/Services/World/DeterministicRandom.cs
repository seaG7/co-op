namespace Infrastructure.Services.World
{
    public sealed class DeterministicRandom
    {
        private ulong _state;

        public DeterministicRandom(ulong seed) => _state = seed == 0UL ? 0x9E3779B97F4A7C15UL : seed;

        public uint NextUInt()
        {
            _state ^= _state << 13;
            _state ^= _state >> 7;
            _state ^= _state << 17;
            return (uint)(_state >> 32);
        }

        public float NextFloat() => NextUInt() / (float)uint.MaxValue;

        public float Range(float min, float max) => min + NextFloat() * (max - min);

        public static ulong Mix(int seed, int salt)
        {
            ulong z = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL + (ulong)(uint)salt + 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
