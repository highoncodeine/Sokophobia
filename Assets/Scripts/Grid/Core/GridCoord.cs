using System;

namespace Sokophobia.Grid
{
    /// <summary>
    /// Integer coordinate on the grid. Immutable value type.
    /// </summary>
    [Serializable]
    public struct GridCoord : IEquatable<GridCoord>
    {
        public int x;
        public int z;

        public GridCoord(int x, int z)
        {
            this.x = x;
            this.z = z;
        }

        public static GridCoord Zero => new GridCoord(0, 0);

        public static GridCoord operator +(GridCoord a, GridCoord b)
            => new GridCoord(a.x + b.x, a.z + b.z);

        public static GridCoord operator -(GridCoord a, GridCoord b)
            => new GridCoord(a.x - b.x, a.z - b.z);

        public static bool operator ==(GridCoord a, GridCoord b) => a.Equals(b);
        public static bool operator !=(GridCoord a, GridCoord b) => !a.Equals(b);

        public bool Equals(GridCoord other) => x == other.x && z == other.z;

        public override bool Equals(object obj) => obj is GridCoord o && Equals(o);

        public override int GetHashCode()
        {
            unchecked
            {
                return (x * 397) ^ z;
            }
        }

        public override string ToString() => $"({x}, {z})";
    }
}