namespace Sokophobia.Grid
{
    /// <summary>
    /// Defines what occupies a grid cell.
    /// </summary>
    public enum CellType
    {
        Void,   // Outside the playable grid — cannot be entered.
        Floor,  // Walkable cell.
        Wall    // Blocking cell.
    }
}