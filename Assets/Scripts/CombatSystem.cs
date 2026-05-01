using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(Animator))]
public class CombatSystem : MonoBehaviour
{
    [Header("Attack Hitbox")]
    public Transform attackPoint;
    public LayerMask playerLayer;

    [Header("Damage")]
    public int lightDamage = 8;
    public int heavyDamage = 20;

    [Header("Range")]
    public float lightAttackRadius = 0.65f;
    public float heavyAttackRadius = 1.05f;

    [Header("Knockback")]
    public float lightKnockback = 7f;
    public float heavyKnockback = 16f;
    public float upwardBias = 0.4f;

    [Header("Timing")]
    public float lightStartup = 0.08f;
    public float lightCooldown = 0.22f;
    public float heavyStartup = 0.16f;
    public float heavyCooldown = 0.38f;

    private PlayerController _pc;
    private HealthManager _health;
    private Animator _anim;
    private Coroutine _activeAttack;

    private static readonly int H_LightAttack = Animator.StringToHash("lightAttack");
    private static readonly int H_HeavyAttack = Animator.StringToHash("heavyAttack");

    private void Awake()
    {
        _pc = GetComponent<PlayerController>();
        _health = GetComponent<HealthManager>();
        _anim = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        if (_health != null)
            _health.OnHealthChanged += OnSelfHealthChanged;
    }

    private void OnDisable()
    {
        if (_health != null)
            _health.OnHealthChanged -= OnSelfHealthChanged;
    }

    public void DoLightAttack()
    {
        if (!CanAttack()) return;

        _activeAttack = StartCoroutine(AttackRoutine(
            lightDamage,
            lightAttackRadius,
            lightKnockback,
            lightStartup,
            lightCooldown,
            H_LightAttack,
            isHeavy: false
        ));
    }

    public void DoHeavyAttack()
    {
        if (!CanAttack()) return;

        _activeAttack = StartCoroutine(AttackRoutine(
            heavyDamage,
            heavyAttackRadius,
            heavyKnockback,
            heavyStartup,
            heavyCooldown,
            H_HeavyAttack,
            isHeavy: true
        ));
    }

    private int _lastSelfHealth = -1;

    private void OnSelfHealthChanged(int current, int max)
    {
        if (_lastSelfHealth < 0)
        {
            _lastSelfHealth = current;
            return;
        }

        bool tookDamage = current < _lastSelfHealth;
        _lastSelfHealth = current;

        if (tookDamage && _activeAttack != null)
        {
            StopCoroutine(_activeAttack);
            _activeAttack = null;

            if (_pc != null)
                _pc.isAttacking = false;
        }
    }

    private bool CanAttack()
    {
        if (_pc == null) return false;
        if (_pc.isDead) return false;
        if (_pc.isAttacking) return false;
        if (_pc.controlsLocked) return false;

        if (GameStateManager.Instance != null &&
            GameStateManager.Instance.State != GameStateManager.MatchState.Fighting)
            return false;

        return true;
    }

    private IEnumerator AttackRoutine(
        int damage,
        float radius,
        float knockbackForce,
        float startup,
        float cooldown,
        int animHash,
        bool isHeavy)
    {
        _pc.isAttacking = true;

        if (_anim != null)
            _anim.SetTrigger(animHash);

        if (AudioManager.Instance != null)
        {
            if (isHeavy) AudioManager.Instance.PlayHeavySwing();
            else AudioManager.Instance.PlayLightSwing();
        }

        yield return new WaitForSeconds(startup);

        if (CanAttackDuringActiveFrame())
            PerformHitCheck(damage, radius, knockbackForce, isHeavy);

        yield return new WaitForSeconds(cooldown);

        if (_pc != null)
            _pc.isAttacking = false;

        _activeAttack = null;
    }

    private bool CanAttackDuringActiveFrame()
    {
        if (_pc == null) return false;
        if (_pc.isDead) return false;
        if (_pc.controlsLocked) return false;

        if (GameStateManager.Instance != null &&
            GameStateManager.Instance.State != GameStateManager.MatchState.Fighting)
            return false;

        return true;
    }

    private void PerformHitCheck(int damage, float radius, float knockbackForce, bool isHeavy)
    {
        if (attackPoint == null) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            attackPoint.position,
            radius,
            playerLayer
        );

        foreach (Collider2D hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            HealthManager hm = hit.GetComponent<HealthManager>();
            if (hm == null) continue;

            if (hm.IsInvincible) continue;

            Vector2 dir = (hit.transform.position - transform.position).normalized;

            if (Mathf.Abs(dir.x) < 0.1f)
                dir.x = transform.localScale.x >= 0f ? 1f : -1f;

            Vector2 knockback = new Vector2(
                dir.x * knockbackForce,
                knockbackForce * upwardBias
            );

            hm.TakeDamage(damage, knockback);

            if (AudioManager.Instance != null)
            {
                if (isHeavy) AudioManager.Instance.PlayHeavyHit();
                else AudioManager.Instance.PlayLightHit();
            }

            if (HitStop.Instance != null)
                HitStop.Instance.DoHitStop(isHeavy ? 0.12f : 0.08f);

            if (CameraShake.Instance != null)
                CameraShake.Instance.Shake(isHeavy ? 0.18f : 0.12f, isHeavy ? 0.36f : 0.25f);

            if (HitEffect.Instance != null)
                HitEffect.Instance.Spawn(hit.transform.position);

            if (ScreenFlash.Instance != null)
                ScreenFlash.Instance.Flash(isHeavy);

            break;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, lightAttackRadius);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(attackPoint.position, heavyAttackRadius);
    }
}