using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class AdventurerSetup
{
    private const string AnimFolder = "Assets/Animations";
    private const string DataFolder = "Assets/Data";
    private const string IndividualFolder =
        "Assets/Art/rvros-adventurer/Individual Sprites";

    private const string ControllerName = "AC_Adventurer";
    private const string ProfileName = "Adventurer_Profile.asset";
    private const string KnightProfileName = "Knight_Profile.asset";
    private const string MatchSettingsName = "DefaultMatchSettings.asset";

    [MenuItem("Tools/Arena Combat/5 - Build Adventurer Animator + Profile")]
    public static void BuildAdventurer()
    {
        EnsureFolder(AnimFolder);
        EnsureFolder(DataFolder);

        ImportAdventurerSprites();

        BuildAdventurerController();
        BuildAdventurerProfile();
        BuildKnightProfile();
        BuildDefaultMatchSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Step 5 done: adventurer animator, character profiles, and match settings created.");
    }

    private static void ImportAdventurerSprites()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { IndividualFolder });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;

            bool changed = false;

            if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; changed = true; }
            if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; changed = true; }
            if (ti.filterMode != FilterMode.Point) { ti.filterMode = FilterMode.Point; changed = true; }
            if (ti.spritePixelsPerUnit != 32f) { ti.spritePixelsPerUnit = 32f; changed = true; }

            if (changed) ti.SaveAndReimport();
        }
    }

    private static void BuildAdventurerController()
    {
        string controllerPath = $"{AnimFolder}/{ControllerName}.controller";
        AssetDatabase.DeleteAsset(controllerPath);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.AddParameter("isMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("isRunning", AnimatorControllerParameterType.Bool);
        controller.AddParameter("isGrounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("velocityY", AnimatorControllerParameterType.Float);
        controller.AddParameter("jump", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("lightAttack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("heavyAttack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("hit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("death", AnimatorControllerParameterType.Trigger);

        var sm = controller.layers[0].stateMachine;

        AnimationClip idle = BuildClipFromIndividualSprites($"{AnimFolder}/{ControllerName}_Idle.anim", "adventurer-idle-", 4, 8f, true);
        AnimationClip run = BuildClipFromIndividualSprites($"{AnimFolder}/{ControllerName}_Run.anim", "adventurer-run-", 6, 12f, true);
        AnimationClip jump = BuildClipFromIndividualSprites($"{AnimFolder}/{ControllerName}_Jump.anim", "adventurer-jump-", 4, 12f, false);
        AnimationClip fall = BuildClipFromIndividualSprites($"{AnimFolder}/{ControllerName}_Fall.anim", "adventurer-fall-", 2, 8f, true);
        AnimationClip light = BuildClipFromIndividualSprites($"{AnimFolder}/{ControllerName}_Light.anim", "adventurer-attack1-", 5, 18f, false);
        AnimationClip heavy = BuildClipFromIndividualSprites($"{AnimFolder}/{ControllerName}_Heavy.anim", "adventurer-attack2-", 6, 14f, false);
        AnimationClip hit = BuildClipFromIndividualSprites($"{AnimFolder}/{ControllerName}_Hit.anim", "adventurer-hurt-", 3, 12f, false);
        AnimationClip death = BuildClipFromIndividualSprites($"{AnimFolder}/{ControllerName}_Death.anim", "adventurer-die-", 7, 10f, false);

        AnimatorState idleState = sm.AddState("Idle");   idleState.motion = idle;
        AnimatorState runState = sm.AddState("Run");     runState.motion = run;
        AnimatorState jumpState = sm.AddState("Jump");   jumpState.motion = jump;
        AnimatorState fallState = sm.AddState("Fall");   fallState.motion = fall;
        AnimatorState lightState = sm.AddState("Light"); lightState.motion = light;
        AnimatorState heavyState = sm.AddState("Heavy"); heavyState.motion = heavy;
        AnimatorState hitState = sm.AddState("Hit");     hitState.motion = hit;
        AnimatorState deathState = sm.AddState("Death"); deathState.motion = death;

        sm.defaultState = idleState;

        AddTransition(idleState, runState, "isMoving", true);
        AddTransition(runState, idleState, "isMoving", false);
        AddTriggerTransition(idleState, jumpState, "jump");
        AddTriggerTransition(runState, jumpState, "jump");
        AddFloatLessTransition(jumpState, fallState, "velocityY", 0f);
        AddBoolTransition(fallState, idleState, "isGrounded", true, "isMoving", false);
        AddBoolTransition(fallState, runState, "isGrounded", true, "isMoving", true);

        AddAnyStateTrigger(sm, lightState, "lightAttack");
        AddExitTransition(lightState, idleState);

        AddAnyStateTrigger(sm, heavyState, "heavyAttack");
        AddExitTransition(heavyState, idleState);

        AddAnyStateTrigger(sm, hitState, "hit");
        AddExitTransition(hitState, idleState);

        AddAnyStateTrigger(sm, deathState, "death");
    }

    private static AnimationClip BuildClipFromIndividualSprites(
        string clipPath,
        string prefix,
        int frameCount,
        float fps,
        bool loop)
    {
        AssetDatabase.DeleteAsset(clipPath);

        AnimationClip clip = new AnimationClip { frameRate = fps };

        EditorCurveBinding binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "Visual",
            propertyName = "m_Sprite"
        };

        List<Sprite> sprites = new List<Sprite>();
        for (int i = 0; i < frameCount; i++)
        {
            string spritePath = $"{IndividualFolder}/{prefix}{i:00}.png";
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (s != null) sprites.Add(s);
        }

        if (sprites.Count == 0)
        {
            Debug.LogWarning($"No sprites matched prefix {prefix} — clip {clipPath} will be empty.");
            AssetDatabase.CreateAsset(clip, clipPath);
            return clip;
        }

        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[sprites.Count];
        for (int i = 0; i < sprites.Count; i++)
        {
            keys[i] = new ObjectReferenceKeyframe
            {
                time = i / fps,
                value = sprites[i]
            };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        AssetDatabase.CreateAsset(clip, clipPath);
        return clip;
    }

    private static void BuildAdventurerProfile()
    {
        string profilePath = $"{DataFolder}/{ProfileName}";
        AssetDatabase.DeleteAsset(profilePath);

        CharacterProfile profile = ScriptableObject.CreateInstance<CharacterProfile>();
        profile.displayName = "Adventurer";
        profile.walkSpeed = 8f;
        profile.runSpeed = 13f;
        profile.jumpForce = 16f;
        profile.fastFallForce = 22f;
        profile.maxFallSpeed = -28f;
        profile.lightDamage = 7;
        profile.heavyDamage = 18;
        profile.lightAttackRadius = 0.7f;
        profile.heavyAttackRadius = 1.0f;
        profile.lightKnockback = 6.5f;
        profile.heavyKnockback = 15f;
        profile.upwardBias = 0.42f;
        profile.lightStartup = 0.07f;
        profile.lightCooldown = 0.20f;
        profile.heavyStartup = 0.18f;
        profile.heavyCooldown = 0.42f;
        profile.maxHealth = 105;
        profile.iFrameDuration = 0.25f;
        profile.colliderSize = new Vector2(0.5f, 0.95f);
        profile.colliderOffset = new Vector2(0f, -0.02f);
        profile.visualYOffset = -0.45f;
        profile.attackPointOffset = new Vector2(0.7f, 0.10f);
        profile.visualScale = new Vector3(2.4f, 2.4f, 1f);
        profile.animatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            $"{AnimFolder}/{ControllerName}.controller");

        AssetDatabase.CreateAsset(profile, profilePath);
    }

    private static void BuildKnightProfile()
    {
        string profilePath = $"{DataFolder}/{KnightProfileName}";
        if (AssetDatabase.LoadAssetAtPath<CharacterProfile>(profilePath) != null)
            return;

        CharacterProfile profile = ScriptableObject.CreateInstance<CharacterProfile>();
        profile.displayName = "Knight";
        profile.walkSpeed = 8f;
        profile.runSpeed = 13f;
        profile.jumpForce = 16f;
        profile.fastFallForce = 22f;
        profile.maxFallSpeed = -28f;
        profile.lightDamage = 8;
        profile.heavyDamage = 20;
        profile.lightAttackRadius = 0.65f;
        profile.heavyAttackRadius = 1.05f;
        profile.lightKnockback = 7f;
        profile.heavyKnockback = 16f;
        profile.upwardBias = 0.4f;
        profile.lightStartup = 0.08f;
        profile.lightCooldown = 0.22f;
        profile.heavyStartup = 0.16f;
        profile.heavyCooldown = 0.38f;
        profile.maxHealth = 100;
        profile.iFrameDuration = 0.25f;
        profile.colliderSize = new Vector2(0.5f, 0.95f);
        profile.colliderOffset = new Vector2(0f, -0.02f);
        profile.visualYOffset = -0.55f;
        profile.attackPointOffset = new Vector2(0.75f, 0.10f);
        profile.visualScale = new Vector3(2f, 2f, 1f);
        profile.animatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            $"{AnimFolder}/AC_Player1.controller");

        AssetDatabase.CreateAsset(profile, profilePath);
    }

    private static void BuildDefaultMatchSettings()
    {
        string assetPath = $"{DataFolder}/{MatchSettingsName}";
        if (AssetDatabase.LoadAssetAtPath<MatchSettings>(assetPath) != null)
            return;

        MatchSettings settings = ScriptableObject.CreateInstance<MatchSettings>();
        AssetDatabase.CreateAsset(settings, assetPath);
    }

    private static void AddTransition(AnimatorState from, AnimatorState to, string boolParam, bool value)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0.05f;
        t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, boolParam);
    }

    private static void AddTriggerTransition(AnimatorState from, AnimatorState to, string trigger)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0.05f;
        t.AddCondition(AnimatorConditionMode.If, 0, trigger);
    }

    private static void AddFloatLessTransition(AnimatorState from, AnimatorState to, string param, float threshold)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0.05f;
        t.AddCondition(AnimatorConditionMode.Less, threshold, param);
    }

    private static void AddBoolTransition(AnimatorState from, AnimatorState to, string p1, bool v1, string p2, bool v2)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0.05f;
        t.AddCondition(v1 ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, p1);
        t.AddCondition(v2 ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, p2);
    }

    private static void AddAnyStateTrigger(AnimatorStateMachine sm, AnimatorState to, string trigger)
    {
        var t = sm.AddAnyStateTransition(to);
        t.hasExitTime = false;
        t.duration = 0.03f;
        t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If, 0, trigger);
    }

    private static void AddExitTransition(AnimatorState from, AnimatorState to)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = true;
        t.exitTime = 0.95f;
        t.duration = 0.05f;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        string child = Path.GetFileName(path);

        if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(child))
            AssetDatabase.CreateFolder(parent, child);
    }
}
