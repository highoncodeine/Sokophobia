namespace Sokophobia.Grid
{
    /// <summary>
    /// What kind of entity occupies a grid cell (if any).
    /// </summary>
    public enum OccupantType
    {
        None,
        Player,
        Box,
        Enemy,
        PressurePlate,
        Door,
        Exit
    }
}