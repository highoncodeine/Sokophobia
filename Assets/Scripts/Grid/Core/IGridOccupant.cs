using UnityEngine;

namespace Sokophobia.Grid
{
    /// <summary>
    /// Anything that can occupy a grid cell (player, box, enemy, etc.).
    /// </summary>
    public interface IGridOccupant
    {
        GridCoord Coord { get; }
        OccupantType Type { get; }
        Transform Transform { get; }
    }
}