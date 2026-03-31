# 2D Arena Combat Game — Unity Editor Setup Guide
## PUSL3190 | Singith Kesara Wahalathanthri | 10952382

This guide walks you through every step needed to wire up the scripts in the Unity Editor.  
After following these steps the game will be fully playable: two knights, health bars,  
99-second timer, round system, and audio — exactly like the coursework requires.

---

## 0. Open the Project

1. Open **Unity Hub → Open → Add from disk** and browse to `2D-Arena-Combat-Game-2-revamp`.  
2. Unity version: **2022.3 LTS** (or 6000.x). Accept any upgrade prompts.  
3. Wait for reimport. A few yellow warnings are normal; red errors mean a step below is missing.

---

## 1. Layers Setup (Project Settings)

`Edit → Project Settings → Tags and Layers`

| Layer # | Name     |
|---------|----------|
| 6       | Ground   |
| 7       | Player   |

Both player GameObjects must be on the **Player** layer.  
All ground/platform colliders must be on the **Ground** layer.

---

## 2. Player 1 (Colour1 Knight) — GameObject Setup

### 2a. Find the existing Player GameObject in the Hierarchy
It already has: `Transform`, `SpriteRenderer`, `Rigidbody2D`, `Animator`, `PlayerController`.

### 2b. Add missing components
In the Inspector click **Add Component** and add each of the following:

- `HealthManager`
- `CombatSystem`
- `AnimationController`
- `DamageNumberSpawner`
- `AudioSource` *(× 2 — AudioManager adds its own, so ignore if already present)*

### 2c. Configure PlayerController
| Field              | Value / Object |
|--------------------|----------------|
| Walk Speed         | 8              |
| Run Speed          | 13             |
| Jump Force         | 16             |
| Fast Fall Force    | 22             |
| Max Fall Speed     | -28            |
| Ground Check       | *GroundCheck* child (see 2d) |
| Ground Check Radius| 0.18           |
| Ground Layer       | Ground         |
| **Player Index**   | **1**          |

### 2d. Create child objects on Player 1

**GroundCheck** — empty child at the **feet** of the character:
- Right-click Player in Hierarchy → Create Empty → rename `GroundCheck`
- Position: `(0, -0.55, 0)` (roughly at feet)

**AttackPoint** — empty child at the **sword/fist** side:
- Create Empty → rename `AttackPoint`
- Position: `(0.7, 0.1, 0)` for right-facing character

### 2e. Configure CombatSystem
| Field              | Value          |
|--------------------|----------------|
| Attack Point       | *AttackPoint* child |
| Light Attack Radius| 0.6            |
| Heavy Attack Radius| 0.9            |
| Player Layer       | Player         |
| Light Damage       | 8              |
| Heavy Damage       | 20             |

### 2f. Configure Rigidbody2D
| Field                   | Value       |
|-------------------------|-------------|
| Body Type               | Dynamic     |
| Collision Detection     | Continuous  |
| Freeze Rotation Z       | ✓ checked   |
| Gravity Scale           | 3           |

---

## 3. Player 2 (Colour2 Knight) — GameObject Setup

Duplicate Player 1 (`Ctrl+D`). Rename to `Player2`.

Changes from Player 1:
- Sprite Renderer → assign the **Colour2** knight sprite sheet  
- `PlayerController → Player Index = 2`  
- Move Player2 to the **right** spawn position (e.g., X = 4)
- Both players must be on the **Player** layer

---

## 4. Animator Controllers

### 4a. Player 1 Animator Controller (`AC_Player` — already exists)
Open Window → Animation → Animator and add these **Parameters**:

| Name         | Type    |
|--------------|---------|
| isMoving     | Bool    |
| isRunning    | Bool    |
| isGrounded   | Bool    |
| velocityY    | Float   |
| jump         | Trigger |
| lightAttack  | Trigger |
| heavyAttack  | Trigger |
| hit          | Trigger |
| death        | Trigger |

### 4b. State Transitions (minimum viable)
Create states and wire the transitions:

```
Entry → Idle (default)
Idle   → Run       : isMoving = true
Run    → Idle      : isMoving = false
Any    → Jump      : jump trigger
Jump   → Idle      : isGrounded = true AND velocityY < 0.1
Any    → LightAtk  : lightAttack trigger; has exit time = false
Any    → HeavyAtk  : heavyAttack trigger; has exit time = false
Any    → Hit       : hit trigger
Any    → Death     : death trigger
```

**Knight sprite sheet frame counts (all in `Art/freeknight/Colour1/Outline/120x80_PNGSheets`):**

| State      | File           | Frames | FPS |
|------------|----------------|--------|-----|
| Idle       | _Idle.png      | 10     | 10  |
| Run        | _Run.png       | 10     | 12  |
| Jump       | _Jump.png      | 3      | 8   |
| Fall       | _Fall.png      | 3      | 8   |
| LightAttack| _Attack.png    | 4      | 16  |
| HeavyAttack| _Attack2.png   | 6      | 12  |
| Hit        | _Hit.png       | 2      | 12  |
| Death      | _Death.png     | 10     | 10  |

Each PNG sheet is 120×80 per frame.  
Use **Sprite Editor → Slice → Grid By Cell Size → 120×80** to slice each sheet.

### 4c. Player 2 Animator Controller
Duplicate `AC_Player`, rename `AC_Player2`, reassign the same parameters but  
point each state to the **Colour2** sliced sprites instead of Colour1.

---

## 5. Arena / Ground Setup

### 5a. Ground platform
The existing flat `Ground` GameObject needs a **Composite Collider2D** or **BoxCollider2D** on the Ground layer.  
Set its Tag to `Ground` as well.

### 5b. Side platforms (Brawlhalla-style)
Create 2–3 additional platform GameObjects:
- Add `BoxCollider2D` (layer = Ground)
- Position them at different heights (e.g., Y = 2, left/right of centre)

### 5c. Arena boundaries
Create an empty `ArenaManager` GameObject and add the `ArenaManager.cs` script:

| Field              | Value / Object            |
|--------------------|---------------------------|
| Death Zone Y       | -10 (below ground level)  |
| Arena Camera       | Main Camera               |
| Cam Min Size       | 5                         |
| Cam Max Size       | 9                         |
| Player1 Transform  | Player1 transform         |
| Player2 Transform  | Player2 transform         |

---

## 6. Game State Manager Setup

Create an empty GameObject named `GameManager`. Add `GameStateManager.cs`:

| Field         | Value / Object |
|---------------|----------------|
| Rounds To Win | 2              |
| Match Time Sec| 99             |
| Spawn P1      | SpawnPoint1 empty Transform (left side, e.g. X=-4) |
| Spawn P2      | SpawnPoint2 empty Transform (right side, e.g. X=4) |
| Player 1      | Player1 GameObject |
| Player 2      | Player2 GameObject |

Create two empty GameObjects `SpawnPoint1` and `SpawnPoint2` above the ground.

---

## 7. UI Canvas Setup

Create a **Canvas** (Screen Space – Overlay).

### 7a. Health Bar — Player 1 (left side)
- Create UI → Slider → rename `HealthBarP1`
- Anchor: top-left
- Position: `(-300, -30)`, Width: `350`, Height: `30`
- Slider Min=0, Max=1, Value=1, **Whole Numbers = false**
- Fill Rectangle → Image → Color = green (will be driven by UIManager)
- Delete the Handle Slide Area

### 7b. Health Bar — Player 2 (right side)
Duplicate HealthBarP1 → rename `HealthBarP2`  
- Anchor: top-right, mirror position `(300, -30)`
- **Flip the fill direction:** Slider → Direction = Right to Left

### 7c. Timer Text (centre top)
- Create UI → TextMeshPro Text → rename `TimerText`
- Anchor: top-centre, position `(0, -25)`
- Text = `99`, Font size = 72, Bold, colour = White
- Assign `m5x7` font from `Assets/UI/Fonts/`

### 7d. Announcement Text (centre)
- Create UI → TextMeshPro Text → rename `AnnouncementText`
- Anchor: middle-centre, position `(0, 0)`
- Font size = 64, Bold, colour = Yellow
- Alignment: Centre

### 7e. Score displays
- Two small TextMeshPro texts below each health bar showing round wins (●)
- `P1ScoreText` (anchor top-left), `P2ScoreText` (anchor top-right)

### 7f. Match Over Panel
- Create UI → Panel → rename `MatchOverPanel`
- Set it to inactive by default
- Add child TextMeshPro `ResultText` (large, centre)
- Add two Buttons: `RematchButton` ("REMATCH") and `QuitButton` ("QUIT")

### 7g. Screen Flash (full-screen Image)
- Create UI → Image → rename `ScreenFlashImage`
- Set to full stretch (anchor all-four-corners)
- Color = `(1,1,1,0)` (white, fully transparent)
- Add `ScreenFlash.cs` script to the Canvas (or a child), assign `flashImage` field

### 7h. UIManager GameObject
Create empty `UIManager` GameObject (or add to Canvas root). Add `UIManager.cs`:

Assign every text/slider/image/panel reference from the above elements.

---

## 8. Audio Manager Setup

Create empty `AudioManager` GameObject. Add `AudioManager.cs`.  
Drag and drop the following clips from the **Project panel**:

| Field           | File path in Assets/Audio                                              |
|-----------------|------------------------------------------------------------------------|
| Light Swing     | `SFX/12_Player_Movement_SFX/56_Attack_03.wav`                          |
| Heavy Swing     | `SFX/10_Battle_SFX/22_Slash_04.wav`                                    |
| Light Hit       | `SFX/12_Player_Movement_SFX/61_Hit_03.wav`                             |
| Heavy Hit       | `SFX/10_Battle_SFX/15_Impact_flesh_02.wav`                             |
| Death           | `SFX/10_Battle_SFX/69_Enemy_death_01.wav`                              |
| Jump            | `SFX/12_Player_Movement_SFX/30_Jump_03.wav`                            |
| Land            | `SFX/12_Player_Movement_SFX/45_Landing_01.wav`                         |
| Round Start     | `SFX/10_Battle_SFX/55_Encounter_02.wav`                                |
| UI Confirm      | `SFX/10_UI_Menu_SFX/013_Confirm_03.wav`                                |
| Battle Music Intro | `Music/10 Battle1 (8bit style)/Assets_for_Unity/8bit-Battle01_intro.ogg` |
| Battle Music Loop  | `Music/10 Battle1 (8bit style)/Assets_for_Unity/8bit-Battle01_loop.ogg`  |

---

## 9. Damage Numbers Prefab

1. Create a new **empty GameObject** in the scene, rename `DamageNumberPrefab`
2. Add `TextMeshPro` component (set render mode to **World Space**)
3. Add `DamageNumber.cs` script
4. Set Font Size = 4, alignment = Centre, font = m5x7
5. Drag this GameObject into `Assets/Characters/Player/` to create a **Prefab**, then delete from scene
6. On each Player, find `DamageNumberSpawner` → assign `Damage Number Prefab` field

---

## 10. Player Layer Collision Matrix

`Edit → Project Settings → Physics 2D → Layer Collision Matrix`

- Player vs Player = ✓ (so attack hitboxes detect each other)
- Player vs Ground = ✓
- Player vs Player = **uncheck** the body collision (use a separate hurtbox layer) —  
  OR keep it on and the CombatSystem's overlap check will handle not self-hitting

---

## 11. Build Settings

`File → Build Settings`
- Add `Scenes/Gameplayscene` to the build
- Platform: PC, Mac & Linux Standalone
- Target: Windows x86_64

---

## 12. Controls Reference Card

| Action       | Player 1          | Player 2               |
|--------------|-------------------|------------------------|
| Move Left    | `A`               | `Numpad 4`             |
| Move Right   | `D`               | `Numpad 6`             |
| Jump         | `W` or `Space`    | `Numpad 8`             |
| Double Jump  | Press jump again while airborne ||
| Fast Fall    | Hold `S` while falling | Hold `Numpad 5` while falling |
| Light Attack | `J`               | `Numpad 0`             |
| Heavy Attack | `K`               | `Numpad Enter`         |

---

## 13. Gameplay Summary

| Feature                | Implementation                                      |
|------------------------|-----------------------------------------------------|
| Movement               | Brawlhalla-style: run, jump, double-jump, fast-fall |
| Attacks                | Light (fast/weak) + Heavy (slow/strong) with directional knockback |
| Health System          | 100 HP per player; colour bar green→yellow→red      |
| Timer                  | 99-second countdown; goes red under 10 sec          |
| Round System           | Best of 3 rounds (configurable)                     |
| Round Intro            | "ROUND X" → "FIGHT!" text (Street Fighter style)   |
| Win Condition          | KO opponent OR have more HP when timer runs out     |
| Damage Numbers         | Floating pop-ups on each hit                        |
| Screen Flash           | White flash on heavy hits                           |
| Audio                  | Battle music + swing / hit / jump / death SFX       |
| Camera                 | Auto-zoom to frame both players at all times        |
| Death Zone             | Fall below arena = instant KO (Brawlhalla blast zone) |

---

*All scripts are in `Assets/Scripts/`. No third-party packages beyond TextMeshPro and the Input System (both included in Unity 2022.3 LTS) are required.*
