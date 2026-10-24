using System;

namespace CatanRoguelike.Core.Map
{
    /// <summary>Point or direction on the board XZ plane (no UnityEngine).</summary>
    public readonly struct MapPlaneXz
    {
        public readonly float X;
        public readonly float Z;

        public MapPlaneXz(float x, float z)
        {
            X = x;
            Z = z;
        }

        public float SqrMagnitude => X * X + Z * Z;

        public float Magnitude => (float)Math.Sqrt(SqrMagnitude);

        public MapPlaneXz Normalized
        {
            get
            {
                float mag = Magnitude;
                return mag < 1e-8f ? UnitZ : this / mag;
            }
        }

        public static MapPlaneXz Zero => new(0f, 0f);

        /// <summary>+Z on the board plane (board “north” for harbor math).</summary>
        public static MapPlaneXz UnitZ => new(0f, 1f);

        public static MapPlaneXz operator +(MapPlaneXz a, MapPlaneXz b) => new(a.X + b.X, a.Z + b.Z);

        public static MapPlaneXz operator -(MapPlaneXz a, MapPlaneXz b) => new(a.X - b.X, a.Z - b.Z);

        public static MapPlaneXz operator *(MapPlaneXz a, float s) => new(a.X * s, a.Z * s);

        public static MapPlaneXz operator /(MapPlaneXz a, float s) => new(a.X / s, a.Z / s);

        public static MapPlaneXz operator /(MapPlaneXz a, int s) => a / (float)s;

        public static float Dot(MapPlaneXz a, MapPlaneXz b) => a.X * b.X + a.Z * b.Z;

        public static float Clamp01(float value) =>
            value < 0f ? 0f : value > 1f ? 1f : value;

        public override string ToString() => $"({X:F3},{Z:F3})";
    }
}
