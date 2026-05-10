# Multiplayer wire-up — step by step

Beginner-friendly walkthrough to enable online 2-player combat in your existing project. You'll wire components in the Unity Editor, no code writing needed.

**Estimated time:** 30-45 minutes the first time. Read each step fully before clicking — every word matters.

---

## Pre-flight checklist (5 min)

Before you start, confirm Unity restored the new packages.

1. Open the project in Unity.
2. Top menu → **Window → Package Manager**.
3. Top-left dropdown → switch to **Packages: In Project**.
4. You should see in the list:
   - **Netcode for GameObjects** (version 2.1.x)
   - **Unity Transport** (version 2.5.x)
5. If they're missing or have red exclamation marks, click each one and hit **Install** / **Update**.
6. Close Package Manager.
7. **Console window**: bottom panel, click **Console** tab. There should be no compile errors. If there are red errors, stop and tell me — don't continue.

---

## Part 1 — Add a NetworkManager to your gameplay scene (10 min)

Open `Assets/Scenes/Gameplayscene.unity` (double-click in Project window).

### 1.1 Create the NetworkManager GameObject

1. In the **Hierarchy** window (top-left panel), right-click empty space → **Create Empty**.
2. Rename the new GameObject to exactly: `NetworkManager`
3. Make sure it's at the root of the Hierarchy (not a child of anything). Drag to the top if needed.

### 1.2 Add the NetworkManager component

With `NetworkManager` selected:

1. In the **Inspector** (right panel), click **Add Component**.
2. Type: `Network Manager`
3. Click the result that says **Network Manager** (Unity Netcode for GameObjects icon, blue).

You'll see fields like "Network Config", "Connection Approval", etc. Leave defaults for now.

### 1.3 Add the UnityTransport component

Still on the `NetworkManager` GameObject:

1. **Add Component** again.
2. Type: `Unity Transport`
3. Click **Unity Transport**.

The component appears with fields like "Protocol Type", "Connection Data" (Address/Port).
- **Address**: leave as `127.0.0.1`
- **Port**: leave as `7777`

### 1.4 Hook UnityTransport into NetworkManager

Scroll up to the **Network Manager** component on the same GameObject:

1. Find the field labeled **Network Transport**.
2. It should now show **UnityTransport (Unity Transport)** automatically. If empty, click the small dot ⊙ next to the field and pick **UnityTransport**.

### 1.5 Create the ArenaNetworkManager helper

1. In Hierarchy, right-click → **Create Empty**.
2. Rename to: `ArenaNetworkManager`
3. With it selected, **Add Component** → type `Arena Network Manager` → click it.

### 1.6 Create the scene binder

1. In Hierarchy, right-click → **Create Empty**.
2. Rename to: `MultiplayerSceneBinder`
3. With it selected, **Add Component** → type `Multiplayer Scene Binder` → click it.
4. Leave the two slots empty for now — you'll fill them in Part 2.

### 1.7 Save

`Ctrl+S` to save the scene.

**Checkpoint:** Hierarchy now has a `NetworkManager`, `ArenaNetworkManager`, and `MultiplayerSceneBinder` at the root. Console has no errors.

---

## Part 2 — Make Player1 and Player2 network-aware (15 min)

Still in `Gameplayscene.unity`. Find `Player1` in the Hierarchy and click it.

### 2.1 Add NetworkObject to Player1

In Inspector:

1. **Add Component** → type `Network Object` → click it.
2. Fields appear: "Always Replicate As Root", "Synchronize Transform", etc. Leave all defaults.
3. **Important:** the field **Player Object** — leave UNCHECKED. (We're using scene placement, not auto-spawn.)

### 2.2 Add NetworkTransform to Player1

1. **Add Component** → type `Network Transform` → click it.
2. In the component:
   - **Synchronize Position**: X and Y checked, Z unchecked
   - **Synchronize Rotation**: all unchecked (2D — no rotation)
   - **Synchronize Scale**: leave default (X and Y checked is fine — used for facing flip)
   - **Interpolate**: checked (smooths movement on remote)

### 2.3 Add NetworkAnimator to Player1

1. **Add Component** → type `Network Animator` → click it.
2. The component asks for an **Animator** field. Drag the existing **Animator** component (already on Player1) into it, or click the dot ⊙ and pick the Animator on the same GameObject.

### 2.4 Add NetworkPlayer to Player1

1. **Add Component** → type `Network Player` → click it.
2. **Default Player Index**: set to `1`.

### 2.5 Repeat for Player2

Click `Player2` in the Hierarchy. Repeat steps 2.1 through 2.4 with one change:
- In step 2.4, set **Default Player Index** to `2`.

### 2.6 Hook the players into MultiplayerSceneBinder

Click `MultiplayerSceneBinder` in Hierarchy. Inspector shows two empty slots:

1. **Player 1 Network Object**: drag `Player1` from Hierarchy onto this slot. Unity will auto-grab its `NetworkObject` component.
2. **Player 2 Network Object**: drag `Player2` onto this slot.

### 2.7 Save

`Ctrl+S`.

**Checkpoint:** Both players have NetworkObject + NetworkTransform + NetworkAnimator + NetworkPlayer. The binder has both player slots filled.

---

## Part 3 — Build the Lobby scene by hand (10 min)

This is a small UI scene with three buttons (Local / Host / Join), two text inputs (IP / Port), and a status label.

### 3.1 Create the scene file

1. Top menu → **File → New Scene**.
2. Pick **Basic (Built-in)** template if asked.
3. `Ctrl+S` → name: `LobbyMenu` → save into `Assets/Scenes/`.

You should now have an empty scene with just `Main Camera` and `Directional Light`.

### 3.2 Add a Canvas

1. In Hierarchy, right-click empty space → **UI → Canvas**.
2. Unity creates `Canvas` and `EventSystem`. Both are needed.
3. Click `Canvas`. In Inspector → **Canvas Scaler** component:
   - **UI Scale Mode**: Scale With Screen Size
   - **Reference Resolution**: 1920 × 1080
   - **Match**: 0.5

### 3.3 Add a panel background

1. Right-click `Canvas` in Hierarchy → **UI → Panel**.
2. Rename it to `MainPanel`.
3. In Inspector → **Image** component → set color alpha to about 200 (semi-transparent dark).

### 3.4 Add the Local Play button

1. Right-click `MainPanel` → **UI → Button - TextMeshPro**.
   - If a TextMeshPro Importer popup appears, click **Import TMP Essentials**, wait, close popup.
2. Rename the button to `LocalPlayButton`.
3. Click the button. In Inspector → **Rect Transform**:
   - Set Pos Y to about `200`
   - Width: `360`, Height: `60`
4. Expand `LocalPlayButton` in Hierarchy → click its child **Text (TMP)**.
5. In Inspector, change the **Text Input** field to: `LOCAL 1V1`. Set Font Size to about `36`.

### 3.5 Add the Host button

1. Right-click `MainPanel` → **UI → Button - TextMeshPro**.
2. Rename: `HostButton`. Pos Y: `100`. Same size as before.
3. Set the child text to: `HOST GAME`.

### 3.6 Add the IP input field

1. Right-click `MainPanel` → **UI → Input Field - TextMeshPro**.
2. Rename: `IPField`. Pos Y: `0`. Width `360`, Height `40`.
3. Click the field, in Inspector → **TMP_InputField** component → **Text** field at the bottom: type `127.0.0.1`.
4. Find its child `Text Area → Placeholder` → in TextMeshPro field, type `IP Address`.

### 3.7 Add the Port input field

1. Right-click `MainPanel` → **UI → Input Field - TextMeshPro**.
2. Rename: `PortField`. Pos Y: `-60`. Width `360`, Height `40`.
3. Default text: `7777`. Placeholder: `Port`.

### 3.8 Add the Join button

1. Right-click `MainPanel` → **UI → Button - TextMeshPro**.
2. Rename: `JoinButton`. Pos Y: `-130`.
3. Child text: `JOIN GAME`.

### 3.9 Add the Back button

1. Right-click `MainPanel` → **UI → Button - TextMeshPro**.
2. Rename: `BackButton`. Pos Y: `-220`.
3. Child text: `BACK`.

### 3.10 Add the status label

1. Right-click `MainPanel` → **UI → Text - TextMeshPro**.
2. Rename: `StatusText`. Pos Y: `-310`. Width: `800`. Height: `40`.
3. Clear the text. Set color to yellow. Set Alignment to Center.

### 3.11 Add the LobbyMenu script

1. In Hierarchy, right-click empty space (not on Canvas) → **Create Empty**. Name it `LobbyController`.
2. Click it. **Add Component** → type `Lobby Menu` → click it.
3. Now drag references into each slot:
   - **Local Play Button**: drag `LocalPlayButton` from Hierarchy.
   - **Host Button**: drag `HostButton`.
   - **Join Button**: drag `JoinButton`.
   - **Back Button**: drag `BackButton`.
   - **Ip Field**: drag `IPField`.
   - **Port Field**: drag `PortField`.
   - **Status Text**: drag `StatusText`.
   - **Gameplay Scene Name**: type `GameplayScene` (must match the actual scene file name — careful with capital S; check `Assets/Scenes/Gameplayscene.unity` and use exactly that name with the same capitalization).
   - **Main Menu Scene Name**: type `MainMenu`.

### 3.12 Add a NetworkManager to the lobby too

The lobby needs the same network plumbing as the gameplay scene because StartHost/StartClient happen here.

Repeat **Part 1.1 through 1.5** in this lobby scene:
- Create `NetworkManager` GameObject with NetworkManager + UnityTransport components.
- Create `ArenaNetworkManager` GameObject.
- (No need for MultiplayerSceneBinder in the lobby.)

### 3.13 Save

`Ctrl+S`.

**Checkpoint:** Lobby scene has Canvas with 5 buttons, 2 inputs, 1 status label, all wired into LobbyController. Plus NetworkManager + ArenaNetworkManager.

---

## Part 4 — Connect MainMenu → Lobby (3 min)

1. Open `Assets/Scenes/MainMenu.unity`.
2. In Hierarchy find the **MainMenuManager** GameObject (it's the one with the script that has Play/Controls/Quit fields).
3. In Inspector, scroll to **Gameplay Scene Name** field on the MainMenuManager component.
4. Change it from `GameplayScene` to: `LobbyMenu`.
5. `Ctrl+S`.

Now Play in the main menu opens the lobby instead of jumping straight into the arena.

---

## Part 5 — Add scenes to Build Profiles (3 min)

Unity needs to know all three scenes ship with the game.

1. Top menu → **File → Build Profiles**.
2. In the panel that opens, scroll the bottom section: **Scene List**.
3. Click **Add Open Scene** while you have each scene open, OR drag each .unity file from Project into the list:
   - `Assets/Scenes/MainMenu.unity`  (index 0)
   - `Assets/Scenes/LobbyMenu.unity` (index 1)
   - `Assets/Scenes/Gameplayscene.unity` (index 2)
4. Order: MainMenu must be at index 0 (top of list). The other two can be in any order.
5. Close the Build Profiles window.

---

## Part 6 — Test multiplayer locally (5 min)

You can test with **two Unity windows** on the same machine — no need for two computers.

### Option A: Editor + standalone build

1. **File → Build And Run**.
2. Pick a folder for the build. Click **Build**.
3. Once the .exe launches, **also press Play in the Unity Editor**.
4. You now have two windows: the editor (Player 1) and the build (Player 2).

### Option B: Two builds

1. Build twice into the same folder, run two .exe instances.

### The actual test

In window #1:
1. Click **PLAY** on main menu → arrives at Lobby.
2. Click **HOST GAME**. Lobby loads gameplay scene. You'll see Player 1 + Player 2 on screen but only Player 1 responds to your keys.

In window #2:
1. Click **PLAY** → Lobby.
2. Make sure IP says `127.0.0.1`, Port `7777`.
3. Click **JOIN GAME**. Window loads gameplay scene.

Each window now controls its own character. Hit each other, watch HP go down on both screens.

---

## Common problems and fixes

**"Type or namespace name 'NetworkObject' could not be found"**
The Netcode package didn't restore. Open Window → Package Manager, check Netcode for GameObjects is installed.

**"NullReferenceException on NetworkManager.Singleton"**
You forgot to add the `NetworkManager` GameObject to the scene. Each scene that does networking (Lobby + Gameplay) needs one.

**"Players don't move on remote screen"**
- NetworkTransform missing on the player. Add it.
- Or NetworkObject's "Synchronize Transform" is unchecked. Recheck it.

**"Animations don't sync"**
NetworkAnimator missing or its Animator field empty. Re-add and drag the Animator in.

**"Player 2 controlled by both clients"**
You forgot Part 2.6 (assigning Player1 + Player2 to MultiplayerSceneBinder). Check that script's slots in the Inspector.

**"Build error: 'GameplayScene' not found"**
Capital-S typo. The actual file is `Gameplayscene.unity` (lowercase 's'). The scene NAME in build settings must match. Open the scene and check the title bar.

**Both windows on the same PC can't see each other**
Windows Defender / firewall might block port 7777. Allow Unity / your build's .exe through the firewall when the popup appears.

---

## What's next once multiplayer works

For the FYP submission you'll want at least one of:
- A second arena layout (duplicate Gameplayscene, rearrange platforms, add a scene-select to the lobby).
- A character-select toggle (use the Knight_Profile / Adventurer_Profile assets — assign them to Player 2's PlayerController in the inspector).
- A short demo video showing host + client in the same match.

Don't push for more features than you can polish. A small clean working demo > a big buggy one.
