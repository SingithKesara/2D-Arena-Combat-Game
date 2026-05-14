# 2D Arena Combat Game

**PUSL3190 Computing Project – Final Year**
Author: Singith Kesara Wahalathanthri · Plymouth Index 10952382
Supervisor: Mr. Anton Jayakody
Programme: BSc (Hons) Technology Management, NSBM / University of Plymouth

A Unity 6 two-player 2D arena fighter prototype with local-split and online (Unity Netcode for GameObjects) multiplayer.

---

## 1. Quick Start

### Prerequisites
- Unity 6 (6000.4.0f1 or later)
- Windows 10 / 11 (build target)

### Open the project
1. Clone or download this repository.
2. Open the folder with Unity Hub → "Add project from disk" → select the project root.
3. Unity restores the packages listed in `Packages/manifest.json` automatically (this includes Netcode for GameObjects 2.1 and Unity Transport 2.5).
4. Open `Assets/Scenes/MainMenu.unity` and press **Play** to test.

### Make a build (for the multiplayer test)
1. **File → Build Profiles → Build** → choose an output folder.
2. Once built, run the `.exe` in one window and press Play in the Editor for the second window.
3. .exe → **PLAY → HOST GAME**. Editor → **PLAY → JOIN GAME** (default IP `127.0.0.1` / port `7777`).

---

## 2. Features

### Gameplay
- **Local 2-player split-keyboard** (P1 = WASD / J / K, P2 = Numpad).
- **Online 2-player over LAN or direct IP** via Unity Netcode for GameObjects.
- Movement: walk / run / jump / double-jump / fast-fall.
- **Block / defense system**: hold the block key to reduce incoming damage 75% and knockback 50%. Sprite tints blue while blocking.
- **Combat**: light attack (J — 8 dmg) and heavy attack (K — 20 dmg) with knockback, invincibility frames, hit-stop, screen flash, and camera shake. Server-authoritative damage with anti-cheat clamping.
- Health bars with smooth lag-bar trailing effect.
- Round-based match flow: best-of-3, configurable timer (default 99s).
- Brawlhalla-style fall-out blast zone — knocking the opponent off the arena KOs them.
- Match-over panel with **Play Again** / **Main Menu** / **Quit**.
- Pause menu (Escape) with Resume / Quit to Menu — pauses time locally in single-instance play.

### Multiplayer
- **Host / Join** lobby flow with IP + port input.
- Player ownership transferred to joining client; host always controls Player 1, client always Player 2.
- Position sync via `NetworkTransform` (Owner authority mode).
- Animation sync via custom `OwnerNetworkAnimator` (subclass of `NetworkAnimator` with server-authority overridden).
- Health sync via `NetworkVariable<int>` on `NetworkPlayer`.
- Timer / score / round-intro / match-result sync via `NetworkGameSync` (NetworkVariable + ClientRpc).
- Damage application is **server-authoritative**: clients send `RequestDamageServerRpc` and the server validates ownership, distance, and damage magnitude before applying.
- Graceful disconnect handling: host sees "OPPONENT LEFT / MATCH ENDED" and can return to menu; client returns to main menu automatically.

### Polish & Feel
- Hit-stop (frame freeze) on connect — scaled by light vs heavy.
- Camera shake on hits, scaled by attack type.
- Screen flash on heavy hits.
- Damage numbers floating up from victims.
- Audio: jump, land (gated on real impact velocity), light/heavy swing, light/heavy hit, death, round start, battle music loop with intro.
- Parallax background scrolling (Sun, Clouds, Cliffs, Trees, Fog) using the hand-painted environment pack.

---

## 3. Controls

### Local Play (split-keyboard)
| Action | Player 1 | Player 2 |
|---|---|---|
| Move | A / D | Numpad 4 / 6 |
| Jump | W or Space | Numpad 8 |
| Fast Fall | S | Numpad 5 |
| Light Attack | J | Numpad 0 |
| Heavy Attack | K | Numpad Enter |
| **Block (hold)** | **L** | **Numpad . (decimal)** |
| Pause | Escape | Escape |

### Block / Defense
Hold the **Block** key (L for P1, Numpad `.` for P2) to raise your guard:
- Sprite tints **light blue** to show you're blocking.
- Damage taken is reduced by **75%** (configurable on `HealthManager.blockDamageMultiplier`).
- Knockback is halved (`blockKnockbackMultiplier`).
- You can't move, jump, or attack while blocking — committing to defense is a tradeoff.
- You can only block on the ground; aerial block is not allowed.
- Block state is synced over the network (NetworkVariable<bool> on `NetworkPlayer`), so the host sees the client's block tint and the server applies the reduced damage authoritatively.

### Online Play (each side uses its own keyboard)
Both players use the **Player 1 key set** (WASD / J / K / Space / L) — they each control their character from their own machine.

---

## 4. Architecture

### Core gameplay scripts (`Assets/Scripts/`)
| Script | Responsibility |
|---|---|
| `PlayerController.cs` | Input read, physics, ground check, fall-out detection, double jump, animation parameter writes, block input + visual tint. |
| `CombatSystem.cs` | Light/heavy attack timing, hit-box overlap, knockback, hit feedback (audio, screen flash, hit-stop, camera shake), damage routing (local vs networked). |
| `HealthManager.cs` | HP tracking, i-frames, block-damage mitigation, server-authoritative damage application, `OnHealthChanged` event for UI. |
| `GameStateManager.cs` | Match flow: rounds, timer, win conditions, intro/result announcements, waiting-for-opponent state, forfeit on disconnect. |
| `ArenaManager.cs` | Blast-zone (Y threshold) fall-out kills and camera framing. |
| `ArenaCameraController.cs` | Dynamic camera framing both players, zoom on distance, shake offset. |
| `UIManager.cs` | HUD: health bar with lag-bar, timer, score, announcement text, match-over panel buttons. |
| `MainMenuManager.cs` | Main menu Play/Controls/Quit + fade transition. |
| `AudioManager.cs` | Singleton audio with named Play methods for each SFX category, plus music loop. |

### Networking (`Assets/Scripts/Networking/`)
| Script | Responsibility |
|---|---|
| `ArenaNetworkManager.cs` | Singleton wrapper around `NetworkManager` with `StartLocal`/`StartHost`/`StartClient`/`Shutdown` and robust busy-state detection. |
| `LobbyMenu.cs` | UI controller for the Lobby scene: Local 1v1 / Host / Join + IP/port fields + status text. Auto-cleans stale sessions on entry. |
| `NetworkPlayer.cs` | `NetworkBehaviour` attached to each player. Manages ownership-aware input/sim authority, syncs HP and block state via NetworkVariables, validates incoming damage RPCs. |
| `MultiplayerSceneBinder.cs` | Lives in the gameplay scene. Transfers Player 2 ownership to the joining client, ends match on disconnect, returns disconnected clients to main menu. |
| `NetworkGameSync.cs` | Mirrors `GameStateManager` events to clients via `NetworkVariable<float>` (timer), `NetworkVariable<int>` (wins) and ClientRpcs (round-intro / match-result strings). Also exposes `RequestRestartServerRpc` for the client-side Play-Again button. |
| `OwnerNetworkAnimator.cs` | Subclass of `NetworkAnimator` that returns `false` from `OnIsServerAuthoritative` so each player's animation is driven by its owner. |

### UI (`Assets/Scripts/UI/`)
| Script | Responsibility |
|---|---|
| `PauseMenu.cs` | Runtime-built pause overlay (Escape toggles). Resume / Quit to Menu. Quit calls `ArenaNetworkManager.Shutdown` before loading MainMenu. |
| `MenuAutoSelect.cs` | First-button auto-focus for keyboard / controller navigation. |

### Data (`Assets/Scripts/Data/`)
| ScriptableObject | Purpose |
|---|---|
| `CharacterProfile` | Movement / combat / health / visual values per character. Optional inspector override for the inline default values on each gameplay script. |
| `MatchSettings` | Match rules (rounds-to-win, timer, spawn positions, death-zone Y). |

### Editor tools (`Assets/Scripts/Editor/`)
Available via the **Tools / Arena Combat** menu:
1. `2 - Slice Used Knight Sheets` — slices `freeknight` Colour1/Colour2 sheets into Sprite assets.
2. `3 - Build Animator Controllers` — builds `AC_Player1` / `AC_Player2` controllers.
3. `4 - Build Verified Arena Gameplay Scene` — generates a fresh GameplayScene.
4. `5 - Build Adventurer Animator + Profile` — slices rvros adventurer sprites and builds a CharacterProfile + animator (optional alternate character).
5. `Build Lobby Menu Scene` — generates the LobbyMenu scene with host/join UI.
6. `Build Polished Main Menu Scene` — generates the MainMenu scene.

---

## 5. Security / anti-cheat in the multiplayer code

The brief lists "Security Concerns" as a marking criterion for the Final Product. The networked damage path is server-authoritative and validates client requests:

- `RequestDamageServerRpc` (`NetworkPlayer.cs`) only accepts requests from a client that owns the sender's NetworkPlayer instance.
- Damage values are clamped to `MAX_TRUSTED_DAMAGE = 40` (well above any legitimate attack, well below an instakill).
- Knockback magnitude is clamped to `MAX_TRUSTED_KNOCKBACK = 30`.
- Server checks `Vector2.Distance(attacker, victim) <= MAX_TRUSTED_HIT_DISTANCE = 3.5` so a client can't damage a player on the far side of the arena.
- Damage is ignored unless `GameStateManager.State == Fighting`, so attacks during the intro / round-end window can't apply.
- `HealthManager.TakeDamage` and `HealthManager.ForceDeath` are gated on `networkSimulationAuthority`, which is only `true` on the server in networked play.

---

## 6. Testing

See [TEST_PLAN.md](TEST_PLAN.md) for the manual test plan covering: movement, combat, round flow, online connection, disconnect handling, rematch, and security checks.

---

## 7. Scenes
- `Assets/Scenes/MainMenu.unity` — title screen with Play / Controls / Quit. Play loads `LobbyMenu`.
- `Assets/Scenes/LobbyMenu.unity` — generated by the `Build Lobby Menu Scene` editor tool. Local 1v1 / Host / Join.
- `Assets/Scenes/Gameplayscene.unity` — the arena. Contains both players, GameManager (with `GameStateManager` + `NetworkGameSync`), `MultiplayerSceneBinder`, `ArenaManager`, the HUD canvas, and the parallax background.

---

## 8. Tech Stack
- **Unity 6** (6000.4.0f1)
- **C#** with .NET Standard 2.1
- **Unity Input System** (new) for keyboard binding
- **Universal Render Pipeline (URP)** 2D
- **Netcode for GameObjects** 2.1.1 with **Unity Transport** 2.5.1
- **TextMeshPro** for all UI text
- Sprite animation via Animator Controllers (sprite-sheet workflow — bone-rig was deferred per Interim Report §8)

---

## 9. Asset Credits

Free / royalty-free packs combined for prototype fidelity:
- `freeknight` (Colour1 / Colour2) — knight character sheets used for both players
- `rvros-adventurer` — adventurer character sheets (alternate sprite source, available via editor tool)
- `freecutetileset` — platforms and ground decor
- `2D Hand Painted Platformer Environment` (BayatGames) — sky, clouds, trees, cliffs, mushrooms, vegetation
- `Free UI build package` (Brackeys) — UI atlas
- `henrysoftware-freepixelfood`, `kyrises-rpg-icon-pack`, `luizmelo_Monsters_Creatures_Fantasy` — additional sprite atlases available in the project

---

## 10. Known Limitations
- Bone-rig animation is not implemented; sprite-sheet animation is used instead. This was a deliberate scope adjustment documented in the Interim Report.
- Online matchmaking is direct-IP only; no relay / lobby server. Two players need to be on the same LAN or one needs port forwarding to host across the internet.
- Audio mixer / volume sliders are not exposed in the UI; volumes are inspector-configurable on the `AudioManager` component.
- Game-end pose / replays are minimal — match-over panel shows result text and routes to rematch or menu.
- Only one character variant per player is wired into the gameplay scene by default; alternate sprite sets are available via the editor tools but require manual scene wiring.
