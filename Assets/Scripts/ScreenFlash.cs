using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates a brief white flash on screen when either player lands a heavy hit.
/// Attach to a full-screen transparent Image in the UI Canvas (render on top).
/// GameStateManager calls ScreenFlash.Instance.Flash() after a heavy hit registers.
/// </summary>
public class ScreenFlash : MonoBehaviour
{
    public static ScreenFlash Instance { get; private set; }

    [Header("Flash Settings")]
    public Image flashImage;
    [Range(0f, 1f)] public float maxAlpha   = 0.35f;
    public float                  flashTime  = 0.08f;
    public float                  fadeTime   = 0.15f;

    private Coroutine _co;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (flashImage != null)
            flashImage.color = new Color(1, 1, 1, 0);
    }

    /// <summary>Trigger a flash. isHeavy = larger flash.</summary>
    public void Flash(bool isHeavy = false)
    {
        if (flashImage == null) return;
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(DoFlash(isHeavy ? maxAlpha : maxAlpha * 0.5f));
    }

    private IEnumerator DoFlash(float targetAlpha)
    {
        // Instant white
        flashImage.color = new Color(1, 1, 1, targetAlpha);
        yield return new WaitForSeconds(flashTime);

        // Fade out
        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(targetAlpha, 0f, elapsed / fadeTime);
            flashImage.color = new Color(1, 1, 1, alpha);
            yield return null;
        }

        flashImage.color = new Color(1, 1, 1, 0);
    }
}
