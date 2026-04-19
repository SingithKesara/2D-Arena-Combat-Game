using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Collider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 8f;
    public float runSpeed = 13f;
    public float jumpForce = 16f;
    public float fastFallForce = 22f;
    public float maxFallSpeed = -28f;

    [Header("Ground Detection")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.12f;
    public LayerMask groundLayer;

    [Header("Identity")]
    public int playerIndex = 1;

    [HideInInspector] public bool isGrounded;
    [HideInInspector] public bool isAttacking;
    [HideInInspector] public bool isDead;

    public float MoveX => _move.x;

    private Vector2 _move;
    private int _jumpsLeft;
    private const int MAX_JUMPS = 2;
    private bool _facingRight;

    private Rigidbody2D _rb;
    private Animator _anim;
    private CombatSystem _combat;

    private static readonly int H_Jump = Animator.StringToHash("jump");
    private static readonly int H_Hit = Animator.StringToHash("hit");
    private static readonly int H_Death = Animator.StringToHash("death");

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _anim = GetComponent<Animator>();
        _combat = GetComponent<CombatSystem>();

        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        _jumpsLeft = MAX_JUMPS;
        _facingRight = playerIndex == 1;
        ApplyFacing();
    }

    private void Update()
    {
        if (isDead) return;

        ReadInput();
        CheckGround();
    }

    private void FixedUpdate()
    {
        if (isDead) return;

        ApplyMovement();
        ClampFall();

        if (Mathf.Abs(_rb.linearVelocity.x) < 0.1f)
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
    }

    private void ReadInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        float x = 0f;
        float y = 0f;

        if (playerIndex == 1)
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

        if (groundCheck == null)
        {
            isGrounded = false;
            return;
        }

        isGrounded = Physics2D.OverlapCircle(
            groundCheck.position,
            groundCheckRadius,
            groundLayer
        );

        if (isGrounded && !wasGrounded)
        {
            _jumpsLeft = MAX_JUMPS;
        }
    }

    private void ApplyMovement()
    {
        if (isAttacking)
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
        {
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, maxFallSpeed);
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

    public void FaceTarget(Transform target)
    {
        if (target == null) return;
        SetFacing(target.position.x > transform.position.x);
    }

    private void TryJump()
    {
        if (isAttacking || isDead) return;
        if (_jumpsLeft <= 0) return;

        _jumpsLeft--;
        isGrounded = false;

        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);
        _anim.SetTrigger(H_Jump);
    }

    private void TryLightAttack()
    {
        if (isAttacking || isDead || _combat == null) return;
        _combat.DoLightAttack();
    }

    private void TryHeavyAttack()
    {
        if (isAttacking || isDead || _combat == null) return;
        _combat.DoHeavyAttack();
    }

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
        isDead = false;
        isAttacking = false;
        isGrounded = false;
        _jumpsLeft = MAX_JUMPS;
        _move = Vector2.zero;

        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.linearVelocity = Vector2.zero;
        transform.position = spawnPos;

        _anim.Rebind();
        _anim.Update(0f);
    }

    public void ResetState()
    {
        isDead = false;
        isAttacking = false;
        isGrounded = false;
        _jumpsLeft = MAX_JUMPS;
        _move = Vector2.zero;

        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.linearVelocity = Vector2.zero;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;

        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}