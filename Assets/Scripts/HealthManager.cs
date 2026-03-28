using System;
using UnityEngine;

/// <summary>
/// Tracks a player's current health, applies incoming damage + knockback,
/// triggers animations, plays SFX, and notifies GameStateManager on death.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class HealthManager : MonoBehaviour
{
    // ─────────────── Inspector ────────────────────────────────
    [Header("Stats")]
    public int maxHealth = 100;

    [Header("Invincibility After Hit")]
    public float iFrameDuration = 0.25f;

    // ─────────────── Runtime ──────────────────────────────────
    public int CurrentHealth { get; private set; }

    // UIManager subscribes to these
    public event Action<int, int> OnHealthChanged;  // (current, max)
    public event Action           OnDied;

    private bool             _isInvincible;
    private float            _iFrameTimer;
    private Rigidbody2D      _rb;
    private PlayerController _pc;

    // ─────────────── Unity lifecycle ──────────────────────────
    private void Awake()
    {
        _rb           = GetComponent<Rigidbody2D>();
        _pc           = GetComponent<PlayerController>();
        CurrentHealth = maxHealth;
    }

    private void Update()
    {
        if (_isInvincible)
        {
            _iFrameTimer -= Time.deltaTime;
            if (_iFrameTimer <= 0f)
                _isInvincible = false;
        }
    }

    // ─────────────── Public API ───────────────────────────────
    /// <summary>Called by the attacker's CombatSystem.</summary>
    public void TakeDamage(int amount, Vector2 knockback)
    {
        if (_isInvincible || _pc.isDead) return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

        // Apply knockback impulse
        _rb.linearVelocity = Vector2.zero;
        _rb.AddForce(knockback, ForceMode2D.Impulse);

        // Trigger hit flash animation
        _pc.OnHitReceived();

        // Activate i-frames
        _isInvincible = true;
        _iFrameTimer  = iFrameDuration;

        if (CurrentHealth <= 0)
            Die();
    }

    private void Die()
    {
        AudioManager.Instance?.PlayDeath();
        _pc.OnDeath();
        OnDied?.Invoke();
        GameStateManager.Instance?.OnPlayerDied(this);
    }

    /// <summary>Called at the start of every new round.</summary>
    public void ResetHealth()
    {
        CurrentHealth = maxHealth;
        _isInvincible = false;
        _iFrameTimer  = 0f;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }
}
