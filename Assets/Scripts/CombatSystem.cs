using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(Animator))]
public class CombatSystem : MonoBehaviour
{
    [Header("Profile (optional — overrides inline values when assigned)")]
    public CharacterProfile profile;

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

    [Header("Networking (set by NetworkPlayer when networked)")]
    [HideInInspector] public bool networkSimulationAuthority = true;
    [HideInInspector] public NetworkPlayer netPlayer;

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

        ApplyProfile();
    }

    private void ApplyProfile()
    {
        CharacterProfile p = profile != null ? profile : (_pc != null ? _pc.profile : null);
        if (p == null) return;

        lightDamage = p.lightDamage;
        heavyDamage = p.heavyDamage;
        lightAttackRadius = p.lightAttackRadius;
        heavyAttackRadius = p.heavyAttackRadius;
        lightKnockback = p.lightKnockback;
        heavyKnockback = p.heavyKnockback;
        upwardBias = p.upwardBias;
        lightStartup = p.lightStartup;
        lightCooldown = p.lightCooldown;
        heavyStartup = p.heavyStartup;
        heavyCooldown = p.heavyCooldown;
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

        // The MatchState is only authoritatively known on the server. On the client,
        // skip this check and trust the server-side damage RPC to be rejected if the
        // round isn't actually live.
        GameStateManager gsm = GameStateManager.Instance;
        if (gsm != null && gsm.isNetworkAuthority &&
            gsm.State != GameStateManager.MatchState.Fighting)
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

        GameStateManager gsm = GameStateManager.Instance;
        if (gsm != null && gsm.isNetworkAuthority &&
            gsm.State != GameStateManager.MatchState.Fighting)
            return false;

        return true;
    }

    private void PerformHitCheck(int damage, float radius, float knockbackForce, bool isHeavy)
    {
        if (attackPoint == null) return;
        // We let any side run the hit check (so the attacking client gets local feedback);
        // damage application itself is gated to the server inside the loop below.

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

            // Apply damage authoritatively. On the server we can call directly;
            // on a client we send a ServerRpc so the host applies the damage.
            bool isServer = Unity.Netcode.NetworkManager.Singleton != null &&
                            Unity.Netcode.NetworkManager.Singleton.IsServer;

            if (isServer)
            {
                hm.TakeDamage(damage, knockback);
            }
            else if (netPlayer != null)
            {
                Unity.Netcode.NetworkObject victimNO = hit.GetComponent<Unity.Netcode.NetworkObject>();
                if (victimNO != null)
                    netPlayer.RequestDamageServerRpc(victimNO.NetworkObjectId, damage, knockback);
            }

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