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
