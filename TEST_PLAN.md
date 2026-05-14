# Test Plan — 2D Arena Combat Game

This document is the manual test plan used to verify the build before submission. It maps each test to the **Functional Requirements** documented in the Interim Report and the **marking criteria** ("Testing Methods", "Security Concerns") of the PUSL3190 Coursework C1 rubric.

Each test is independently runnable and produces a binary pass/fail outcome. Steps assume Unity 6 is installed and the project has been opened at least once so packages are restored.

---

## How to test multiplayer locally

You don't need two machines. The standard procedure is:

1. Run `File → Build Profiles → Build`, output to e.g. `D:\NSBM\FYP\Build\`.
2. Launch the resulting `.exe` — this is **Window A**.
3. Switch back to Unity → press **Play** in the Editor — this is **Window B**.
4. In Window A click **PLAY → HOST GAME** (it becomes the host).
5. In Window B click **PLAY → JOIN GAME** with IP `127.0.0.1`, port `7777`.

For all networked tests below, "host" = Window A (.exe), "client" = Window B (Editor).

---

## Section 1 — Core gameplay (single-instance / local play)

### TC-1.1: Player 1 movement (FR-02)
**Steps:** From MainMenu click PLAY → Lobby → Local 1v1.
1. Press A — Player 1 walks left.
2. Press D — Player 1 walks right.
3. Press W (or Space) — Player 1 jumps.
4. Press W again while airborne — Player 1 double-jumps.
5. Press S while airborne and descending — fast-fall accelerates downward.

**Pass criteria:** All transitions trigger correctly, character returns to idle pose after movement stops.

### TC-1.2: Player 2 movement (FR-02)
Same as TC-1.1 but using Numpad keys for Player 2 (Numpad 4 / 6 / 8 / 5).

### TC-1.3: Light attack (FR-03, FR-04)
**Steps:** Local 1v1. P1 moves next to P2.
1. Press J — P1 swings light attack.
2. If close enough, P2's HP decreases by ~8.
3. P2 receives knockback and brief invincibility frames (subsequent hits within 0.25s are ignored).

**Pass criteria:** HP bar drops, hit-stop fires, screen flashes white, camera shakes.

### TC-1.4: Heavy attack (FR-03, FR-04)
Same as TC-1.3 but press K. HP should drop by ~20 and feedback is stronger (longer hit-stop, bigger shake).

### TC-1.5a: Block reduces damage
**Steps:** Local 1v1. Have P2 hold L (or Numpad `.`) to block. P1 attacks P2.
**Pass criteria:**
- P2's sprite tints light blue while the key is held.
- P2's HP drops by ~25% of the normal value (75% reduction).
- P2's knockback is visibly weaker (halved).
- After release, sprite returns to white and damage is full again.

### TC-1.5b: Block restricts actions
**Steps:** Hold the block key.
**Pass criteria:**
- A/D doesn't move the character (frozen on the spot).
- W / Space doesn't jump.
- J / K doesn't attack.
- Releasing the block key restores all actions.

### TC-1.5c: Aerial block not allowed
**Steps:** Jump, then while airborne hold the block key.
**Pass criteria:** `isBlocking` stays false. Sprite stays white.

### TC-1.5d: Block resets on death and new round
**Pass criteria:** Sprite tint clears immediately on death. `isBlocking` is false at round start.

### TC-1.6: Fall-out blast zone
Push P2 off the side of the main platform so they fall below the arena.
**Pass criteria:** P2 dies on hitting the death-Y threshold (-8.5). Round ends in P1's favour.

### TC-1.7: Time-up round
Wait for the round timer to reach 0 without either player dying.
**Pass criteria:** Round ends; player with higher HP wins. If HP is equal, "TIME! DRAW!" is announced.

### TC-1.8: Pause menu
Press Escape mid-fight.
**Pass criteria:** Pause overlay appears, game freezes (Time.timeScale = 0). Resume unfreezes; Quit to Menu returns to MainMenu cleanly.

### TC-1.9: Round flow + match end
**Steps:** Play until one player wins two rounds.
**Pass criteria:** Each round-end shows "PLAYER X WINS ROUND!" with score update. After 2 wins, match-over panel shows "PLAYER X\nWINS!".

### TC-1.10: PLAY AGAIN
Click PLAY AGAIN on match-over panel.
**Pass criteria:** Score resets to 0/0 on UI. New round 1 begins.

---

## Section 2 — Online multiplayer

### TC-2.1: Host / Join handshake (FR-01)
1. Window A clicks Host. Arena loads showing "WAITING FOR OPPONENT...".
2. Window B clicks Join. After ~1s, the waiting message clears and both windows show "ROUND 1 / FIGHT!".

**Pass criteria:** Both players visible on both screens, each side controls only their player.

### TC-2.2: Movement sync (FR-09)
1. In Window A press WASD → Player 1 moves on both screens identically.
2. In Window B press WASD → Player 2 moves on both screens identically.

**Pass criteria:** Movement is replicated; the host doesn't accidentally drive Player 2 and the client doesn't drive Player 1.

### TC-2.3: Animation sync (FR-09)
Have each player jump, attack, get hit.
**Pass criteria:** Animator state (run / jump / fall / attack / hit / death) is consistent across both windows.

### TC-2.4: HP sync (FR-05, FR-09)
Player 1 attacks Player 2. Watch Player 2's HP bar.
**Pass criteria:** Player 2's HP bar drains on **both** windows simultaneously, to the same value.

### TC-2.5: Timer sync (FR-06)
**Pass criteria:** The match timer counts down at the same rate on both screens. No drift across a 99-second round.

### TC-2.6: Score sync
**Pass criteria:** When a round ends, both windows update the WINS counter at the same time.

### TC-2.7: Spawn reset on new round
**Pass criteria:** After Round 1 ends, both Player 1 and Player 2 snap to spawn positions on both screens.

### TC-2.8: Player 2 attacks register
Have Player 2 (client side) hit Player 1.
**Pass criteria:** Player 1's HP drops on both screens. (Tests the client→server damage RPC path.)

### TC-2.9: Rematch (online)
After a match, click PLAY AGAIN on either side.
**Pass criteria:** Both windows reset to ROUND 1 / FIGHT!, score returns to 0/0, both players at spawn with full HP.

### TC-2.10: Client disconnect (graceful)
Mid-match, close the client window or click MENU on the client side.
**Pass criteria:** Host shows "OPPONENT LEFT / MATCH ENDED" + match-over panel. PLAY AGAIN returns host to MainMenu. No exceptions in the Console.

### TC-2.11: Host disconnect (graceful)
Mid-match, close the host window.
**Pass criteria:** Client returns to MainMenu automatically. No stale NetworkObjects, no error spam.

### TC-2.12: Re-host / re-join after a match
After one full match + Menu → Lobby, the host clicks Host again.
**Pass criteria:** New session starts cleanly. No "Cannot start Client while an instance is already running" error.

### TC-2.13: Block syncs across network
Host blocks (hold L). Observe client window.
**Pass criteria:** Client sees host's player tinted light blue. When client attacks the host's blocking player, host's HP drops by the reduced amount on both screens.

---

## Section 3 — Security / validation tests

### TC-3.1: Damage cap on RPC
**Setup:** With networked play active, temporarily set `lightDamage = 999` on the client. Attack.
**Expected:** Server's `RequestDamageServerRpc` clamps `damage` to `MAX_TRUSTED_DAMAGE = 40`. Player 1's HP drops by 40, not 999.

### TC-3.2: Out-of-range damage rejected
**Setup:** With networked play active, temporarily increase `heavyAttackRadius` to 100 on the client. Attack from the far side of the arena.
**Expected:** Server's distance check (`MAX_TRUSTED_HIT_DISTANCE = 3.5`) rejects the request. No HP change.

### TC-3.3: Spoofed sender rejected
**Conceptually:** A malicious client cannot send a `RequestDamageServerRpc` on someone else's NetworkPlayer because the server checks `OwnerClientId == senderId`.
**Expected:** Damage from a non-owner is silently dropped.

### TC-3.4: Damage during intro rejected
During the "ROUND X" / "FIGHT!" intro sequence (before State becomes Fighting), trigger an attack.
**Expected:** Damage is not applied (server checks `gsm.State == Fighting`).

### TC-3.5: Damage after match over rejected
After a match ends, try to continue attacking.
**Expected:** Damage is gated by State and ignored.

---

## Section 4 — UX edge cases

### TC-4.1: Pause during online play
**Note:** `Time.timeScale = 0` only freezes the local instance; in networked play the remote side keeps simulating. Local UI shows pause overlay; remote continues to play.

### TC-4.2: Score display
**Expected:** "WINS: X / 2" text renders correctly (no glyph-missing boxes). Score increments at end of each round and resets on rematch.

### TC-4.3: Match-over panel buttons
**Expected:** PLAY AGAIN, MAIN MENU, QUIT all behave correctly. MAIN MENU calls `ArenaNetworkManager.Shutdown()` before scene-loading.

---

## Test execution log

Fill this in as you run the suite for submission:

| Test | Date | Result | Notes |
|---|---|---|---|
| TC-1.1 | | | |
| TC-1.2 | | | |
| TC-1.3 | | | |
| TC-1.4 | | | |
| TC-1.5a | | | |
| TC-1.5b | | | |
| TC-1.5c | | | |
| TC-1.5d | | | |
| TC-1.6 | | | |
| TC-1.7 | | | |
| TC-1.8 | | | |
| TC-1.9 | | | |
| TC-1.10 | | | |
| TC-2.1 | | | |
| TC-2.2 | | | |
| TC-2.3 | | | |
| TC-2.4 | | | |
| TC-2.5 | | | |
| TC-2.6 | | | |
| TC-2.7 | | | |
| TC-2.8 | | | |
| TC-2.9 | | | |
| TC-2.10 | | | |
| TC-2.11 | | | |
| TC-2.12 | | | |
| TC-2.13 | | | |
| TC-3.1 | | | |
| TC-3.2 | | | |
| TC-3.3 | | | |
| TC-3.4 | | | |
| TC-3.5 | | | |
| TC-4.1 | | | |
| TC-4.2 | | | |
| TC-4.3 | | | |
