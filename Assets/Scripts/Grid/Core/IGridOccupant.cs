using UnityEngine;

namespace Sokophobia.Grid
{
    public interface IGridOccupant
    {
        GridCoord Coord { get; }
        OccupantType Type { get; }
        Transform Transform { get; }
    }
}