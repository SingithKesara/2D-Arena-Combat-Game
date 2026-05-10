using UnityEngine;

[CreateAssetMenu(fileName = "CharacterProfile", menuName = "Arena Combat/Character Profile", order = 0)]
public class CharacterProfile : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "Knight";
    public Sprite portrait;

    [Header("Movement")]
    public float walkSpeed = 8f;
    public float runSpeed = 13f;
    public float jumpForce = 16f;
    public float fastFallForce = 22f;
    public float maxFallSpeed = -28f;

    [Header("Combat - Damage")]
    public int lightDamage = 8;
    public int heavyDamage = 20;

    [Header("Combat - Range")]
    public float lightAttackRadius = 0.65f;
    public float heavyAttackRadius = 1.05f;

    [Header("Combat - Knockback")]
    public float lightKnockback = 7f;
    public float heavyKnockback = 16f;
    public float upwardBias = 0.4f;

    [Header("Combat - Timing")]
    public float lightStartup = 0.08f;
    public float lightCooldown = 0.22f;
    public float heavyStartup = 0.16f;
    public float heavyCooldown = 0.38f;

    [Header("Health")]
    public int maxHealth = 100;
    public float iFrameDuration = 0.25f;

    [Header("Visuals")]
    public RuntimeAnimatorController animatorController;
    public Vector2 colliderSize = new Vector2(0.5f, 0.95f);
    public Vector2 colliderOffset = new Vector2(0f, -0.02f);
    public float visualYOffset = -0.55f;
    public Vector2 attackPointOffset = new Vector2(0.75f, 0.10f);
    public Vector3 visualScale = new Vector3(2f, 2f, 1f);
}
