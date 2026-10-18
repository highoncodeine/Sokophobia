namespace Sokophobia.Enemy
{
    /// <summary>
    /// Stun entry point for Sokobot. Callable from any state.
    /// First trigger: flashlight beam. Consumable later.
    /// </summary>
    public interface ISokobotStunnable
    {
        void Stun(float durationSeconds);
    }
}
