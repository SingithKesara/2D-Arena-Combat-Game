using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Local 2-player controller for arena combat.
/// P1: A/D move, W or Space jump, S fast fall, J light, K heavy
/// P2: Numpad 4/6 move, Numpad 8 jump, Numpad 5 fast fall, Numpad 0 light, Numpad Enter heavy
/// </summary>
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
    public LayerMask groundLayer;
    public float groundCheckDistance = 0.06f;
    public float groundSnapDistance = 0.02f;
    [Range(0.7f, 1f)] public float groundCheckWidthMultiplier = 0.9f;

    [Header("Identity")]
    public int playerIndex = 1;

    [HideInInspector] public bool isGrounded;
    [HideInInspector] public bool isAttacking;
    [HideInInspector] public bool isDead;

    private Vector2 _move;
    private int _jumpsLeft;
    private const int MAX_JUMPS = 2;
    private bool _facingRight;

    private Rigidbody2D _rb;
    private Animator _anim;
    private CombatSystem _combat;
    private Collider2D _col;

    private static readonly int H_Moving = Animator.StringToHash("isMoving");
    private static readonly int H_Running = Animator.StringToHash("isRunning");
    private static readonly int H_Grounded = Animator.StringToHash("isGrounded");
    private static readonly int H_VelY = Animator.StringToHash("velocityY");
    private static readonly int H_Jump = Animator.StringToHash("jump");
    private static readonly int H_Hit = Animator.StringToHash("hit");
    private static readonly int H_Death = Animator.StringToHash("death");

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _anim = GetComponent<Animator>();
        _combat = GetComponent<CombatSystem>();
        _col = GetComponent<Collider2D>();

        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        _jumpsLeft = MAX_JUMPS;
        _facingRight = playerIndex == 1;
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
        SnapToGround();
        SyncAnimator();
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
            if (kb.wKey.isPressed || kb.spaceKey.isPressed) y = 1f;
            if (kb.sKey.isPressed) y = -1f;

            if (kb.wKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) TryJump();
            if (kb.jKey.wasPressedThisFrame) TryLightAttack();
            if (kb.kKey.wasPressedThisFrame) TryHeavyAttack();
        }
        else
        {
            if (kb.numpad4Key.isPressed) x -= 1f;
            if (kb.numpad6Key.isPressed) x += 1f;
            if (kb.numpad8Key.isPressed) y = 1f;
            if (kb.numpad5Key.isPressed) y = -1f;

            if (kb.numpad8Key.wasPressedThisFrame) TryJump();
            if (kb.numpad0Key.wasPressedThisFrame) TryLightAttack();
            if (kb.numpadEnterKey.wasPressedThisFrame) TryHeavyAttack();
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

        Bounds bounds = _col.bounds;

        Vector2 boxCenter = new Vector2(
            bounds.center.x,
            bounds.min.y + (groundCheckDistance * 0.5f)
        );

        Vector2 boxSize = new Vector2(
            bounds.size.x * groundCheckWidthMultiplier,
            groundCheckDistance
        );

        isGrounded = Physics2D.OverlapBox(boxCenter, boxSize, 0f, groundLayer);

        if (isGrounded && !wasGrounded)
        {
            _jumpsLeft = MAX_JUMPS;
            AudioManager.Instance?.PlayLand();
        }

        if (!isGrounded && _rb.linearVelocity.y <= 0f)
        {
            RaycastHit2D hit = Physics2D.Raycast(
                bounds.center,
                Vector2.down,
                bounds.extents.y + groundSnapDistance,
                groundLayer
            );

            if (hit.collider != null)
            {
                float bottomToGround = bounds.min.y - hit.point.y;

                if (bottomToGround >= 0f &&
                    bottomToGround <= groundSnapDistance &&
                    _rb.linearVelocity.y <= 0f)
                {
                    isGrounded = true;
                    _jumpsLeft = MAX_JUMPS;
                }
            }
        }
    }

    private void ApplyMovement()
    {
        if (isAttacking) return;

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

    private void SnapToGround()
    {
        if (!isGrounded) return;
        if (_rb.linearVelocity.y < -0.01f || _rb.linearVelocity.y > 0.01f) return;

        Bounds bounds = _col.bounds;

        RaycastHit2D hit = Physics2D.Raycast(
            bounds.center,
            Vector2.down,
            bounds.extents.y + groundSnapDistance,
            groundLayer
        );

        if (hit.collider == null) return;

        float currentBottom = bounds.min.y;
        float targetBottom = hit.point.y;
        float gap = currentBottom - targetBottom;

        if (gap > 0.001f && gap <= groundSnapDistance)
        {
            transform.position -= new Vector3(0f, gap, 0f);
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0f);
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
        SetFacing(target.position.x > transform.position.x);
    }

    private void TryJump()
    {
        if (isAttacking || isDead) return;
        if (_jumpsLeft <= 0) return;

        _jumpsLeft--;
        isGrounded = false;

        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);

        if (_anim != null) _anim.SetTrigger(H_Jump);
        AudioManager.Instance?.PlayJump();
    }

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

    private void SyncAnimator()
    {
        if (_anim == null) return;

        _anim.SetBool(H_Moving, Mathf.Abs(_move.x) > 0.1f);
        _anim.SetBool(H_Running, Mathf.Abs(_move.x) > 0.5f);
        _anim.SetBool(H_Grounded, isGrounded);
        _anim.SetFloat(H_VelY, _rb.linearVelocity.y);
    }

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
        isDead = false;
        isAttacking = false;
        isGrounded = false;
        _jumpsLeft = MAX_JUMPS;
        _move = Vector2.zero;

        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.linearVelocity = Vector2.zero;
        transform.position = spawnPos;

        if (_anim != null)
        {
            _anim.Rebind();
            _anim.Update(0f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return;

        Bounds bounds = col.bounds;

        Vector2 boxCenter = new Vector2(
            bounds.center.x,
            bounds.min.y + (groundCheckDistance * 0.5f)
        );

        Vector2 boxSize = new Vector2(
            bounds.size.x * groundCheckWidthMultiplier,
            groundCheckDistance
        );

        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireCube(boxCenter, boxSize);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(
            bounds.center,
            bounds.center + Vector3.down * (bounds.extents.y + groundSnapDistance)
        );
    }
    
    public void ResetState()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        rb.linearVelocity = Vector2.zero;
    }
}