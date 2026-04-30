using UnityEngine;

/// <summary>
/// Attach to each player GameObject alongside HealthManager.
/// Listens to damage events and spawns DamageNumber prefabs at the hit location.
/// </summary>
[RequireComponent(typeof(HealthManager))]
public class DamageNumberSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [Tooltip("Drag the DamageNumber prefab here (TextMeshPro world-space)")]
    public DamageNumber damageNumberPrefab;

    [Header("Spawn Offset")]
    public Vector2 spawnOffset = new Vector2(0f, 1.2f);  // above the character head

    private HealthManager _hm;
    private int           _lastHealth;

    private void Awake()
    {
        _hm = GetComponent<HealthManager>();
        _hm.OnHealthChanged += OnHealthChanged;
    }

    private void Start()
    {
        _lastHealth = _hm.CurrentHealth;
    }

    private void OnDestroy()
    {
        if (_hm != null) _hm.OnHealthChanged -= OnHealthChanged;
    }

    // Called whenever health changes
    private void OnHealthChanged(int current, int max)
    {
        int delta = _lastHealth - current;
        _lastHealth = current;

        if (delta <= 0 || damageNumberPrefab == null) return;

        Vector3 spawnPos = (Vector3)((Vector2)transform.position + spawnOffset);
        DamageNumber dn = Instantiate(damageNumberPrefab, spawnPos, Quaternion.identity);
        dn.Init(delta, isHeavy: delta >= 15);
    }
}
