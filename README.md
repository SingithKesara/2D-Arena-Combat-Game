# 2D Arena Combat Game
## PUSL3190 Computing Project | 10952382 | Singith Kesara Wahalathanthri

### Quick Start — Run these 7 steps in Unity Editor (Tools → Arena Combat):

| Step | Menu Item | What it does |
|------|-----------|-------------|
| ① | Setup Layers and Tags | Creates Ground (L6) + Player (L7) layers |
| ② | Slice Sprite Sheets | Slices knight sprites + imports all UI textures |
| ③ | Build Animator Controllers | Creates AC_Player1 & AC_Player2 |
| ④ | Build Gameplay Scene | Builds complete gameplay scene |
| ⑤ | Fix Physics2D Matrix | Players land on Ground, pass through each other |
| ⑥ | Clean Stale Files | Removes old duplicate files |
| ⑦ | Build Main Menu Scene | Creates the MainMenu.unity scene |

After ④ — open Gameplayscene, Ctrl+S, then run ⑤ and ⑦.
After ⑦ — open MainMenu scene in Scenes folder, press Play.

### Controls
| Action | Player 1 | Player 2 |
|--------|----------|----------|
| Move | A / D | Numpad 4 / 6 |
| Jump (double jump) | W or Space | Numpad 8 |
| Fast Fall | S (while airborne) | Numpad 5 |
| Light Attack | J | Numpad 0 |
| Heavy Attack | K | Numpad Enter |

### Features (per PID / Interim Report requirements)
- Two playable knight characters (Colour1 & Colour2 freeknight assets)
- Sprite sheet animations: Idle, Run, Jump, Fall, Light Attack, Heavy Attack, Hit, Death
- Physics-driven movement with double-jump (Brawlhalla-style)
- Health system: 100 HP per player, visual health bars (green→yellow→red)
- 99-second match timer with pulse effect in last 10 seconds
- Round system: best of 2 rounds, "ROUND X → FIGHT!" intro
- Win conditions: KO or most HP when timer expires
- Floating damage numbers on hit
- Screen flash on heavy hit
- Layered parallax background
- Battle BGM + SFX (swing, hit, jump, land, death)
- Main Menu with controls panel
- Scene transition with fade effect
