using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class HealthManager : MonoBehaviour
{
    [Header("Profile (optional — overrides inline values when assigned)")]
    public CharacterProfile profile;

    [Header("Stats")]
    public int maxHealth = 100;

    [Header("Invincibility After Hit")]
    public float iFrameDuration = 0.25f;

    [Header("Block Mitigation")]
    [Tooltip("Damage multiplier applied when the victim is blocking (0.25 = 75% damage reduction).")]
    [Range(0f, 1f)] public float blockDamageMultiplier = 0.25f;
    [Tooltip("Knockback magnitude multiplier applied when the victim is blocking.")]
    [Range(0f, 1f)] public float blockKnockbackMultiplier = 0.5f;

    [Header("Networking (set by NetworkPlayer when networked)")]
    [HideInInspector] public bool networkSimulationAuthority = true;

    public int CurrentHealth { get; private set; }
    public bool IsInvincible => _isInvincible;

    public event Action<int, int> OnHealthChanged;
    public event Action OnDied;

    private bool _isInvincible;
    private float _iFrameTimer;
    private Rigidbody2D _rb;
    private PlayerController _pc;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _pc = GetComponent<PlayerController>();

        ApplyProfile();

        CurrentHealth = maxHealth;
    }

    private void ApplyProfile()
    {
        CharacterProfile p = profile != null ? profile : (_pc != null ? _pc.profile : null);
        if (p == null) return;

        maxHealth = p.maxHealth;
        iFrameDuration = p.iFrameDuration;
    }

    private void Update()
    {
        if (!_isInvincible) return;

        _iFrameTimer -= Time.deltaTime;
        if (_iFrameTimer <= 0f)
            _isInvincible = false;
    }

    public void TakeDamage(int amount, Vector2 knockback)
    {
        if (!networkSimulationAuthority) return;
        if (_isInvincible || _pc == null || _pc.isDead) return;

        // Blocking: reduce damage and knockback. The victim's isBlocking flag is synced
        // across the network by NetworkPlayer's _netBlocking NetworkVariable so the
        // server side sees the correct value at damage-application time.
        bool wasBlocked = _pc.isBlocking;
        int appliedDamage = wasBlocked
            ? Mathf.Max(0, Mathf.RoundToInt(amount * blockDamageMultiplier))
            : amount;
        Vector2 appliedKnockback = wasBlocked
            ? knockback * blockKnockbackMultiplier
            : knockback;

        CurrentHealth = Mathf.Max(0, CurrentHealth - appliedDamage);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.AddForce(appliedKnockback, ForceMode2D.Impulse);
        }

        _pc.OnHitReceived();

        _isInvincible = true;
        _iFrameTimer = iFrameDuration;

        if (CurrentHealth <= 0)
            Die();
    }

    public void ForceDeath()
    {
        // Damage / death are server-authoritative in networked play. On a non-authority side
        // (i.e. a client), the server's NetworkVariable will deliver the HP drop instead.
        if (!networkSimulationAuthority) return;
        if (_pc != null && _pc.isDead) return;

        CurrentHealth = 0;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

        Die();
    }

    private void Die()
    {
        if (_pc == null) return;
        if (_pc.isDead) return;

        _pc.OnDeath();
        OnDied?.Invoke();

        if (GameStateManager.Instance != null)
            GameStateManager.Instance.OnPlayerDied(_pc.playerIndex);
    }

    public void ResetHealth()
    {
        CurrentHealth = maxHealth;
        _isInvincible = false;
        _iFrameTimer = 0f;

        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    /// <summary>
    /// Used by NetworkPlayer on the client side to mirror the server's HP value.
    /// Updates CurrentHealth and fires OnHealthChanged so the UI redraws, without going
    /// through the full TakeDamage / Die pipeline (which is server-authoritative).
    /// </summary>
    public void ApplyNetworkedHealth(int current)
    {
        CurrentHealth = Mathf.Clamp(current, 0, maxHealth);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

        // Mirror the death visual on the client too when HP hits zero (the actual game-end
        // decision is still made on the server).
        if (CurrentHealth <= 0 && _pc != null && !_pc.isDead)
            _pc.OnDeath();
    }
}