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
