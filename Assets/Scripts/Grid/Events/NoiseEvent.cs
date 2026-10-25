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

    public struct NoiseEvent
    {
        public Vector3 Position;
        public float Radius;
        public float Intensity;
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