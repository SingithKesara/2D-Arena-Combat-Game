# 2D Arena Combat Game

Final Year Project – Unity 6 2D local multiplayer arena fighter.

## Overview
This project is a fast-paced 2D arena fighting game built in Unity 6.  
It features two local players, animated characters, round-based combat, timed matches, health bars, damage feedback, and a main menu with scene transitions.

## Current Direction
The project is being upgraded from a functional prototype into a more polished final-year-project presentation build.

The new visual direction uses:
- hand-painted layered backgrounds
- cleaner platform tiles
- decorative environment props
- a redesigned gameplay HUD
- mixed fighter styles instead of only recolored knights

## Main Features
- Local 2-player fighting
- Movement, jumping, fast-fall
- Light and heavy attacks
- Health bars with smooth reduction and lag effect
- Round system with timer
- Match result screen
- Main menu with controls panel and fade transition

## Controls

### Player 1
- Move: A / D
- Jump: W or Space
- Fast Fall: S
- Light Attack: J
- Heavy Attack: K

### Player 2
- Move: Numpad 4 / 6
- Jump: Numpad 8
- Fast Fall: Numpad 5
- Light Attack: Numpad 0
- Heavy Attack: Numpad Enter

## Scenes
- `MainMenu`
- `GameplayScene`

## Asset Usage
This project combines multiple asset packs for a more polished arena-fighter presentation:

- `freeknight` for knight fighter visuals
- `rvros-adventurer` for a second fighter style
- `freecutetileset` for readable gameplay platforms
- `2D Hand Painted Platformer Environment` for layered backgrounds and decorative scenery
- `Free UI build package` for framed HUD elements

## Project Structure
- `Assets/Scripts` – gameplay, UI, scene, and combat logic
- `Assets/Scenes` – menu and gameplay scenes
- `Assets/Animations` – generated animation clips and controllers
- `Assets/Art` – character and tile assets
- `Assets/UI` – custom UI textures
- `Assets/Audio` – music and sound effects

## Scene Flow
- The Main Menu loads `GameplayScene`
- The gameplay UI supports rematch and return-to-menu flow
- The current build keeps menu integration intact while improving gameplay visuals

## Current Upgrade Goals
- Rebuild the gameplay scene with cleaner art direction
- Improve character variety
- Replace placeholder-feel HUD with polished UI assets
- Remove unused demo and documentation files
- Keep the main menu and gameplay flow fully connected

## Tech
- Unity 6
- C#
- Unity Input System
- Sprite animation with Animator Controllers

## Author
Singith Kesara Wahalathanthri