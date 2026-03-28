using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles all player movement: horizontal run, jump (double-jump like Brawlhalla),
/// fast-fall, facing direction, and routes attack inputs to CombatSystem.
///
/// Player 1 controls: A/D to move, W/Space to jump, J = light attack, K = heavy attack
/// Player 2 controls: Keypad 4/6 to move, Keypad8/T to jump, Keypad0 = light, KpEnter = heavy
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    // ─────────────── Inspector fields ────────────────────────
    [Header("Movement")]
    public float walkSpeed      = 8f;
    public float runSpeed       = 13f;
    public float jumpForce      = 16f;
    public float fastFallForce  = 22f;
    public float maxFallSpeed   = -28f;

    [Header("Grounding")]
    public Transform groundCheck;
    public float     groundCheckRadius = 0.18f;
    public LayerMask groundLayer;

    [Header("Player Identity")]
    public int playerIndex = 1;   // 1 or 2 — set in Inspector

    // ─────────────── Runtime state ────────────────────────────
    [HideInInspector] public bool  isGrounded;
    [HideInInspector] public bool  isAttacking;
    [HideInInspector] public bool  isDead;

    private Vector2        _moveInput;
    private int            _jumpsLeft;
    private const int      MAX_JUMPS = 2;
    private bool           _isFacingRight = true;

    private Rigidbody2D    _rb;
    private Animator       _anim;
    private CombatSystem   _combat;

    // Animator hash cache
    private static readonly int H_IsMoving   = Animator.StringToHash("isMoving");
    private static readonly int H_IsRunning  = Animator.StringToHash("isRunning");
    private static readonly int H_IsGrounded = Animator.StringToHash("isGrounded");
    private static readonly int H_VelY       = Animator.StringToHash("velocityY");
    private static readonly int H_Jump       = Animator.StringToHash("jump");
    private static readonly int H_Hit        = Animator.StringToHash("hit");
    private static readonly int H_Death      = Animator.StringToHash("death");

    // ─────────────── Unity lifecycle ──────────────────────────
    private void Awake()
    {
        _rb     = GetComponent<Rigidbody2D>();
        _anim   = GetComponent<Animator>();
        _combat = GetComponent<CombatSystem>();

        // Reasonable defaults — prevent rotation
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        _isFacingRight = (playerIndex == 1);
        ApplyFacing();
    }

    private void Update()
    {
        if (isDead) return;
        GatherInput();
    }

    private void FixedUpdate()
    {
        if (isDead) return;
        CheckGround();
        ApplyMovement();
        ClampFall();
        SyncAnimator();
    }

    // ─────────────── Input reading ────────────────────────────
    // We use direct Input.GetKey so that two players on one keyboard don't conflict.
    private void GatherInput()
    {
        float x = 0f, y = 0f;

        if (playerIndex == 1)
        {
            if (Input.GetKey(KeyCode.A)) x -= 1f;
            if (Input.GetKey(KeyCode.D)) x += 1f;
            if (Input.GetKey(KeyCode.W)) y += 1f;
            if (Input.GetKey(KeyCode.S)) y -= 1f;

            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space))
                TryJump();
            if (Input.GetKeyDown(KeyCode.J))
                TryLightAttack();
            if (Input.GetKeyDown(KeyCode.K))
                TryHeavyAttack();
        }
        else // Player 2 — numpad cluster
        {
            if (Input.GetKey(KeyCode.Keypad4) || Input.GetKey(KeyCode.LeftArrow))  x -= 1f;
            if (Input.GetKey(KeyCode.Keypad6) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.Keypad8) || Input.GetKey(KeyCode.UpArrow))    y += 1f;
            if (Input.GetKey(KeyCode.Keypad5) || Input.GetKey(KeyCode.DownArrow))  y -= 1f;

            if (Input.GetKeyDown(KeyCode.Keypad8) || Input.GetKeyDown(KeyCode.UpArrow))
                TryJump();
            if (Input.GetKeyDown(KeyCode.Keypad0) || Input.GetKeyDown(KeyCode.RightControl))
                TryLightAttack();
            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.RightShift))
                TryHeavyAttack();
        }

        _moveInput = new Vector2(x, y);

        // Face direction of movement
        if (_moveInput.x > 0.1f)       SetFacing(true);
        else if (_moveInput.x < -0.1f) SetFacing(false);

        // Fast-fall
        if (_moveInput.y < -0.5f && !isGrounded && _rb.linearVelocity.y < 0)
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x,
                                  Mathf.Min(_rb.linearVelocity.y, -fastFallForce));
    }

    // ─────────────── Ground detection ─────────────────────────
    private void CheckGround()
    {
        bool prev = isGrounded;
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        if (isGrounded && !prev)
            _jumpsLeft = MAX_JUMPS;
    }

    // ─────────────── Movement ─────────────────────────────────
    private void ApplyMovement()
    {
        // Lock horizontal movement during an attack animation
        if (isAttacking) return;
        float speed = (Mathf.Abs(_moveInput.x) > 0.5f) ? runSpeed : walkSpeed;
        _rb.linearVelocity = new Vector2(_moveInput.x * speed, _rb.linearVelocity.y);
    }

    private void ClampFall()
    {
        if (_rb.linearVelocity.y < maxFallSpeed)
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, maxFallSpeed);
    }

    // ─────────────── Facing ───────────────────────────────────
    private void SetFacing(bool faceRight)
    {
        if (_isFacingRight == faceRight) return;
        _isFacingRight = faceRight;
        ApplyFacing();
    }

    private void ApplyFacing()
    {
        Vector3 s = transform.localScale;
        s.x = _isFacingRight ? Mathf.Abs(s.x) : -Mathf.Abs(s.x);
        transform.localScale = s;
    }

    public void FaceTarget(Transform target)
    {
        SetFacing(target.position.x > transform.position.x);
    }

    // ─────────────── Jump ─────────────────────────────────────
    private void TryJump()
    {
        if (_jumpsLeft <= 0) return;
        _jumpsLeft--;
        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);
        _anim.SetTrigger(H_Jump);
    }

    // ─────────────── Attacks ──────────────────────────────────
    private void TryLightAttack()
    {
        if (isAttacking || isDead || _combat == null) return;
        _combat.PerformLightAttack(_moveInput);
    }

    private void TryHeavyAttack()
    {
        if (isAttacking || isDead || _combat == null) return;
        _combat.PerformHeavyAttack(_moveInput);
    }

    // ─────────────── Animator sync ────────────────────────────
    private void SyncAnimator()
    {
        _anim.SetBool(H_IsMoving,   Mathf.Abs(_moveInput.x) > 0.1f);
        _anim.SetBool(H_IsRunning,  Mathf.Abs(_moveInput.x) > 0.5f);
        _anim.SetBool(H_IsGrounded, isGrounded);
        _anim.SetFloat(H_VelY,      _rb.linearVelocity.y);
    }

    // ─────────────── Public API ───────────────────────────────
    public void OnHitReceived()
    {
        _anim.SetTrigger(H_Hit);
    }

    public void OnDeath()
    {
        isDead = true;
        _rb.linearVelocity = Vector2.zero;
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _anim.SetTrigger(H_Death);
    }

    public void ResetForNewRound(Vector3 spawnPos)
    {
        isDead      = false;
        isAttacking = false;
        _jumpsLeft  = MAX_JUMPS;
        _moveInput  = Vector2.zero;

        _rb.bodyType       = RigidbodyType2D.Dynamic;
        _rb.linearVelocity = Vector2.zero;
        transform.position = spawnPos;

        _anim.Rebind();
        _anim.Update(0f);
    }

    // ─────────────── Gizmos ───────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
