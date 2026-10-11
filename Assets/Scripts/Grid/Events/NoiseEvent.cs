using UnityEngine;

namespace Sokophobia.Grid
{
    public enum NoiseSource
    {
        BoxPush,
        BoxPull,
        Footstep,
        DoorOpen,
        Other
    }

    /// <summary>
    /// Emitted whenever something produces a detectable sound.
    /// Person 2's Sokobot subscribes to this to drive its AI.
    /// </summary>
    public struct NoiseEvent
    {
        public Vector3 Position;
        public float Radius;      // how far the sound carries
        public float Intensity;   // 0..1, loudness multiplier
        public NoiseSource Source;

        public NoiseEvent(Vector3 position, float radius, float intensity, NoiseSource source)
        {
            Position = position;
            Radius = radius;
            Intensity = intensity;
            Source = source;
        }

        public override string ToString()
            => $"[Noise:{Source}] pos={Position} r={Radius} i={Intensity}";
    }
}