using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class HealthManager : MonoBehaviour
{
    [Header("Stats")]
    public int maxHealth = 100;

    [Header("Invincibility After Hit")]
    public float iFrameDuration = 0.25f;

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
        CurrentHealth = maxHealth;
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
        if (_isInvincible || _pc == null || _pc.isDead) return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.AddForce(knockback, ForceMode2D.Impulse);
        }

        _pc.OnHitReceived();

        _isInvincible = true;
        _iFrameTimer = iFrameDuration;

        if (CurrentHealth <= 0)
            Die();
    }

    public void ForceDeath()
    {
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
}