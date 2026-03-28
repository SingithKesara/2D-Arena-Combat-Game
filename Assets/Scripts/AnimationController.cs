using UnityEngine;

/// <summary>
/// Bridges the physics / combat state to the Animator Controller.
/// Reads state from PlayerController and updates animator parameters each frame.
/// Attach this alongside PlayerController on each player prefab.
///
/// Required Animator parameters:
///   bool   isMoving
///   bool   isRunning
///   bool   isGrounded
///   float  velocityY
///   trigger jump
///   trigger lightAttack
///   trigger heavyAttack
///   trigger hit
///   trigger death
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerController))]
public class AnimationController : MonoBehaviour
{
    private Animator         _anim;
    private PlayerController _pc;
    private Rigidbody2D      _rb;

    // Cached hashes
    private static readonly int H_IsMoving   = Animator.StringToHash("isMoving");
    private static readonly int H_IsRunning  = Animator.StringToHash("isRunning");
    private static readonly int H_IsGrounded = Animator.StringToHash("isGrounded");
    private static readonly int H_VelY       = Animator.StringToHash("velocityY");

    private void Awake()
    {
        _anim = GetComponent<Animator>();
        _pc   = GetComponent<PlayerController>();
        _rb   = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (_pc.isDead) return;

        _anim.SetBool(H_IsGrounded, _pc.isGrounded);
        _anim.SetFloat(H_VelY, _rb.linearVelocity.y);
    }
}
