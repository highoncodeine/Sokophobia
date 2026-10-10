using System;
using UnityEngine;

namespace Sokophobia.Grid
{
    /// <summary>
    /// Static event bus. Publishers (grid logic) fire; subscribers (AI, UI, puzzle) listen.
    /// Person 2 (Sokobot), Person 3 (PuzzleManager, HUD) should subscribe here.
    /// </summary>
    public static class GameEvents
    {
        // Audio / AI
        public static event Action<NoiseEvent> OnNoise;

        // Box lifecycle
        public static event Action<Box, GridCoord> OnBoxPushed;
        public static event Action<Box, GridCoord> OnBoxPlaced;
        public static event Action<Box, GridCoord> OnBoxRemovedFromPlate;

        // Player lifecycle
        public static event Action OnPlayerDamaged;
        public static event Action OnPlayerDied;

        // ---------- Invokers ----------
        public static void RaiseNoise(NoiseEvent e)
        {
            OnNoise?.Invoke(e);
        }

        public static void RaiseBoxPushed(Box box, GridCoord from)
        {
            OnBoxPushed?.Invoke(box, from);
        }

        public static void RaiseBoxPlaced(Box box, GridCoord cell)
        {
            OnBoxPlaced?.Invoke(box, cell);
        }

        public static void RaiseBoxRemovedFromPlate(Box box, GridCoord cell)
        {
            OnBoxRemovedFromPlate?.Invoke(box, cell);
        }

        public static void RaisePlayerDamaged()
        {
            OnPlayerDamaged?.Invoke();
        }

        public static void RaisePlayerDied()
        {
            OnPlayerDied?.Invoke();
        }
    }
}