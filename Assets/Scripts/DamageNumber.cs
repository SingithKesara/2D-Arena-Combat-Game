using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Spawned by DamageNumberSpawner when a hit lands.
/// Floats upward, fades out, then destroys itself.
/// Requires a TextMeshPro component on this prefab.
/// </summary>
[RequireComponent(typeof(TextMeshPro))]
public class DamageNumber : MonoBehaviour
{
    [Header("Float Settings")]
    public float floatSpeed  = 2.5f;
    public float lifetime    = 0.8f;
    public float spreadX     = 0.5f;   // random horizontal offset

    [Header("Colors")]
    public Color lightHitColor = Color.white;
    public Color heavyHitColor = new Color(1f, 0.4f, 0f);   // orange
    public Color critColor     = Color.red;

    private TextMeshPro _tmp;
    private float       _elapsed;

    private void Awake() => _tmp = GetComponent<TextMeshPro>();

    public void Init(int damage, bool isHeavy)
    {
        _tmp.text     = damage.ToString();
        _tmp.color    = isHeavy ? heavyHitColor : lightHitColor;
        _tmp.fontSize = isHeavy ? 6f : 4f;

        // Slight random horizontal drift
        float rx = Random.Range(-spreadX, spreadX);
        transform.position += new Vector3(rx, 0f, 0f);

        StartCoroutine(AnimateNumber());
    }

    private IEnumerator AnimateNumber()
    {
        Color startColor = _tmp.color;

        while (_elapsed < lifetime)
        {
            _elapsed    += Time.deltaTime;
            float t      = _elapsed / lifetime;

            // Float upward
            transform.position += Vector3.up * floatSpeed * Time.deltaTime;

            // Fade out in the second half
            float alpha  = t < 0.5f ? 1f : 1f - ((t - 0.5f) * 2f);
            _tmp.color   = new Color(startColor.r, startColor.g, startColor.b, alpha);

            // Scale punch: grow briefly, then shrink
            float scale  = t < 0.15f ? Mathf.Lerp(1.2f, 1.5f, t / 0.15f) : Mathf.Lerp(1.5f, 1f, (t - 0.15f) / 0.85f);
            transform.localScale = Vector3.one * scale;

            yield return null;
        }

        Destroy(gameObject);
    }
}
