using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Collider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Profile (optional — overrides inline values when assigned)")]
    public CharacterProfile profile;

    [Header("Movement")]
    public float walkSpeed = 8f;
    public float runSpeed = 13f;
    public float jumpForce = 16f;
    public float fastFallForce = 22f;
    public float maxFallSpeed = -28f;

    [Header("Ground Detection")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;
    public float groundBoxDistance = 0.14f;
    public float groundLockAfterJump = 0.10f;

    [Header("Identity")]
    public int playerIndex = 1;

    [Header("Visual Alignment")]
    public Transform visualRoot;
    public float visualYOffset = -0.55f;

    [Header("Blast Zone")]
    public float deathY = -8.5f;
    public bool dieWhenFallingOut = true;

    [Header("Runtime State")]
    public bool controlsLocked;

    [Header("Networking (set by NetworkPlayer when networked)")]
    [HideInInspector] public bool networkInputAuthority = true;
    [HideInInspector] public bool networkSimulationAuthority = true;
    [HideInInspector] public bool networkUseP1KeyScheme = false;

    [HideInInspector] public bool isGrounded;
    [HideInInspector] public bool isAttacking;
    [HideInInspector] public bool isDead;
    [HideInInspector] public bool isBlocking;

    public float MoveX => _move.x;

    private const int MAX_JUMPS = 2;

    private Vector2 _move;
    private int _jumpsLeft;
    private bool _facingRight;
    private bool _fellOutThisLife;
    private float _groundLockTimer;

    private Rigidbody2D _rb;
    private Animator _anim;
    private CombatSystem _combat;
    private HealthManager _health;
    private Collider2D _col;
    private SpriteRenderer _visualRenderer;
    private bool _lastVisualBlocking;

    private static readonly Color BlockTint = new Color(0.55f, 0.78f, 1f, 1f);

    private readonly RaycastHit2D[] _groundHits = new RaycastHit2D[8];

    private static readonly int H_IsMoving = Animator.StringToHash("isMoving");
    private static readonly int H_IsRunning = Animator.StringToHash("isRunning");
    private static readonly int H_IsGrounded = Animator.StringToHash("isGrounded");
    private static readonly int H_VelocityY = Animator.StringToHash("velocityY");
    private static readonly int H_Jump = Animator.StringToHash("jump");
    private static readonly int H_Hit = Animator.StringToHash("hit");
    private static readonly int H_Death = Animator.StringToHash("death");

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _anim = GetComponent<Animator>();
        _combat = GetComponent<CombatSystem>();
        _health = GetComponent<HealthManager>();
        _col = GetComponent<Collider2D>();

        ApplyProfile();

        if (visualRoot == null)
        {
            Transform found = transform.Find("Visual");
            if (found != null) visualRoot = found;
        }

        if (visualRoot != null)
            _visualRenderer = visualRoot.GetComponent<SpriteRenderer>();

        ApplyVisualOffset();

        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        _jumpsLeft = MAX_JUMPS;
        _facingRight = playerIndex == 1;
        ApplyFacing();
    }

    private void LateUpdate()
    {
        // Keep the block tint in sync whenever isBlocking changes (owner via input or
        // non-owner via NetworkVariable).
        if (isBlocking != _lastVisualBlocking)
        {
            _lastVisualBlocking = isBlocking;
            ApplyBlockingVisual();
        }
    }

    private void ApplyBlockingVisual()
    {
        if (_visualRenderer == null) return;
        _visualRenderer.color = isBlocking ? BlockTint : Color.white;
    }

    private void Update()
    {
        if (isDead) return;

        if (networkInputAuthority)
            ReadInput();

        // Fall-out detection is server-authoritative in networked mode (and always-on locally).
        // The server's transform reflects the synced position from NetworkTransform Owner mode,
        // so it sees Player 2 fall too.
        bool isLocalSession = ArenaNetworkManager.Instance == null ||
                              ArenaNetworkManager.Instance.CurrentMode == ArenaNetworkManager.SessionMode.Local;
        bool isServer = Unity.Netcode.NetworkManager.Singleton != null &&
                        Unity.Netcode.NetworkManager.Singleton.IsServer;

        if (isLocalSession || isServer)
            CheckFallDeath();
    }

    private void FixedUpdate()
    {
        if (isDead) return;

        if (_groundLockTimer > 0f)
            _groundLockTimer -= Time.fixedDeltaTime;

        CheckGround();

        // Only the owner / authority simulates physics-driven movement.
        // Non-owners receive position from NetworkTransform and animation from NetworkAnimator,
        // so writing local velocity / animator parameters here would clobber the synced state.
        if (networkSimulationAuthority)
        {
            ApplyMovement();
            ClampFall();
        }

        if (networkInputAuthority)
            UpdateAnimator();
    }

    private void ReadInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (controlsLocked)
        {
            _move = Vector2.zero;
            return;
        }

        float x = 0f;
        float y = 0f;

        // In networked mode the local owner always uses WASD (their own keyboard).
        // In local 2P play, keep the split-keyboard scheme: P1 = WASD, P2 = Numpad.
        bool useP1Scheme = networkUseP1KeyScheme || playerIndex == 1;

        // Block (hold key). Only allowed when grounded and not mid-attack.
        bool blockHeld;
        if (useP1Scheme)
            blockHeld = kb.lKey.isPressed;
        else
            blockHeld = kb.numpadPeriodKey.isPressed;

        isBlocking = blockHeld && isGrounded && !isAttacking && !isDead;

        if (useP1Scheme)
        {
            if (kb.aKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed) x += 1f;
            if (kb.sKey.isPressed) y = -1f;

            if (kb.wKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                TryJump();

            if (kb.jKey.wasPressedThisFrame)
                TryLightAttack();

            if (kb.kKey.wasPressedThisFrame)
                TryHeavyAttack();
        }
        else
        {
            if (kb.numpad4Key.isPressed) x -= 1f;
            if (kb.numpad6Key.isPressed) x += 1f;
            if (kb.numpad5Key.isPressed) y = -1f;

            if (kb.numpad8Key.wasPressedThisFrame)
                TryJump();

            if (kb.numpad0Key.wasPressedThisFrame)
                TryLightAttack();

            if (kb.numpadEnterKey.wasPressedThisFrame)
                TryHeavyAttack();
        }

        // While blocking, freeze horizontal input so the character stands their ground.
        if (isBlocking) x = 0f;

        _move = new Vector2(x, y);

        if (x > 0.1f) SetFacing(true);
        else if (x < -0.1f) SetFacing(false);

        if (y < -0.5f && !isGrounded && _rb.linearVelocity.y < 0f)
        {
            _rb.linearVelocity = new Vector2(
                _rb.linearVelocity.x,
                Mathf.Min(_rb.linearVelocity.y, -fastFallForce)
            );
        }
    }

    private void CheckGround()
    {
        bool wasGrounded = isGrounded;

        if (_groundLockTimer > 0f)
        {
            isGrounded = false;
            return;
        }

        bool circleGrounded = false;

        if (groundCheck != null)
        {
            circleGrounded = Physics2D.OverlapCircle(
                groundCheck.position,
                groundCheckRadius,
                groundLayer
            );
        }

        bool castGrounded = false;

        if (_col != null)
        {
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(groundLayer);
            filter.useTriggers = false;

            int count = _col.Cast(Vector2.down, filter, _groundHits, groundBoxDistance);

            for (int i = 0; i < count; i++)
            {
                if (_groundHits[i].collider == null) continue;

                if (_groundHits[i].normal.y > 0.35f)
                {
                    castGrounded = true;
                    break;
                }
            }
        }

        bool overlapGrounded = false;

        if (_col != null)
        {
            Bounds b = _col.bounds;

            Vector2 footCenter = new Vector2(b.center.x, b.min.y + 0.04f);
            Vector2 footSize = new Vector2(b.size.x * 0.75f, 0.10f);

            Collider2D overlap = Physics2D.OverlapBox(
                footCenter,
                footSize,
                0f,
                groundLayer
            );

            overlapGrounded = overlap != null;
        }

        bool movingDownOrStill = _rb.linearVelocity.y <= 0.5f;

        isGrounded = movingDownOrStill && (circleGrounded || castGrounded || overlapGrounded);

        if (isGrounded)
        {
            _jumpsLeft = MAX_JUMPS;

            if (!wasGrounded)
            {
                float impactVy = _rb.linearVelocity.y;

                if (impactVy < 0f)
                    _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0f);

                if (impactVy < -6f && AudioManager.Instance != null)
                    AudioManager.Instance.PlayLand();

                // A double-jump in mid-air queues the "jump" trigger because the Fall state
                // has no transition to Jump. On landing, that queued trigger would otherwise
                // bounce the character back into the Jump animation and leave the legs in
                // a stuck running pose. Clear it.
                if (_anim != null)
                    _anim.ResetTrigger(H_Jump);
            }
        }
    }

    private void ApplyMovement()
    {
        if (controlsLocked || isAttacking || isBlocking)
        {
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            return;
        }

        float speed = Mathf.Abs(_move.x) > 0.5f ? runSpeed : walkSpeed;
        _rb.linearVelocity = new Vector2(_move.x * speed, _rb.linearVelocity.y);
    }

    private void ClampFall()
    {
        if (_rb.linearVelocity.y < maxFallSpeed)
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, maxFallSpeed);
    }

    private void TryJump()
    {
        if (controlsLocked || isAttacking || isDead) return;
        if (isBlocking) return; // can't jump while shielded

        if (isGrounded)
            _jumpsLeft = MAX_JUMPS;

        if (_jumpsLeft <= 0) return;

        _jumpsLeft--;
        isGrounded = false;
        _groundLockTimer = groundLockAfterJump;

        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);
        _anim.SetTrigger(H_Jump);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayJump();
    }

    private void TryLightAttack()
    {
        if (controlsLocked || isAttacking || isDead || isBlocking || _combat == null) return;
        _combat.DoLightAttack();
    }

    private void TryHeavyAttack()
    {
        if (controlsLocked || isAttacking || isDead || isBlocking || _combat == null) return;
        _combat.DoHeavyAttack();
    }

    private void UpdateAnimator()
    {
        if (_anim == null) return;

        _anim.SetBool(H_IsGrounded, isGrounded);
        _anim.SetBool(H_IsMoving, Mathf.Abs(_move.x) > 0.1f);
        _anim.SetBool(H_IsRunning, Mathf.Abs(_move.x) > 0.5f);
        _anim.SetFloat(H_VelocityY, _rb.linearVelocity.y);
    }

    private void CheckFallDeath()
    {
        if (!dieWhenFallingOut) return;
        if (_fellOutThisLife) return;
        if (transform.position.y > deathY) return;

        // Only count fall-out kills while the round is actually live. During Intro / RoundEnd /
        // MatchOver the players might briefly be off-stage during respawn or freeze frames,
        // and the death event would be discarded by GameStateManager anyway (causing the round
        // to get stuck "won but never ended").
        if (GameStateManager.Instance != null &&
            GameStateManager.Instance.State != GameStateManager.MatchState.Fighting)
            return;

        _fellOutThisLife = true;

        if (_health != null)
            _health.ForceDeath();
        else
            OnDeath();
    }

    public void SetControlsLocked(bool locked)
    {
        controlsLocked = locked;

        if (locked)
        {
            _move = Vector2.zero;

            if (_rb != null)
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
        }
    }

    private void SetFacing(bool right)
    {
        if (_facingRight == right) return;

        _facingRight = right;
        ApplyFacing();
    }

    private void ApplyFacing()
    {
        Vector3 s = transform.localScale;
        s.x = _facingRight ? Mathf.Abs(s.x) : -Mathf.Abs(s.x);
        transform.localScale = s;
    }

    private void ApplyVisualOffset()
    {
        if (visualRoot == null) return;

        Vector3 local = visualRoot.localPosition;
        local.y = visualYOffset;
        visualRoot.localPosition = local;
    }

    private void ApplyProfile()
    {
        if (profile == null) return;

        walkSpeed = profile.walkSpeed;
        runSpeed = profile.runSpeed;
        jumpForce = profile.jumpForce;
        fastFallForce = profile.fastFallForce;
        maxFallSpeed = profile.maxFallSpeed;
        visualYOffset = profile.visualYOffset;

        if (profile.animatorController != null && _anim != null)
            _anim.runtimeAnimatorController = profile.animatorController;
    }

    public void FaceTarget(Transform target)
    {
        if (target == null) return;
        SetFacing(target.position.x > transform.position.x);
    }

    public void OnHitReceived()
    {
        if (_anim != null)
            _anim.SetTrigger(H_Hit);
    }

    public void OnDeath()
    {
        if (isDead) return;

        isDead = true;
        controlsLocked = true;
        isAttacking = false;
        isBlocking = false;

        _rb.linearVelocity = Vector2.zero;
        _rb.bodyType = RigidbodyType2D.Kinematic;

        if (_anim != null)
            _anim.SetTrigger(H_Death);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayDeath();
    }

    public void ResetForNewRound(Vector3 spawnPos)
    {
        isDead = false;
        isAttacking = false;
        isBlocking = false;
        controlsLocked = false;
        isGrounded = false;
        _fellOutThisLife = false;
        _jumpsLeft = MAX_JUMPS;
        _move = Vector2.zero;
        _groundLockTimer = 0f;

        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.linearVelocity = Vector2.zero;
        transform.position = spawnPos;

        ApplyVisualOffset();

        if (_anim != null)
        {
            _anim.Rebind();
            _anim.Update(0f);
        }
    }

    public void ResetState()
    {
        isDead = false;
        isAttacking = false;
        isBlocking = false;
        controlsLocked = false;
        isGrounded = false;
        _fellOutThisLife = false;
        _jumpsLeft = MAX_JUMPS;
        _move = Vector2.zero;
        _groundLockTimer = 0f;

        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.linearVelocity = Vector2.zero;

        ApplyVisualOffset();
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        if (_col != null)
        {
            Bounds b = _col.bounds;

            Vector2 footCenter = new Vector2(b.center.x, b.min.y + 0.04f);
            Vector2 footSize = new Vector2(b.size.x * 0.75f, 0.10f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(footCenter, footSize);
        }

        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(
            new Vector3(-20f, deathY, 0f),
            new Vector3(20f, deathY, 0f)
        );
    }
}