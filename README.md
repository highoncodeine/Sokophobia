# Sokophobia

A game created in Unity for Game Jam #1, in partial fulfillment of the requirements for GAMEDEV.

## Group Members

- BARREO, Carlo Gabriel
- CUSTODIO, Justin Miguel
- GARCIA, Hadjj
- OCAMPO, Jericho

## Game Description

Sokophobia is a single-player, first-person horror puzzle game inspired by the retro game Sokoban. Navigate a maze and push crates onto every pressure plate to open the exit. Placing the first crate on a plate awakens Sokobot, an enemy that listens to crate noises, chases the player, and becomes faster as more plates are completed. Use your flashlight's high beam to temporarily stun it. Make sure to manage your battery charge and collect spare batteries to fully recharge it. Once the door opens, reach the final exit area to win. Losing all your health results in game over.

## Game Controls

| Input | Action |
| --- | --- |
| W / Up Arrow | Move forward. |
| S / Down Arrow | Move backward. |
| A / Left Arrow | Strafe left. |
| D / Right Arrow | Strafe right. |
| Mouse movement | Look around and aim the flashlight. |
| Move into a crate | Push it, provided the space behind it is clear. |
| Hold Left Mouse Button | Use the high beam. Keep it aimed at Sokobot to charge a stun. Successful stuns consume battery charge. |
| E | Pick up a battery when looking at it within interaction range. |
| Esc | Pause or resume the game. |
| F5 | Immediately restart the current level. |

Movement is grid-based, relative to the nearest cardinal direction you are facing. After a successful stun, let the flashlight recharge, then release and press the left mouse button again to use another high beam.

### Menu Options

- Main menu: **Play** opens the controls screen. Press **Space** to start. **Quit** exits the application (not the Unity Editor).
- Pause menu: **Resume** continues the game. **Quit** returns to the main menu.
- Victory / game-over screen: **Play Again** reloads the level. **Quit** returns to the main menu.

### Testing Shortcuts

| Input | Action |
| --- | --- ||
| F8 | Mark the plate puzzle complete for testing. |