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
    private Animator _anim;

    private static readonly int H_LightAttack = Animator.StringToHash("lightAttack");
    private static readonly int H_HeavyAttack = Animator.StringToHash("heavyAttack");

    private void Awake()
    {
        _pc = GetComponent<PlayerController>();
        _anim = GetComponent<Animator>();
    }

    public void DoLightAttack()
    {
        if (_pc == null || _pc.isDead || _pc.isAttacking) return;
        StartCoroutine(AttackRoutine(
            damage: lightDamage,
            radius: lightAttackRadius,
            knockbackForce: lightKnockback,
            startup: lightStartup,
            cooldown: lightCooldown,
            animHash: H_LightAttack
        ));
    }

    public void DoHeavyAttack()
    {
        if (_pc == null || _pc.isDead || _pc.isAttacking) return;
        StartCoroutine(AttackRoutine(
            damage: heavyDamage,
            radius: heavyAttackRadius,
            knockbackForce: heavyKnockback,
            startup: heavyStartup,
            cooldown: heavyCooldown,
            animHash: H_HeavyAttack
        ));
    }

    private IEnumerator AttackRoutine(
        int damage,
        float radius,
        float knockbackForce,
        float startup,
        float cooldown,
        int animHash)
    {
        _pc.isAttacking = true;
        _anim.SetTrigger(animHash);

        yield return new WaitForSeconds(startup);

        if (attackPoint != null)
        {
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

                Vector2 dir = (hit.transform.position - transform.position).normalized;
                if (Mathf.Abs(dir.x) < 0.1f)
                    dir.x = transform.localScale.x >= 0f ? 1f : -1f;

                Vector2 knockback = new Vector2(
                    dir.x * knockbackForce,
                    knockbackForce * upwardBias
                );

                hm.TakeDamage(damage, knockback);

                if (HitStop.Instance != null)
                    HitStop.Instance.DoHitStop(0.08f);

                if (CameraShake.Instance != null)
                    CameraShake.Instance.Shake(0.12f, 0.25f);

                if (HitEffect.Instance != null)
                    HitEffect.Instance.Spawn(hit.transform.position);

                hit.transform.position += (Vector3)(new Vector2(Mathf.Sign(dir.x), 0f) * 0.25f);
                break;
            }
        }

        yield return new WaitForSeconds(cooldown);
        _pc.isAttacking = false;
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