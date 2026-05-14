# Core gameplay patch

This patch updates the current sprite-sheet build to better match the documented core gameplay goals:
- two local players
- movement states
- attacks and hit detection
- health UI
- timer and round flow
- platform arena
- smoother ground landing and less floating

Files changed:
- Assets/Scripts/PlayerController.cs
- Assets/Scripts/UIManager.cs
- Assets/Scripts/Editor/AutoSetup.cs

---

## Iteration 1 — Gameplay bug fixes (kept)

Scripts touched: `AnimationController.cs`, `PlayerController.cs`, `CombatSystem.cs`, `GameStateManager.cs`, `ArenaManager.cs`.

- **Duplicate animator updates** — `AnimationController` was redundant with `PlayerController.UpdateAnimator`. Now an empty stub.
- **Audio feedback wired in** — `AudioManager.Play___()` calls hooked into jump, land, swing, hit, death, round start.
- **Death zone i-frame bug** — `ArenaManager.CheckPlayer` now uses `ForceDeath()` (bypasses i-frames). Threshold aligned with `PlayerController.deathY` at -8.5.
- **ScreenFlash** — now fires on every hit, stronger flash on heavy.
- **Attack cancel on hit** — `CombatSystem` subscribes to its own `HealthManager.OnHealthChanged`; mid-attack damage stops the routine and clears `isAttacking`.
- **Friendly-fire i-frame respect** — `PerformHitCheck` skips invincible victims so the swing isn't consumed on a no-op.
- **Hit-feedback scales with attack type** — heavy hits use longer hit-stop and stronger camera shake.

These changes do not alter any scene or serialized field — the original MainMenu and GameplayScene from master continue to work exactly as before, with the added polish.

---

## Iteration 2 — Multiplayer foundation (added, NOT auto-wired into scenes)

Adds the scaffolding for online multiplayer without touching the existing scenes. Nothing here changes how the current local-play scenes look or play.

### Packages added
`Packages/manifest.json` gained:
- `com.unity.netcode.gameobjects` 2.1.1
- `com.unity.transport` 2.5.1

Unity will restore them on next open. If anything fails to compile after restore, open **Window / Package Manager**, search "Netcode for GameObjects", and click Install.

### New runtime scripts (passive — only run if you wire them up)
- `Assets/Scripts/Data/CharacterProfile.cs` — ScriptableObject for per-character stats (movement, combat, health, visuals).
- `Assets/Scripts/Data/MatchSettings.cs` — ScriptableObject for match rules (rounds, timer, spawns, death zone).
- `Assets/Scripts/Networking/ArenaNetworkManager.cs` — wraps Unity Netcode `NetworkManager` with `StartLocal`/`StartHost`/`StartClient`/`Shutdown`.
- `Assets/Scripts/Networking/NetworkPlayer.cs` — `NetworkBehaviour` companion that propagates ownership and synced health.
- `Assets/Scripts/Networking/LobbyMenu.cs` — UI controller for Local 1v1 / Host / Join.

### Modified gameplay scripts (additive only — old scenes still work)
- `PlayerController`, `CombatSystem`, `HealthManager` gained an optional `profile` field. When null, all behavior matches master.
- `GameStateManager` gained an optional `settings` field. When null, behavior matches master.
- All four also gained `networkInputAuthority`/`networkSimulationAuthority` flags that default to `true` (so local play is unchanged).

### New editor tool (manual to run, leaves your scenes alone)
- `Tools / Arena Combat / 5 - Build Adventurer Animator + Profile`
  Imports the rvros adventurer Individual Sprites, builds `AC_Adventurer.controller` and `Assets/Data/Adventurer_Profile.asset` + `Knight_Profile.asset` + `DefaultMatchSettings.asset`. Does **not** touch any scene. Use this if you want the second character to look like the rvros adventurer instead of a recolored knight.

### What was reverted
The 2026-05-02 visual overhaul (richer painted background + decorative props on the gameplay scene + procedurally rebuilt MainMenu/Lobby scenes) was rolled back because the result didn't match the project's intended look. `Assets/Scenes/MainMenu.unity` and `Assets/Scenes/Gameplayscene.unity` are now back to the master version. `MenuSetup.cs` and the generated `LobbyMenu.unity` were deleted. `AutoSetup.cs` is back to its master version.

---

## Recommended FYP path (for the Mortal-Kombat + Brawlhalla goal)

The current build already has the Mortal-Kombat-style fighting feel: two-player rounds, light/heavy attacks, knockback, hit-stop, screen flash, health bars with lag bars, win conditions, rematch flow. The Brawlhalla piece is the platform arena + fall-out blast zone, which is also in.

What's left for a strong FYP submission:

1. **Don't regenerate the scenes via tooling.** Polish them directly in the Unity Editor. The scene files in master are the right baseline — if you want fancier backgrounds, drag sprites into the Background hierarchy in the scene by hand and tweak by eye.
2. **Wire the multiplayer foundation into the gameplay scene.** This is the most important documented goal (FR-01, FR-09 from the Interim) and the foundation is now in place. The wire-up is small but needs to be done in the Unity Editor:
   - Add a `NetworkManager` GameObject to GameplayScene with the Netcode `NetworkManager` component + `UnityTransport`.
   - Add an empty `ArenaNetworkManager` GameObject too.
   - On each Player GameObject in the scene, add `NetworkObject`, `NetworkTransform`, `NetworkAnimator`, and `NetworkPlayer`.
   - Build a small lobby scene by hand (Canvas with three buttons: Local / Host / Join + IP/port inputs) and drop a `LobbyMenu` script on it that references the buttons.
3. **Maps (Brawlhalla-style variety).** Duplicate the existing `Gameplayscene.unity` once or twice and re-arrange the platforms inside the Editor to make 2–3 stages. Hook the chosen stage into the lobby flow with `SceneManager.LoadScene`.
4. **Iteration 1 bug fixes are already in place.** Audio, attack cancel, screen flash, death zone — those all work without any further wiring.

If at any point a tool or script generates something that looks wrong, the safe move is `git checkout master -- <file>` to roll that one file back without losing the rest.

---

## Iteration 3 — Combat depth, Block, and assessment-rubric alignment

### Combat variant system (`CombatSystem.cs`)
Replaced the two-attack model with **seven context-sensitive attack variants** selected from the same J/K inputs:

| Variant | Input | Damage | Notes |
|---|---|---|---|
| Light Punch | `J` on ground | 8 | Combo opener |
| Combo Punch | `J` within 0.55s | 10 | Combo step 2 |
| Uppercut | `J` within 0.55s | 14 | Launches victim straight up |
| Heavy Slash | `K` on ground | 20 | Slow strong swing |
| Air Kick | `J` in air | 10 | Doesn't advance combo |
| Diving Slam | `K` in air | 22 | Knocks victim straight down |
| Crouch Strike | `S+J` on ground | 6 | Low fast poke |

Implementation: `AttackProfile` struct + `ProfileFor(variant)` static table. Combo state (`_comboStep`, `_comboExpireTime`) is per-attacker, resets on heavy / air attack / damage-taken / death / round-end. `PerformHitCheck` accepts `overrideUp` / `overrideDown` so Uppercut and Diving Slam route knockback vertically.

`PlayerController.TryLightAttack/TryHeavyAttack` now reads `isGrounded` and `_move.y < -0.5f` so the right variant is selected automatically from existing keys (no new input bindings).

### Block / Defense (NEW — `PlayerController`, `HealthManager`, `NetworkPlayer`)
Defensive mechanic wired through every layer:
- **Input:** P1 holds `L`, P2 holds `Numpad .` (decimal), online players use `L`.
- **Gating:** block only engages when grounded and not attacking / hit-stunned / dead.
- **State effects:** while blocking, horizontal movement input is forced to 0, jump is denied, attacks are denied.
- **Damage mitigation:** `HealthManager.TakeDamage` multiplies damage by `blockDamageMultiplier` (default 0.25) and knockback by `blockKnockbackMultiplier` (default 0.5) when victim is blocking.
- **Network sync:** new `NetworkVariable<bool> _netBlocking` on `NetworkPlayer` (owner-write, everyone-read). Owner writes on change in `Update`; observers (including the server) mirror via `OnValueChanged`. Server applies damage against the synced block state so mitigation is authoritative.
- **Visual:** `PlayerController.ApplyBlockingVisual` tints the `SpriteRenderer` light blue (`0.55, 0.78, 1`). Driven from `LateUpdate` so both owner and observers see the same tint without needing a separate sync channel.
- **Cleanup:** `isBlocking` is reset in `OnDeath`, `ResetState`, and `ResetForNewRound`.

### UI fixes
- `UIManager.Awake` hides leftover `P1_NamePlate` / `P2_NamePlate` GameObjects at runtime (they rendered as plain white boxes due to white-on-white labels).
- `UIManager.RefreshScore` shows `WINS: X / 2` in basic ASCII (replaces `●`/`○` glyphs that weren't in the LiberationSans atlas).
- `GameStateManager.RestartMatch` fires `OnScoreUpdate(0, 0)` so the UI clears the win counter on PLAY AGAIN.

### Security hardening (`NetworkPlayer.RequestDamageServerRpc`)
Five validation layers on the damage RPC:
1. Sender must own the NetworkPlayer the RPC originated from (no spoofing).
2. Damage clamped to `MAX_TRUSTED_DAMAGE = 40`.
3. Knockback magnitude clamped to `MAX_TRUSTED_KNOCKBACK = 30`.
4. Server-side distance check: `Vector2.Distance(attacker, victim) <= 3.5m`.
5. Damage ignored unless `GameStateManager.State == Fighting`.

### Documentation
- `README.md` rewritten as a submission-quality reference: quick-start, controls table including block, attack variants table, network architecture per-script, security notes section, asset credits, known limitations.
- `TEST_PLAN.md` extended to 34 numbered tests including TC-1.4a–f (combos), TC-1.5a–e (block), TC-2.13 (block-syncs-online).
- `PATCH_NOTES.md` (this file) tracks the iteration log.

### Files touched in iteration 3
- Modified: `Assets/Scripts/PlayerController.cs`, `Assets/Scripts/CombatSystem.cs`, `Assets/Scripts/HealthManager.cs`, `Assets/Scripts/GameStateManager.cs`, `Assets/Scripts/UIManager.cs`, `Assets/Scripts/ArenaManager.cs`, `Assets/Scripts/Networking/NetworkPlayer.cs`, `Assets/Scripts/Networking/MultiplayerSceneBinder.cs`, `Assets/Scripts/Networking/ArenaNetworkManager.cs`, `Assets/Scripts/Networking/LobbyMenu.cs`, `Assets/Scripts/Networking/NetworkGameSync.cs`, `ProjectSettings/UnityConnectSettings.asset`, `ProjectSettings/EditorBuildSettings.asset`.
- Added: `Assets/Scripts/UI/PauseMenu.cs`, `Assets/Scripts/Networking/OwnerNetworkAnimator.cs`, `Assets/Scripts/Networking/MultiplayerSceneBinder.cs`, `Assets/Scripts/Editor/LobbyMenuBuilder.cs`, `README.md` (rewritten), `TEST_PLAN.md`, `MULTIPLAYER_SETUP.md`.

---

## Iteration 4 — Stances + per-variant animation speed + combo counter

Visual depth pass: every action and posture now reads distinctly on screen, even reusing the existing 2-attack sprite sheets.

### Stance system (`PlayerController.cs`)
Added a `Stance` enum (`Idle / Crouching / Blocking / Airborne`) computed each frame by `ComputeStance()`. The current stance maps to a (scale × tint) preset:

| Stance | Scale (mult vs base) | Tint |
|---|---|---|
| Idle | (1, 1, 1) | white |
| Crouching | (1.10, **0.70**, 1) | white |
| Blocking | (1.05, 0.95, 1) | light blue |
| Airborne | (0.98, 1.02, 1) | white |

`ApplyStanceVisual` writes both `visualRoot.localScale` (multiplied with `_baseVisualScale` captured in Awake) and `_visualRenderer.color`. Only re-applies when the stance changes (no per-frame churn).

Crouch is computed alongside block input in `ReadInput`:
```csharp
isCrouching = (y < -0.5f) && isGrounded && !isAttacking && !isBlocking && !isDead;
```
…and cleared in `OnDeath`, `ResetState`, and `ResetForNewRound`.

### Crouch network sync (`NetworkPlayer.cs`)
Added `NetworkVariable<bool> _netCrouching` (owner-write, everyone-read), mirroring the `_netBlocking` pattern. Owner pushes on change in `Update`, observers mirror via `OnCrouchingChanged`. The server uses the synced value when applying damage so crouch-tied hitboxes / mitigations are authoritative.

### Per-variant animator speed (`CombatSystem.cs`)
`AttackProfile` gained an `animSpeed` field. `AttackRoutine` writes `_anim.speed = p.animSpeed` on entry and restores the previous value after the cooldown ends. Settings:

| Variant | Animator speed |
|---|---|
| Light Punch | 1.00× |
| Combo Punch | **1.40×** (snappier follow-up) |
| Uppercut | 1.25× (explosive finisher) |
| Heavy Slash | **0.85×** (weighty, deliberate swing) |
| Air Kick | 1.30× |
| Diving Slam | 1.10× |
| Crouch Strike | 1.50× (very fast low poke) |

This means the same `_Attack.png` / `_Attack2.png` sprite sequences read distinctly per variant without requiring new animation clips. NetworkAnimator syncs `Animator.speed` automatically across host/client.

### Worldspace combo counter (`Assets/Scripts/UI/ComboDisplay.cs`)
New `MonoBehaviour` that builds a `TextMeshPro` label as a child of each player at runtime. Subscribes to `CombatSystem.OnComboStateChanged` and pops "x2" / "x3" / "FINISHER!" with yellow → orange → red color progression, then fades over 0.4s.

`CombatSystem.Awake` auto-attaches `ComboDisplay` if it's missing on the same GameObject, so no scene wiring is required — just build & run.

The counter is intentionally local-only (per-attacker visual). Each side sees their own combo state pop; the remote player's chain isn't broadcast (saves bandwidth, matches genre convention).

### Files touched in iteration 4
- Modified: `Assets/Scripts/PlayerController.cs` (stance enum + ComputeStance + ApplyStanceVisual + isCrouching), `Assets/Scripts/CombatSystem.cs` (animSpeed field + apply/restore in AttackRoutine + OnComboStateChanged event + auto-attach ComboDisplay), `Assets/Scripts/Networking/NetworkPlayer.cs` (_netCrouching NetworkVariable + sync logic), `README.md`, `TEST_PLAN.md`, `PATCH_NOTES.md`.
- Added: `Assets/Scripts/UI/ComboDisplay.cs` (worldspace combo counter).

---

## Iteration 5 — Local-play damage regression fix + per-variant visual flair

Two issues reported after iteration 4 testing:
1. **Health bar not taking damage in local play.** Damage was routed exclusively through `RequestDamageServerRpc` whenever `NetworkManager.IsServer` was false — which is exactly the state Local 1v1 ends up in (NetworkManager exists in the scene but isn't running). The RPC silently dropped because there's no server to receive it, so HP never changed.
2. **"Only 2 attack animations"** — all seven attack variants visually used the same `_Attack.png` / `_Attack2.png` clips. Animator speed alone wasn't visually distinct enough at a glance.

### Fix 1 — Damage routing (`CombatSystem.PerformHitCheck`)
Now branches on three states instead of two:

| Mode | Detection | Damage path |
|---|---|---|
| **Offline / Local 1v1** | `NetworkManager.IsListening` is false **OR** `ArenaNetworkManager.CurrentMode == Local` | Direct `hm.TakeDamage` on this side |
| **Online server (host)** | `NetworkManager.IsServer == true` | Direct `hm.TakeDamage` (NetworkVariable propagates HP to client) |
| **Online client** | `IsListening && !IsServer` | `RequestDamageServerRpc(...)` |

The new check covers the scene-loaded-but-not-networked case that was broken.

### Fix 2 — Per-variant visual flair (`PlayerController.ApplyAttackVisualFlair` + `CombatSystem.AttackProfile`)
Added two new fields to `AttackProfile`:
- `float visualTwist` — degrees of Z rotation applied to `visualRoot` during the swing.
- `bool finisherFlash` — when true, briefly tints the attacker white as a combo-finisher celebration.

`CombatSystem.AttackRoutine` calls `_pc.ApplyAttackVisualFlair(twist, duration, flash)` at swing start. `PlayerController` runs a coroutine that sets `visualRoot.localRotation` to the twist angle, optionally flashes the SpriteRenderer white, waits `startup + cooldown*0.6`, then restores. After restoration the stance system re-applies the correct stance tint on the next `LateUpdate`.

Per-variant twists:

| Variant | Twist |
|---|---|
| Light Punch | -4° (small forward lean) |
| Combo Punch | -8° (deeper lean for chain hit) |
| Uppercut | +18° + white flash (rear-back arc, finisher celebration) |
| Heavy Slash | -10° (heavy windup) |
| Air Kick | -25° (angled aerial kick) |
| Diving Slam | -35° (head-first plunge) |
| Crouch Strike | 0° (already crouched) |

Combined with the iteration-4 animator speed multipliers and stance scales, every attack now reads visibly different despite reusing the same 2 sprite-sheet clips. No new assets required.

### Files touched in iteration 5
- Modified: `Assets/Scripts/CombatSystem.cs` (damage routing + visualTwist/finisherFlash fields + AttackRoutine flair invocation), `Assets/Scripts/PlayerController.cs` (`ApplyAttackVisualFlair` coroutine), `README.md`, `TEST_PLAN.md`, `PATCH_NOTES.md`.

---

## Iteration 6 — Character roster + select screen

Adds a five-fighter roster + Mortal-Kombat-style character select screen, sourced entirely from sprite packs already present in the project (no internet downloads were possible).

### Roster built from existing assets
| Fighter | Asset path | HP / Light / Heavy |
|---|---|---|
| Sir Crimson (Knight Red) | `Art/freeknight/Colour1/...` | 100 / 8 / 20 |
| Sir Azure (Knight Blue) | `Art/freeknight/Colour2/...` | 100 / 8 / 20 |
| Snikt the Goblin | `Art/luizmelo_Monsters_Creatures_Fantasy/Goblin/` | 110 / 9 / 22 |
| Bonehead (Skeleton) | `Art/luizmelo_Monsters_Creatures_Fantasy/Skeleton/` | 120 / 7 / 18 |
| Sporeling (Mushroom) | `Art/luizmelo_Monsters_Creatures_Fantasy/Mushroom/` | 95 / 9 / 21 |

Monsters without explicit Jump / Fall sprites fall back to their Idle sheet for those animator states — fully functional but visually static while airborne. The roster lives in `Assets/Resources/CharacterRegistry.asset` so it's discoverable from any script via `CharacterRegistry.Instance`.

### Editor tool: `Tools / Arena Combat / Build All Characters`
New `Assets/Scripts/Editor/CharacterSetup.cs`. One menu click:
1. Slices every character's sprite sheets (auto-detects rows/cols from frame size).
2. Builds an animator controller per fighter (Idle / Run / Jump / Fall / Light / Heavy / Hit / Death) with the same parameter set the rest of the game already uses, so no further wiring is needed.
3. Generates a `CharacterProfile` ScriptableObject per fighter under `Assets/Data/Profile_*.asset` with the right stats, scale, collider, and animator reference.
4. Populates `Assets/Resources/CharacterRegistry.asset` with the full roster.

### New runtime scripts
- `Assets/Scripts/Data/CharacterRegistry.cs` — Resources-loaded singleton list of CharacterProfiles.
- `Assets/Scripts/Networking/PlayerSelection.cs` — static carrier for picked indices between Lobby and Gameplay scenes.
- `Assets/Scripts/CharacterApplier.cs` — auto-attached to each player; swaps animator controller, collider, visual scale, and stats based on the chosen index. Reads `PlayerSelection` in local play; reacts to `NetworkPlayer._netCharacterIndex` in online play.
- `Assets/Scripts/UI/CharacterSelectController.cs` — runtime UI controller for the select panel. Builds one portrait + name button per registry entry, supports Local / Host / Client modes (Local shows both rows; Host shows P1 only; Client shows P2 only).

### Networking integration (`NetworkPlayer.cs`)
- New `NetworkVariable<int> _netCharacterIndex` (server-write, everyone-read).
- Server seeds Player 1's value from `PlayerSelection.Player1Index` on spawn.
- Client sends `SubmitCharacterChoiceServerRpc(int)` after spawning with its `PlayerSelection.Player2Index`. The server clamps to roster size and writes the NetworkVariable.
- `OnCharacterIndexChanged` propagates to every peer's `CharacterApplier`, which swaps the animator + stats + collider live.
- Sender-ownership validation on the ServerRpc prevents one client from picking another's character.

### Lobby flow update (`LobbyMenu.cs` + `LobbyMenuBuilder.cs`)
- Three new inspector fields: `mainPanel`, `characterSelectPanel`, `characterSelect`.
- Clicking **Local 1V1** / **HOST GAME** / **JOIN GAME** now swaps to the character-select panel first; `Setup(mode, onConfirmed, onCancel)` configures which rows are visible. "Back" returns to the main panel.
- `LobbyMenuBuilder` generates the new panel at edit time: title, two portrait previews, two horizontal rows of pick buttons (one per character from the registry), Confirm + Back.

### Files touched in iteration 6
- Added: `Assets/Scripts/Data/CharacterRegistry.cs`, `Assets/Scripts/Networking/PlayerSelection.cs`, `Assets/Scripts/CharacterApplier.cs`, `Assets/Scripts/UI/CharacterSelectController.cs`, `Assets/Scripts/Editor/CharacterSetup.cs`.
- Modified: `Assets/Scripts/Networking/NetworkPlayer.cs` (char index NetworkVariable + ServerRpc + CharacterApplier auto-attach), `Assets/Scripts/Networking/LobbyMenu.cs` (panel-swap flow), `Assets/Scripts/Editor/LobbyMenuBuilder.cs` (generates char-select panel), `README.md`, `PATCH_NOTES.md`.

### Caveats
- Goblin, Skeleton, Mushroom don't have native jump/fall sprite sheets so their airborne pose reuses Idle. Functional but visually static while jumping — acceptable for the FYP scope.
- I cannot download new sprite packs from the internet — image fetch isn't a capability I have. To add more characters later, drop a sprite-sheet folder into `Assets/Art/...` and add an entry to `CharacterSetup.BuildSpecs()`.

---

## Iteration 7 — Scope revert (final FYP-stable state)

Iterations 4-6 expanded scope into 7-attack combos, stance variants, a combo counter UI, and a character roster + select screen. Several of those changes introduced regressions (HP not draining, characters mis-sized, scene size mismatches) that proved costly to debug under FYP-submission time pressure. This iteration rolls those features back to the **iteration-3 stable baseline** so the report can be written against a known-working build.

### What was reverted
- `CombatSystem.cs`: returned to the simple `DoLightAttack()` / `DoHeavyAttack()` model. Removed `AttackVariant`, `AttackProfile`, `ProfileFor`, combo state tracking, animator speed manipulation, visual twist / finisher flash, and the per-variant `OnComboStateChanged` event.
- `PlayerController.cs`: removed `Stance` enum, `ComputeStance` / `ApplyStanceVisual`, `isCrouching` field, the visual flair coroutine. Block tint still works through a simpler `ApplyBlockingVisual` call on `LateUpdate`. The block input + damage mitigation is unchanged.
- `NetworkPlayer.cs`: removed `_netCrouching` NetworkVariable, `_netCharacterIndex` NetworkVariable, `SubmitCharacterChoiceServerRpc`, the CharacterApplier auto-attach. Kept `_netHealth`, `_netPlayerIndex`, `_netBlocking`, `RequestDamageServerRpc` with anti-cheat clamps, `ResetForNewRoundClientRpc`.
- `LobbyMenu.cs` + `LobbyMenuBuilder.cs`: removed the character-select panel / mode-swap flow. Local / Host / Join now go straight to the gameplay scene as in iteration 3.

### Files deleted
- `Assets/Scripts/UI/ComboDisplay.cs` (iteration 4)
- `Assets/Scripts/Data/CharacterRegistry.cs` (iteration 6)
- `Assets/Scripts/Networking/PlayerSelection.cs` (iteration 6)
- `Assets/Scripts/CharacterApplier.cs` (iteration 6)
- `Assets/Scripts/UI/CharacterSelectController.cs` (iteration 6)
- `Assets/Scripts/Editor/CharacterSetup.cs` (iteration 6)
- `.meta` companions for all of the above.

### What was preserved
Everything from iterations 1 through 3 is intact:
- Iteration 1 gameplay bug fixes (audio hooks, attack-cancel-on-hit, ScreenFlash, death zone i-frame fix, hit-stop scaling).
- Iteration 2 multiplayer foundation (Netcode packages, ArenaNetworkManager, NetworkPlayer, LobbyMenu, MultiplayerSceneBinder, NetworkGameSync, OwnerNetworkAnimator).
- Iteration 3 block system (input, damage reduction, knockback reduction, network sync of `isBlocking`, sprite tint).
- Iteration 3 security hardening (`RequestDamageServerRpc` clamps on damage, knockback, distance + sender ownership check + match-state gate).
- Iteration 3 UI fixes (nameplates hidden, score uses `WINS: X / 2` text, score resets on rematch).
- Iteration 3 disconnect / forfeit handling, PLAY AGAIN that returns to menu when opponent has left, EnsureCleanNetworkState lobby coroutine.
- Iteration 5 damage routing fix (local / server / client three-way branch in `PerformHitCheck`).

### Final feature set (what the report should describe)
- Movement: walk / run / jump / double-jump / fast-fall on a 2D platform arena
- Combat: light attack (J) and heavy attack (K) with knockback, i-frames, hit-stop, screen flash, camera shake
- Block: hold L (P1) / Numpad `.` (P2) — 75% damage reduction, 50% knockback reduction, sprite tint, networked
- Round system: best-of-3, configurable timer, win declarations
- Brawlhalla-style fall-out blast zone
- Match-over panel: Play Again / Main Menu / Quit; pause menu via Escape
- Local 2-player split-keyboard mode
- Online 2-player over direct IP via Unity Netcode for GameObjects
- Server-authoritative damage with anti-cheat clamps
- Audio: jump, land (gated on impact velocity), light/heavy swing + hit, death, round start, battle music loop
- Parallax hand-painted background with sun, clouds, cliffs, trees, fog

### Files touched in iteration 7
- Modified: `Assets/Scripts/CombatSystem.cs`, `Assets/Scripts/PlayerController.cs`, `Assets/Scripts/Networking/NetworkPlayer.cs`, `Assets/Scripts/Networking/LobbyMenu.cs`, `Assets/Scripts/Editor/LobbyMenuBuilder.cs`, `README.md`, `TEST_PLAN.md`, `PATCH_NOTES.md`.
- Deleted: `Assets/Scripts/UI/ComboDisplay.cs`, `Assets/Scripts/Data/CharacterRegistry.cs`, `Assets/Scripts/Networking/PlayerSelection.cs`, `Assets/Scripts/CharacterApplier.cs`, `Assets/Scripts/UI/CharacterSelectController.cs`, `Assets/Scripts/Editor/CharacterSetup.cs` (+ their `.meta` files).
