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

Important: the uploaded project still does **not** fully implement online multiplayer or true bone-rig animation assets. Those were listed in the proposal/PID, but the interim report says the current implementation switched to sprite-sheet animation and networking had not started yet.

---

## Gameplay bug fixes (this iteration)

Pass focused on remaining gameplay bugs found while auditing the build.

### Files changed
- Assets/Scripts/AnimationController.cs
- Assets/Scripts/PlayerController.cs
- Assets/Scripts/CombatSystem.cs
- Assets/Scripts/GameStateManager.cs
- Assets/Scripts/ArenaManager.cs

### What was fixed

**1. Duplicate animator updates** — `AnimationController.Update()` was writing the same animator parameters (`isMoving`, `isRunning`, `isGrounded`, `velocityY`) that `PlayerController.UpdateAnimator()` already writes during `FixedUpdate`. Both components are added to every player by `AutoSetup`, causing redundant per-frame work and a possible source of inconsistent state between physics and animation steps. `AnimationController` is now an empty stub so existing scene references stay valid; `PlayerController` is the single source of truth.

**2. Missing audio feedback** — `AudioManager` had clip slots and a full play API but was never called by gameplay code. Hooks added:
- `PlayJump()` from `PlayerController.TryJump`
- `PlayLand()` from `PlayerController.CheckGround` when the player transitions from airborne to grounded with downward impact velocity below `-6` (avoids a false "land" sound on round-start drop)
- `PlayLightSwing` / `PlayHeavySwing` at the start of `CombatSystem.AttackRoutine`
- `PlayLightHit` / `PlayHeavyHit` from `CombatSystem.PerformHitCheck` when a hit connects
- `PlayDeath` from `PlayerController.OnDeath`
- `PlayRoundStart` from `GameStateManager.DoRoundIntro` once the round transitions to `Fighting`

**3. Death zone blocked by i-frames** — `ArenaManager.CheckPlayer` killed fall-out players via `TakeDamage(9999, Vector2.zero)`, but `HealthManager.TakeDamage` returns early when the victim has invincibility frames. A player knocked off the stage immediately after being hit could survive their fall. Replaced with `HealthManager.ForceDeath()` which bypasses i-frames.

**4. Two competing fall-out thresholds** — `PlayerController.deathY = -8.5` and `ArenaManager.deathZoneY = -12` were inconsistent, so the arena-level safety net never actually fired. `ArenaManager.deathZoneY` is now `-8.5` to match.

**5. ScreenFlash never triggered** — the ScreenFlash component existed but no caller invoked it. `CombatSystem.PerformHitCheck` now triggers `ScreenFlash.Instance.Flash(isHeavy)` on connection, with a stronger flash on heavy hits.

**6. Attacks continued firing after the attacker was hit** — getting struck during attack startup left the active `AttackRoutine` running, so the swing's hitbox would still resolve. `CombatSystem` now subscribes to its own `HealthManager.OnHealthChanged`; on a damage event mid-attack, the routine is stopped, the active-attack handle is cleared, and `isAttacking` is reset.

**7. Hit-feedback intensity now scales with attack type** — heavy hits use longer hit-stop (0.12s vs 0.08s) and a stronger camera shake (0.18s/0.36 amplitude vs 0.12s/0.25). Light hits remain crisp; heavy hits feel weighty.

**8. Friendly-fire i-frame respect** — `CombatSystem.PerformHitCheck` skips victims that are currently invincible instead of treating them as the first valid target and consuming the swing on a no-op. Lets the swing pass through and find a different target if any are present.

### What this does NOT touch
Online multiplayer, networking sync, and bone-rig animation are still out of scope for this pass — the interim report continues to flag them as not-yet-started.
