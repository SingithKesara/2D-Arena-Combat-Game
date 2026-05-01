using UnityEngine;

/// <summary>
/// Manages the arena:
///   - Death zone beneath the arena (Brawlhalla-style blast zone)
///   - Smooth camera that frames both players
///   - Optional: kills players who fall off the bottom
/// </summary>
public class ArenaManager : MonoBehaviour
{
    [Header("Death Zone")]
    public float deathZoneY = -8.5f;    // Y below which a player dies instantly

    [Header("Camera Tracking")]
    public Camera     arenaCamera;
    public float      camSmoothing  = 4f;
    public float      camMinY       = -2f;
    public float      camMaxY       = 6f;
    public float      camMinSize    = 5f;
    public float      camMaxSize    = 9f;
    public float      camPadding    = 4f;   // extra world units around players

    [Header("Players")]
    public Transform player1Transform;
    public Transform player2Transform;

    // ─────────────── Unity lifecycle ──────────────────────────
    private void LateUpdate()
    {
        CheckDeathZone();
        TrackCamera();
    }

    // ─────────────── Death zone ───────────────────────────────
    private void CheckDeathZone()
    {
        CheckPlayer(player1Transform);
        CheckPlayer(player2Transform);
    }

    private void CheckPlayer(Transform t)
    {
        if (t == null) return;
        if (t.position.y >= deathZoneY) return;

        PlayerController pc = t.GetComponent<PlayerController>();
        if (pc == null || pc.isDead) return;

        HealthManager hm = t.GetComponent<HealthManager>();
        if (hm == null) return;

        hm.ForceDeath();
    }

    // ─────────────── Camera tracking ─────────────────────────
    // Frames both players; zooms out when they're far apart.
    private void TrackCamera()
    {
        if (arenaCamera == null || player1Transform == null || player2Transform == null) return;

        Vector2 p1 = player1Transform.position;
        Vector2 p2 = player2Transform.position;

        // Mid-point
        Vector2 mid = (p1 + p2) * 0.5f;
        mid.y = Mathf.Clamp(mid.y, camMinY, camMaxY);

        // Target position
        Vector3 targetPos = new Vector3(mid.x, mid.y, arenaCamera.transform.position.z);
        arenaCamera.transform.position = Vector3.Lerp(
            arenaCamera.transform.position, targetPos, camSmoothing * Time.deltaTime);

        // Orthographic size based on player distance
        float dist = Vector2.Distance(p1, p2) * 0.5f + camPadding;
        float targetSize = Mathf.Clamp(dist, camMinSize, camMaxSize);
        arenaCamera.orthographicSize = Mathf.Lerp(
            arenaCamera.orthographicSize, targetSize, camSmoothing * Time.deltaTime);
    }

    // ─────────────── Gizmos ───────────────────────────────────
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(new Vector3(-50, deathZoneY, 0), new Vector3(50, deathZoneY, 0));
    }
}
