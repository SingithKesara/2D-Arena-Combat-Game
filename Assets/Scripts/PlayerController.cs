using UnityEngine;
using UnityEngine.InputSystem;   // ← New Input System; UnityEngine.Input is DISABLED in this project

/// <summary>
/// Brawlhalla-style player controller.
/// Uses Keyboard.current (New Input System) — no legacy Input.GetKey calls.
///
/// Player 1: A/D move | W or Space jump | J light attack | K heavy attack
/// Player 2: Numpad4/6 move | Numpad8 jump | Numpad0 light | NumpadEnter heavy
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────
    [Header("Movement")]
    public float walkSpeed     = 8f;
    public float runSpeed      = 13f;
    public float jumpForce     = 16f;
    public float fastFallForce = 22f;
    public float maxFallSpeed  = -28f;

    [Header("Ground Detection")]
    public Transform groundCheck;
    public float     groundCheckRadius = 0.18f;
    public LayerMask groundLayer;

    [Header("Identity")]
    public int playerIndex = 1;   // 1 or 2

    // ─── Runtime state (read by other scripts) ────────────────
    [HideInInspector] public bool isGrounded;
    [HideInInspector] public bool isAttacking;
    [HideInInspector] public bool isDead;

    // ─── Private ──────────────────────────────────────────────
    private Vector2       _move;
    private int           _jumpsLeft;
    private const int     MAX_JUMPS = 2;
    private bool          _facingRight;

    private Rigidbody2D   _rb;
    private Animator      _anim;
    private CombatSystem  _combat;

    // Animator hash cache
    private static readonly int H_Moving   = Animator.StringToHash("isMoving");
    private static readonly int H_Running  = Animator.StringToHash("isRunning");
    private static readonly int H_Grounded = Animator.StringToHash("isGrounded");
    private static readonly int H_VelY     = Animator.StringToHash("velocityY");
    private static readonly int H_Jump     = Animator.StringToHash("jump");
    private static readonly int H_Hit      = Animator.StringToHash("hit");
    private static readonly int H_Death    = Animator.StringToHash("death");

    // ─── Unity lifecycle ──────────────────────────────────────
    private void Awake()
    {
        _rb     = GetComponent<Rigidbody2D>();
        _anim   = GetComponent<Animator>();
        _combat = GetComponent<CombatSystem>();

        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        _facingRight = (playerIndex == 1);
        ApplyFacing();
    }

    private void Update()
    {
        if (isDead) return;
        ReadInput();
    }

    private void FixedUpdate()
    {
        if (isDead) return;
        CheckGround();
        ApplyMovement();
        ClampFall();
        SyncAnimator();
    }

    // ─── Input (New Input System — Keyboard.current) ──────────
    private void ReadInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;   // no keyboard connected

        float x = 0f, y = 0f;

        if (playerIndex == 1)
        {
            // ── Movement ──────────────────────────────
            if (kb.aKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed) x += 1f;
            if (kb.wKey.isPressed) y =  1f;
            if (kb.sKey.isPressed) y = -1f;

            // ── Jump (wasPressed = fires once on the frame the key goes down) ──
            if (kb.wKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                TryJump();

            // ── Attacks ───────────────────────────────
            if (kb.jKey.wasPressedThisFrame) TryLightAttack();
            if (kb.kKey.wasPressedThisFrame) TryHeavyAttack();
        }
        else // Player 2
        {
            // ── Movement ──────────────────────────────
            if (kb.numpad4Key.isPressed) x -= 1f;
            if (kb.numpad6Key.isPressed) x += 1f;
            if (kb.numpad8Key.isPressed) y =  1f;
            if (kb.numpad5Key.isPressed) y = -1f;

            // ── Jump ──────────────────────────────────
            if (kb.numpad8Key.wasPressedThisFrame) TryJump();

            // ── Attacks ───────────────────────────────
            if (kb.numpad0Key.wasPressedThisFrame)     TryLightAttack();
            if (kb.numpadEnterKey.wasPressedThisFrame) TryHeavyAttack();
        }

        _move = new Vector2(x, y);

        // Update facing direction
        if (x > 0.1f)       SetFacing(true);
        else if (x < -0.1f) SetFacing(false);

        // Fast-fall
        if (y < -0.5f && !isGrounded && _rb.linearVelocity.y < 0)
            _rb.linearVelocity = new Vector2(
                _rb.linearVelocity.x,
                Mathf.Min(_rb.linearVelocity.y, -fastFallForce));
    }

    // ─── Ground detection ─────────────────────────────────────
    private void CheckGround()
    {
        bool prev  = isGrounded;
        isGrounded = groundCheck != null &&
                     Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        if (isGrounded && !prev)
        {
            _jumpsLeft = MAX_JUMPS;
            AudioManager.Instance?.PlayLand();
        }
    }

    // ─── Movement ─────────────────────────────────────────────
    private void ApplyMovement()
    {
        if (isAttacking) return;
        float speed = Mathf.Abs(_move.x) > 0.5f ? runSpeed : walkSpeed;
        _rb.linearVelocity = new Vector2(_move.x * speed, _rb.linearVelocity.y);
    }

    private void ClampFall()
    {
        if (_rb.linearVelocity.y < maxFallSpeed)
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, maxFallSpeed);
    }

    // ─── Facing ───────────────────────────────────────────────
    private void SetFacing(bool right)
    {
        if (_facingRight == right) return;
        _facingRight = right;
        ApplyFacing();
    }

    private void ApplyFacing()
    {
        var s = transform.localScale;
        s.x = _facingRight ? Mathf.Abs(s.x) : -Mathf.Abs(s.x);
        transform.localScale = s;
    }

    public void FaceTarget(Transform target)
    {
        SetFacing(target.position.x > transform.position.x);
    }

    // ─── Jump ─────────────────────────────────────────────────
    private void TryJump()
    {
        if (_jumpsLeft <= 0) return;
        _jumpsLeft--;
        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);
        if (_anim != null) _anim.SetTrigger(H_Jump);
        AudioManager.Instance?.PlayJump();
    }

    // ─── Attacks ──────────────────────────────────────────────
    private void TryLightAttack()
    {
        if (isAttacking || isDead || _combat == null) return;
        _combat.PerformLightAttack(_move);
    }

    private void TryHeavyAttack()
    {
        if (isAttacking || isDead || _combat == null) return;
        _combat.PerformHeavyAttack(_move);
    }

    // ─── Animator sync ────────────────────────────────────────
    private void SyncAnimator()
    {
        if (_anim == null) return;
        _anim.SetBool(H_Moving,   Mathf.Abs(_move.x) > 0.1f);
        _anim.SetBool(H_Running,  Mathf.Abs(_move.x) > 0.5f);
        _anim.SetBool(H_Grounded, isGrounded);
        _anim.SetFloat(H_VelY,    _rb.linearVelocity.y);
    }

    // ─── Public API ───────────────────────────────────────────
    public void OnHitReceived()
    {
        if (_anim != null) _anim.SetTrigger(H_Hit);
    }

    public void OnDeath()
    {
        isDead = true;
        _rb.linearVelocity = Vector2.zero;
        _rb.bodyType = RigidbodyType2D.Kinematic;
        if (_anim != null) _anim.SetTrigger(H_Death);
    }

    public void ResetForNewRound(Vector3 spawnPos)
    {
        isDead      = false;
        isAttacking = false;
        _jumpsLeft  = MAX_JUMPS;
        _move       = Vector2.zero;

        _rb.bodyType       = RigidbodyType2D.Dynamic;
        _rb.linearVelocity = Vector2.zero;
        transform.position = spawnPos;

        if (_anim != null) { _anim.Rebind(); _anim.Update(0f); }
    }

    // ─── Gizmos ───────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
