using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(Rigidbody2D))]
public class AnimationController : MonoBehaviour
{
    private Animator _anim;
    private PlayerController _pc;
    private Rigidbody2D _rb;

    private static readonly int H_IsMoving = Animator.StringToHash("isMoving");
    private static readonly int H_IsRunning = Animator.StringToHash("isRunning");
    private static readonly int H_IsGrounded = Animator.StringToHash("isGrounded");
    private static readonly int H_VelY = Animator.StringToHash("velocityY");

    private void Awake()
    {
        _anim = GetComponent<Animator>();
        _pc = GetComponent<PlayerController>();
        _rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (_pc == null || _anim == null) return;

        _anim.SetBool(H_IsGrounded, _pc.isGrounded);
        _anim.SetBool(H_IsMoving, Mathf.Abs(_pc.MoveX) > 0.1f);
        _anim.SetBool(H_IsRunning, Mathf.Abs(_pc.MoveX) > 0.5f);
        _anim.SetFloat(H_VelY, _rb.linearVelocity.y);
    }
}
