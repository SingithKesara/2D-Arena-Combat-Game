using System.Collections;
using UnityEngine;

/// <summary>
/// Handles attack logic for one player.
/// Supports light attack (fast, low damage) and heavy attack (slow, high damage).
/// Directional variants driven by movement input at time of press (like Brawlhalla).
/// Integrates with AudioManager for SFX.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class CombatSystem : MonoBehaviour
{
    // ─────────────── Inspector ────────────────────────────────
    [Header("Attack Hitbox")]
    public Transform  attackPoint;
    public float      lightAttackRadius = 0.6f;
    public float      heavyAttackRadius = 0.9f;
    public LayerMask  playerLayer;

    [Header("Damage")]
    public int lightDamage = 8;
    public int heavyDamage = 20;

    [Header("Frame Timing (seconds)")]
    public float lightStartup  = 0.10f;
    public float lightActive   = 0.12f;
    public float lightCooldown = 0.30f;
    public float heavyStartup  = 0.22f;
    public float heavyActive   = 0.18f;
    public float heavyCooldown = 0.55f;

    [Header("Knockback")]
    public float lightKnockback = 5f;
    public float heavyKnockback = 12f;
    [Tooltip("Minimum upward component of knockback so hits feel satisfying")]
    public float upwardBias     = 0.4f;

    // ─────────────── Private refs ─────────────────────────────
    private PlayerController _pc;
    private Animator         _anim;

    private static readonly int H_LightAtk = Animator.StringToHash("lightAttack");
    private static readonly int H_HeavyAtk = Animator.StringToHash("heavyAttack");

    // ─────────────── Unity lifecycle ──────────────────────────
    private void Awake()
    {
        _pc   = GetComponent<PlayerController>();
        _anim = GetComponent<Animator>();
    }

    // ─────────────── Public entry points ──────────────────────
    public void PerformLightAttack(Vector2 direction)
    {
        StartCoroutine(AttackCoroutine(
            direction,
            lightStartup, lightActive, lightCooldown,
            lightDamage, lightAttackRadius, lightKnockback,
            H_LightAtk, isHeavy: false));
    }

    public void PerformHeavyAttack(Vector2 direction)
    {
        StartCoroutine(AttackCoroutine(
            direction,
            heavyStartup, heavyActive, heavyCooldown,
            heavyDamage, heavyAttackRadius, heavyKnockback,
            H_HeavyAtk, isHeavy: true));
    }

    // ─────────────── Core attack coroutine ────────────────────
    private IEnumerator AttackCoroutine(
        Vector2 dir,
        float startup, float active, float cooldown,
        int damage, float radius, float knockback,
        int animHash, bool isHeavy)
    {
        _pc.isAttacking = true;
        _anim.SetTrigger(animHash);

        // Swing SFX fires immediately (sounds like the windup)
        if (isHeavy) AudioManager.Instance?.PlayHeavySwing();
        else         AudioManager.Instance?.PlayLightSwing();

        // Startup: play animation, no hitbox yet
        yield return new WaitForSeconds(startup);

        // Active window: check for hits every frame
        float elapsed = 0f;
        bool  hitDone = false;

        while (elapsed < active)
        {
            if (!hitDone)
            {
                Collider2D[] hits = Physics2D.OverlapCircleAll(
                    attackPoint.position, radius, playerLayer);

                foreach (Collider2D col in hits)
                {
                    if (col.gameObject == gameObject) continue; // never hit self

                    HealthManager hm = col.GetComponentInParent<HealthManager>();
                    if (hm == null) continue;

                    Vector2 knockDir = ComputeKnockback(col.transform, dir, knockback);
                    hm.TakeDamage(damage, knockDir);
                    hitDone = true;

                    if (isHeavy) AudioManager.Instance?.PlayHeavyHit();
                    else         AudioManager.Instance?.PlayLightHit();

                    break; // one hit per swing
                }
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Recovery frames
        yield return new WaitForSeconds(cooldown);
        _pc.isAttacking = false;
    }

    // ─────────────── Knockback direction ──────────────────────
    private Vector2 ComputeKnockback(Transform target, Vector2 inputDir, float force)
    {
        // Base: push away from attacker
        Vector2 away = (target.position - transform.position).normalized;
        Vector2 dir  = away;

        // Blend with attack direction for directional knockback
        if (inputDir.sqrMagnitude > 0.1f)
            dir = (away + inputDir.normalized * 0.5f).normalized;

        // Always add some upward bias
        dir.y = Mathf.Max(dir.y, upwardBias);
        dir.Normalize();

        return dir * force;
    }

    // ─────────────── Gizmos ───────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, lightAttackRadius);
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.5f);
        Gizmos.DrawWireSphere(attackPoint.position, heavyAttackRadius);
    }
}
