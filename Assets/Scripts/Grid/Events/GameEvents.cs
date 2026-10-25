using System;
using UnityEngine;

namespace Sokophobia.Grid
{
    public static class GameEvents
    {
        public static event Action<NoiseEvent> OnNoise;

        public static event Action<Box, GridCoord> OnBoxPushed;
        public static event Action<Box, GridCoord> OnBoxPlaced;
        public static event Action<Box, GridCoord> OnBoxRemovedFromPlate;

        public static event Action OnPlayerDamaged;
        public static event Action OnPlayerDied;
        public static event Action OnLevelWon;

        public static event Action OnPuzzleDoorOpened;
        public static event Action<bool> OnGameplayPopupChanged;

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

        public static void RaiseLevelWon()
        {
            OnLevelWon?.Invoke();
        }

        public static void RaisePuzzleDoorOpened()
        {
            OnPuzzleDoorOpened?.Invoke();
        }

        public static void RaiseGameplayPopupChanged(bool isVisible)
        {
            OnGameplayPopupChanged?.Invoke(isVisible);
        }
    }
}