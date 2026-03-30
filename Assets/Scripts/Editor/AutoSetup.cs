/*  ============================================================
 *  AutoSetup.cs  —  Editor-only one-click scene builder
 *  Unity 6 (6000.x) — fixed for:
 *    • ISpriteEditorDataProvider (replaces obsolete spritesheet)
 *    • No reflection on sorting layers (pre-baked in TagManager.asset)
 *    • Safe layer setup without crashing
 *
 *  HOW TO USE  (run in order):
 *    Top menu → Tools → Arena Combat
 *      → ① Setup Layers and Tags
 *      → ② Slice All Sprite Sheets
 *      → ③ Build Animator Controllers
 *      → ④ Build Full Scene
 *      → ⑤ Fix Physics2D Layer Matrix
 *  ============================================================ */
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;          // ISpriteEditorDataProvider (com.unity.2d.sprite)
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class AutoSetup
{
    // ── Asset paths ───────────────────────────────────────────
    private const string K1   = "Assets/Art/freeknight/Colour1/Outline/120x80_PNGSheets/";
    private const string K2   = "Assets/Art/freeknight/Colour2/Outline/120x80_PNGSheets/";
    private const string ANIMS = "Assets/Animations/";

    // ─────────────────────────────────────────────────────────
    // ① Layers & Tags
    // ─────────────────────────────────────────────────────────
    [MenuItem("Tools/Arena Combat/① Setup Layers and Tags")]
    public static void SetupLayersAndTags()
    {
        // Load the TagManager serialized object
        var objs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (objs == null || objs.Length == 0)
        {
            Debug.LogError("Cannot load TagManager.asset");
            return;
        }
        var tm = new SerializedObject(objs[0]);

        // ── Tags ─────────────────────────────────────────
        AddTagIfMissing(tm, "Player");
        AddTagIfMissing(tm, "Ground");

        // ── Layers ───────────────────────────────────────
        SetLayerName(tm, 6, "Ground");
        SetLayerName(tm, 7, "Player");

        tm.ApplyModifiedPropertiesWithoutUndo();

        // Force save — no reflection, just write through SerializedObject
        EditorUtility.SetDirty(objs[0]);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=lime>✔ Tags: Player, Ground added. Layers: 6=Ground, 7=Player.</color>\n" +
                  "Sorting layers (Background, Midground, Foreground, Characters) were pre-baked in TagManager.asset.");
    }

    private static void AddTagIfMissing(SerializedObject tm, string tag)
    {
        // Check live Unity registry first — avoids "tag already registered" warning
        // for built-in tags like "Player" that Unity pre-registers
        foreach (var t in UnityEditorInternal.InternalEditorUtility.tags)
            if (t == tag) return;

        var tags = tm.FindProperty("tags");
        for (int i = 0; i < tags.arraySize; i++)
            if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;
        int idx = tags.arraySize;
        tags.arraySize = idx + 1;
        tags.GetArrayElementAtIndex(idx).stringValue = tag;
    }

    private static void SetLayerName(SerializedObject tm, int layerIndex, string name)
    {
        var layers = tm.FindProperty("layers");
        if (layers.arraySize <= layerIndex)
            layers.arraySize = layerIndex + 1;
        var element = layers.GetArrayElementAtIndex(layerIndex);
        if (element.stringValue != name)
            element.stringValue = name;
    }

    // ─────────────────────────────────────────────────────────
    // ② Slice sprite sheets  (Unity 6 ISpriteEditorDataProvider)
    // ─────────────────────────────────────────────────────────
    [MenuItem("Tools/Arena Combat/② Slice All Sprite Sheets")]
    public static void SliceAllSpriteSheets()
    {
        // sheet-name → frame count  (all cells are 120 × 80 px)
        var sheets = new Dictionary<string, int>
        {
            { "_Idle",             10 },
            { "_Run",              10 },
            { "_Death",            10 },
            { "_Attack",            4 },
            { "_Attack2",           6 },
            { "_Hit",               1 },
            { "_Jump",              3 },
            { "_Fall",              3 },
            { "_Dash",              2 },
            { "_AttackCombo2hit",  10 },
        };

        int total = 0;
        foreach (var kv in sheets)
        {
            if (SliceSheet(K1 + kv.Key + ".png", kv.Key, "k1", kv.Value)) total++;
            if (SliceSheet(K2 + kv.Key + ".png", kv.Key, "k2", kv.Value)) total++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"<color=lime>✔ Sliced {total} sprite sheets (120×80 px per frame, PPU=32).</color>");
    }

    /// <summary>
    /// Slices one sprite sheet using the Unity 6 ISpriteEditorDataProvider API.
    /// Returns true on success.
    /// </summary>
    private static bool SliceSheet(string assetPath, string sheetKey, string prefix, int frameCount)
    {
        var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (ti == null) { Debug.LogWarning($"Sheet not found: {assetPath}"); return false; }

        // ── Basic import settings ────────────────────────
        ti.textureType         = TextureImporterType.Sprite;
        ti.spriteImportMode    = SpriteImportMode.Multiple;
        ti.spritePixelsPerUnit = 32f;
        ti.filterMode          = FilterMode.Point;
        ti.mipmapEnabled       = false;
        ti.textureCompression  = TextureImporterCompression.Uncompressed;
        ti.alphaIsTransparency = true;
        EditorUtility.SetDirty(ti);
        ti.SaveAndReimport();   // must reimport once so the data provider target is valid

        // ── Use ISpriteEditorDataProvider ───────────────
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var dp = factory.GetSpriteEditorDataProviderFromObject(ti);
        if (dp == null) { Debug.LogWarning($"No data provider for {assetPath}"); return false; }

        dp.InitSpriteEditorDataProvider();

        int fw = 120, fh = 80;

        var rects = new SpriteRect[frameCount];
        for (int i = 0; i < frameCount; i++)
        {
            rects[i] = new SpriteRect
            {
                name      = $"{prefix}{sheetKey}_{i}",
                rect      = new Rect(i * fw, 0, fw, fh),
                pivot     = new Vector2(0.5f, 0.5f),
                alignment = SpriteAlignment.Center,
                spriteID  = GUID.Generate()
            };
        }

        dp.SetSpriteRects(rects);
        dp.Apply();

        // Reimport to bake the new sprite data
        (dp.targetObject as AssetImporter)?.SaveAndReimport();
        return true;
    }

    // ─────────────────────────────────────────────────────────
    // ③ Build Animator Controllers
    // ─────────────────────────────────────────────────────────
    [MenuItem("Tools/Arena Combat/③ Build Animator Controllers")]
    public static void BuildAnimatorControllers()
    {
        if (!AssetDatabase.IsValidFolder(ANIMS.TrimEnd('/')))
            AssetDatabase.CreateFolder("Assets", "Animations");

        BuildController("AC_Player1", K1, "k1");
        BuildController("AC_Player2", K2, "k2");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=lime>✔ Animator Controllers created in Assets/Animations/</color>");
    }

    private static void BuildController(string name, string folder, string prefix)
    {
        string path = ANIMS + name + ".controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);

        // Parameters
        ctrl.AddParameter("isMoving",    AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("isRunning",   AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("isGrounded",  AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("velocityY",   AnimatorControllerParameterType.Float);
        ctrl.AddParameter("jump",        AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("lightAttack", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("heavyAttack", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("hit",         AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("death",       AnimatorControllerParameterType.Trigger);

        var sm = ctrl.layers[0].stateMachine;

        // Build clips
        var idle   = MakeClip(folder, prefix + "_Idle",           10,  8f, path);
        var run    = MakeClip(folder, prefix + "_Run",            10, 12f, path);
        var jump   = MakeClip(folder, prefix + "_Jump",            3, 10f, path);
        var fall   = MakeClip(folder, prefix + "_Fall",            3, 10f, path);
        var light  = MakeClip(folder, prefix + "_Attack",          4, 16f, path);
        var heavy  = MakeClip(folder, prefix + "_Attack2",         6, 12f, path);
        var hit    = MakeClip(folder, prefix + "_Hit",             1, 12f, path, loop: false);
        var death  = MakeClip(folder, prefix + "_Death",          10,  8f, path, loop: false);

        // States
        var sIdle  = AddState(sm, "Idle",        idle,  isDefault: true);
        var sRun   = AddState(sm, "Run",         run);
        var sJump  = AddState(sm, "Jump",        jump);
        var sFall  = AddState(sm, "Fall",        fall);
        var sLight = AddState(sm, "LightAttack", light);
        var sHeavy = AddState(sm, "HeavyAttack", heavy);
        var sHit   = AddState(sm, "Hit",         hit);
        var sDeath = AddState(sm, "Death",       death);

        // Idle ↔ Run
        BoolTrans(sIdle, sRun,  "isMoving", true,  0.05f);
        BoolTrans(sRun,  sIdle, "isMoving", false, 0.05f);

        // Grounded → Jump trigger
        TriggerTrans(sIdle, sJump, "jump");
        TriggerTrans(sRun,  sJump, "jump");

        // Jump → Fall
        var jf = sJump.AddTransition(sFall);
        jf.hasExitTime = false; jf.duration = 0.05f;
        jf.AddCondition(AnimatorConditionMode.Less, 0f, "velocityY");

        // Fall → Idle
        var fi = sFall.AddTransition(sIdle);
        fi.hasExitTime = false; fi.duration = 0.05f;
        fi.AddCondition(AnimatorConditionMode.If, 0, "isGrounded");

        // AnyState attacks (interrupt any state)
        AnyTrigger(sm, sLight, "lightAttack");
        AnyTrigger(sm, sHeavy, "heavyAttack");
        ExitTime(sLight, sIdle, 0.90f, 0.05f);
        ExitTime(sHeavy, sIdle, 0.90f, 0.05f);

        // Hit
        AnyTrigger(sm, sHit, "hit");
        ExitTime(sHit, sIdle, 0.85f, 0.05f);

        // Death (no recovery)
        AnyTrigger(sm, sDeath, "death");

        EditorUtility.SetDirty(ctrl);
    }

    // ── Build a single AnimationClip from sliced sprites ──────
    private static AnimationClip MakeClip(string folder, string clipName,
        int frameCount, float fps, string ctrlPath, bool loop = true)
    {
        string clipPath = Path.GetDirectoryName(ctrlPath) + "/" + clipName + ".anim";
        AssetDatabase.DeleteAsset(clipPath);

        var clip = new AnimationClip { frameRate = fps, name = clipName };
        var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
        clipSettings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, clipSettings);

        // Derive the sheet filename from the clip name
        // e.g. "k1_Idle" → sheet key "_Idle"
        string sheetKey = clipName.Substring(clipName.IndexOf('_')); // "_Idle"
        string sheetPath = folder + sheetKey + ".png";

        // Load sprites
        var sprites = AssetDatabase.LoadAllAssetsAtPath(sheetPath)
            .OfType<Sprite>()
            .OrderBy(s =>
            {
                var parts = s.name.Split('_');
                return int.TryParse(parts[^1], out int n) ? n : 0;
            })
            .ToArray();

        if (sprites.Length == 0)
        {
            Debug.LogWarning($"No sprites found at {sheetPath} for clip '{clipName}'. Run ② first.");
            AssetDatabase.CreateAsset(clip, clipPath);
            return clip;
        }

        float dt = 1f / fps;
        var binding = new EditorCurveBinding
        {
            type         = typeof(SpriteRenderer),
            path         = "",
            propertyName = "m_Sprite"
        };

        // +1 key for seamless loop
        var keys = new ObjectReferenceKeyframe[sprites.Length + (loop ? 1 : 0)];
        for (int i = 0; i < sprites.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i * dt, value = sprites[i] };

        if (loop)
            keys[sprites.Length] = new ObjectReferenceKeyframe
                { time = sprites.Length * dt, value = sprites[0] };

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        AssetDatabase.CreateAsset(clip, clipPath);
        return clip;
    }

    // ── Animator helper methods ────────────────────────────────
    private static AnimatorState AddState(AnimatorStateMachine sm, string name,
        AnimationClip clip, bool isDefault = false)
    {
        var s = sm.AddState(name);
        s.motion = clip;
        if (isDefault) sm.defaultState = s;
        return s;
    }

    private static void BoolTrans(AnimatorState from, AnimatorState to,
        string param, bool val, float dur)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false; t.duration = dur;
        t.AddCondition(val ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, param);
    }

    private static void TriggerTrans(AnimatorState from, AnimatorState to, string trigger)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false; t.duration = 0.05f;
        t.AddCondition(AnimatorConditionMode.If, 0, trigger);
    }

    private static void AnyTrigger(AnimatorStateMachine sm, AnimatorState to, string trigger)
    {
        var t = sm.AddAnyStateTransition(to);
        t.hasExitTime = false; t.duration = 0.05f;
        t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If, 0, trigger);
    }

    private static void ExitTime(AnimatorState from, AnimatorState to,
        float exitTime, float dur)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = true; t.exitTime = exitTime; t.duration = dur;
    }

    // ─────────────────────────────────────────────────────────
    // ④ Build Full Scene
    // ─────────────────────────────────────────────────────────
    [MenuItem("Tools/Arena Combat/④ Build Full Scene")]
    public static void BuildFullScene()
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        int playerLayer = LayerMask.NameToLayer("Player");

        if (groundLayer < 0 || playerLayer < 0)
        {
            EditorUtility.DisplayDialog("Missing Layers",
                "Run ① first. Layers 'Ground' (6) and 'Player' (7) must exist.", "OK");
            return;
        }

        // Clean up previous auto-build objects
        foreach (var n in new[]
        {
            "Ground","LeftPlatform","RightPlatform","TopPlatform",
            "Player1","Player2","SpawnPoint1","SpawnPoint2",
            "GameManager","ArenaManagerGO","AudioManagerGO","HUD_Canvas"
        })
            DestroyIfExists(n);

        // ── Platforms ─────────────────────────────────────────
        MakePlatform("Ground",        new Vector3(0,  -3f, 0), new Vector2(20f, 0.5f), groundLayer, new Color32(90, 55, 20, 255));
        MakePlatform("LeftPlatform",  new Vector3(-5f, 0f, 0), new Vector2(4f,  0.3f), groundLayer, new Color32(55, 100, 35, 255));
        MakePlatform("RightPlatform", new Vector3( 5f, 0f, 0), new Vector2(4f,  0.3f), groundLayer, new Color32(55, 100, 35, 255));
        MakePlatform("TopPlatform",   new Vector3( 0f, 3f, 0), new Vector2(3f,  0.3f), groundLayer, new Color32(35, 75, 120, 255));

        // ── Spawn points ──────────────────────────────────────
        var sp1 = new GameObject("SpawnPoint1"); sp1.transform.position = new Vector3(-4f, -1.8f, 0);
        var sp2 = new GameObject("SpawnPoint2"); sp2.transform.position = new Vector3( 4f, -1.8f, 0);

        // ── Players ───────────────────────────────────────────
        var p1 = MakePlayer("Player1", 1, playerLayer, groundLayer, new Vector3(-4f, -1.8f, 0));
        var p2 = MakePlayer("Player2", 2, playerLayer, groundLayer, new Vector3( 4f, -1.8f, 0));

        // ── GameStateManager ──────────────────────────────────
        var gmGO = new GameObject("GameManager");
        var gsm  = gmGO.AddComponent<GameStateManager>();
        gsm.player1      = p1.GetComponent<PlayerController>();
        gsm.player2      = p2.GetComponent<PlayerController>();
        gsm.spawnP1      = sp1.transform;
        gsm.spawnP2      = sp2.transform;
        gsm.roundsToWin  = 2;
        gsm.matchTimeSec = 99f;

        // ── ArenaManager ──────────────────────────────────────
        var amGO = new GameObject("ArenaManagerGO");
        var am   = amGO.AddComponent<ArenaManager>();
        am.arenaCamera      = Camera.main;
        am.player1Transform = p1.transform;
        am.player2Transform = p2.transform;
        am.deathZoneY       = -9f;
        am.camMinSize       = 5f;
        am.camMaxSize       = 9f;
        am.camPadding       = 4f;

        // ── AudioManager ──────────────────────────────────────
        var auGO = new GameObject("AudioManagerGO");
        var au   = auGO.AddComponent<AudioManager>();
        WireAudio(au);

        // ── HUD Canvas ────────────────────────────────────────
        BuildHUD(gsm);

        // Mark scene dirty
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("✔ Scene Built!",
            "Everything is wired and ready.\n\n" +
            "Controls:\n" +
            "  P1 → A/D move | W/Space jump | J light | K heavy\n" +
            "  P2 → Numpad 4/6 move | Num8 jump | Num0 light | NumEnter heavy\n\n" +
            "Press Ctrl+S to save, then press Play!", "Let's go!");
    }

    // ── Platform helper ───────────────────────────────────────
    private static GameObject MakePlatform(string name, Vector3 pos, Vector2 size,
        int layer, Color32 col)
    {
        var go = new GameObject(name);
        go.layer = layer;
        go.tag   = "Ground";
        go.transform.position = pos;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite           = CreateSolidSprite(col);
        sr.drawMode         = SpriteDrawMode.Tiled;
        sr.size             = size;
        sr.sortingLayerName = "Foreground";
        sr.sortingOrder     = 0;

        var bc  = go.AddComponent<BoxCollider2D>();
        bc.size = size;
        return go;
    }

    private static Sprite CreateSolidSprite(Color32 c)
    {
        var t = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var p = new Color32[16];
        for (int i = 0; i < 16; i++) p[i] = c;
        t.SetPixels32(p); t.Apply();
        return Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
    }

    // ── Player helper ─────────────────────────────────────────
    private static GameObject MakePlayer(string name, int index, int playerLayer,
        int groundLayer, Vector3 pos)
    {
        var go = new GameObject(name);
        go.layer = playerLayer;
        go.tag   = "Player";
        go.transform.position = pos;
        // P2 starts facing left
        go.transform.localScale = index == 1
            ? new Vector3(2f, 2f, 1f)
            : new Vector3(-2f, 2f, 1f);

        // SpriteRenderer
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Characters";
        sr.sortingOrder     = index;

        // Rigidbody2D
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.constraints  = RigidbodyConstraints2D.FreezeRotation;

        // Capsule collider
        var cap  = go.AddComponent<CapsuleCollider2D>();
        cap.size = new Vector2(0.6f, 1.4f);
        cap.offset = new Vector2(0f, 0.05f);

        // Animator + controller
        var anim = go.AddComponent<Animator>();
        string ctrlName = index == 1 ? "AC_Player1" : "AC_Player2";
        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ANIMS + ctrlName + ".controller");
        if (ctrl != null)
            anim.runtimeAnimatorController = ctrl;
        else
            Debug.LogWarning($"Controller '{ctrlName}' not found — run ③ first.");

        // GroundCheck child
        var gc = new GameObject("GroundCheck");
        gc.transform.SetParent(go.transform, false);
        gc.transform.localPosition = new Vector3(0f, -0.75f, 0f);

        // AttackPoint child
        var ap = new GameObject("AttackPoint");
        ap.transform.SetParent(go.transform, false);
        ap.transform.localPosition = new Vector3(0.7f, 0.1f, 0f);

        // PlayerController
        var pc              = go.AddComponent<PlayerController>();
        pc.playerIndex      = index;
        pc.walkSpeed        = 8f;
        pc.runSpeed         = 13f;
        pc.jumpForce        = 16f;
        pc.fastFallForce    = 22f;
        pc.groundCheckRadius = 0.18f;
        pc.groundCheck      = gc.transform;
        pc.groundLayer      = 1 << groundLayer;

        // HealthManager, CombatSystem, helpers
        go.AddComponent<HealthManager>();

        var cs = go.AddComponent<CombatSystem>();
        cs.attackPoint       = ap.transform;
        cs.playerLayer       = 1 << playerLayer;
        cs.lightAttackRadius = 0.6f;
        cs.heavyAttackRadius = 0.9f;

        go.AddComponent<AnimationController>();
        go.AddComponent<DamageNumberSpawner>();

        return go;
    }

    // ── HUD Canvas ────────────────────────────────────────────
    private static void BuildHUD(GameStateManager gsm)
    {
        var canvasGO = new GameObject("HUD_Canvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Timer ─────────────────────────────────────────────
        var timerGO = MakeTMP(canvasGO, "TimerText", "99", 72, Color.white,
            TextAlignmentOptions.Center, new Vector2(0, -35), new Vector2(120, 80));
        AnchorTopCentre(timerGO);

        // ── P1 health bar (top-left, fills right) ─────────────
        var hbP1 = MakeHealthBar(canvasGO, "HealthBarP1",
            new Vector2(280, -35), new Vector2(500, 32), false);

        // ── P2 health bar (top-right, fills left) ─────────────
        var hbP2 = MakeHealthBar(canvasGO, "HealthBarP2",
            new Vector2(-280, -35), new Vector2(500, 32), true);

        // ── Player labels ─────────────────────────────────────
        var lP1 = MakeTMP(canvasGO, "LabelP1", "PLAYER 1", 20,
            new Color(0.4f, 0.8f, 1f), TextAlignmentOptions.Left,
            new Vector2(20, -8), new Vector2(220, 28));
        AnchorTopLeft(lP1);

        var lP2 = MakeTMP(canvasGO, "LabelP2", "PLAYER 2", 20,
            new Color(1f, 0.4f, 0.4f), TextAlignmentOptions.Right,
            new Vector2(-20, -8), new Vector2(220, 28));
        AnchorTopRight(lP2);

        // ── Score dots ────────────────────────────────────────
        var sP1 = MakeTMP(canvasGO, "ScoreP1", "", 26, Color.yellow,
            TextAlignmentOptions.Left, new Vector2(20, -72), new Vector2(150, 30));
        AnchorTopLeft(sP1);

        var sP2 = MakeTMP(canvasGO, "ScoreP2", "", 26, Color.yellow,
            TextAlignmentOptions.Right, new Vector2(-20, -72), new Vector2(150, 30));
        AnchorTopRight(sP2);

        // ── Announcement text (centre) ────────────────────────
        var annGO  = MakeTMP(canvasGO, "AnnouncementText", "", 72, Color.yellow,
            TextAlignmentOptions.Center, Vector2.zero, new Vector2(1000, 130));
        annGO.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        // ── Screen flash ─────────────────────────────────────
        var flashGO  = new GameObject("ScreenFlashImg");
        flashGO.transform.SetParent(canvasGO.transform, false);
        var flashImg = flashGO.AddComponent<Image>();
        flashImg.color = new Color(1, 1, 1, 0);
        Stretch(flashGO);
        var sf = canvasGO.AddComponent<ScreenFlash>();
        sf.flashImage = flashImg;

        // ── Match-over panel ──────────────────────────────────
        var panelGO = new GameObject("MatchOverPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        var panelImg = panelGO.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.82f);
        Stretch(panelGO);

        var resultGO = MakeTMP(panelGO, "ResultText", "PLAYER 1\nWINS!", 96,
            Color.yellow, TextAlignmentOptions.Center, new Vector2(0, 100), new Vector2(800, 220));
        resultGO.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        var hintGO = MakeTMP(panelGO, "ControlHint",
            "P1: A/D move | W/Space jump | J light | K heavy\n" +
            "P2: Numpad4/6 move | Num8 jump | Num0 light | NumEnter heavy",
            20, Color.white, TextAlignmentOptions.Center,
            new Vector2(0, -20), new Vector2(1000, 70));

        var rematch = MakeButton(panelGO, "RematchBtn", "▶  PLAY AGAIN",
            new Vector2(-160, -140), new Vector2(270, 65));
        var quit    = MakeButton(panelGO, "QuitBtn",    "✕  QUIT",
            new Vector2( 160, -140), new Vector2(200, 65));

        panelGO.SetActive(false);   // hidden until match ends

        // ── Controls hint at bottom ───────────────────────────
        var ctrlHint = MakeTMP(canvasGO, "BottomHint",
            "P1: A/D  W/Space=Jump  J=Light  K=Heavy        " +
            "P2: Num4/6  Num8=Jump  Num0=Light  NumEnter=Heavy",
            16, new Color(1, 1, 1, 0.45f), TextAlignmentOptions.Center,
            new Vector2(0, 18), new Vector2(1500, 28));
        AnchorBottomCentre(ctrlHint);

        // ── UIManager ────────────────────────────────────────
        var uimGO = new GameObject("UIManager");
        uimGO.transform.SetParent(canvasGO.transform, false);
        var uim = uimGO.AddComponent<UIManager>();

        uim.healthBarP1     = hbP1.GetComponent<Slider>();
        uim.healthBarP2     = hbP2.GetComponent<Slider>();
        uim.healthFillP1    = FindFill(hbP1);
        uim.healthFillP2    = FindFill(hbP2);
        uim.timerText       = timerGO.GetComponent<TextMeshProUGUI>();
        uim.announcementText= annGO.GetComponent<TextMeshProUGUI>();
        uim.p1ScoreText     = sP1.GetComponent<TextMeshProUGUI>();
        uim.p2ScoreText     = sP2.GetComponent<TextMeshProUGUI>();
        uim.matchOverPanel  = panelGO;
        uim.resultText      = resultGO.GetComponent<TextMeshProUGUI>();
        uim.rematchButton   = rematch.GetComponent<Button>();
        uim.quitButton      = quit.GetComponent<Button>();
    }

    // ── UI helper: TextMeshPro ────────────────────────────────
    private static GameObject MakeTMP(GameObject parent, string name, string text,
        float size, Color color, TextAlignmentOptions align,
        Vector2 apos, Vector2 sdelta)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.color     = color;
        tmp.alignment = align;
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = apos;
        rt.sizeDelta        = sdelta;
        return go;
    }

    // ── UI helper: Slider health bar ─────────────────────────
    private static GameObject MakeHealthBar(GameObject parent, string name,
        Vector2 apos, Vector2 sdelta, bool rightToLeft)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent.transform, false);

        var bg  = go.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.04f, 0.04f);

        var sl  = go.AddComponent<Slider>();
        sl.minValue    = 0; sl.maxValue = 1; sl.value = 1;
        sl.wholeNumbers = false;
        sl.direction   = rightToLeft ? Slider.Direction.RightToLeft : Slider.Direction.LeftToRight;
        sl.targetGraphic = bg;

        var rt  = go.GetComponent<RectTransform>();
        rt.anchoredPosition = apos;
        rt.sizeDelta        = sdelta;
        if (!rightToLeft) { rt.anchorMin = new Vector2(0,1); rt.anchorMax = new Vector2(0,1); rt.pivot = new Vector2(0,1); }
        else              { rt.anchorMin = new Vector2(1,1); rt.anchorMax = new Vector2(1,1); rt.pivot = new Vector2(1,1); }

        // Fill area
        var faGO = new GameObject("FillArea");
        faGO.transform.SetParent(go.transform, false);
        var faRT = faGO.AddComponent<RectTransform>();
        faRT.anchorMin = Vector2.zero; faRT.anchorMax = Vector2.one;
        faRT.offsetMin = new Vector2(2,2); faRT.offsetMax = new Vector2(-2,-2);

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(faGO.transform, false);
        var fi = fillGO.AddComponent<Image>();
        fi.color = new Color(0.1f, 0.9f, 0.1f);
        var fiRT = fillGO.GetComponent<RectTransform>();
        fiRT.anchorMin = Vector2.zero; fiRT.anchorMax = Vector2.one;
        fiRT.offsetMin = fiRT.offsetMax = Vector2.zero;

        sl.fillRect = fiRT;
        return go;
    }

    private static Image FindFill(GameObject sliderGO) =>
        sliderGO.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.name == "Fill");

    // ── UI helper: Button ────────────────────────────────────
    private static GameObject MakeButton(GameObject parent, string name,
        string label, Vector2 apos, Vector2 sdelta)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.12f, 0.12f, 0.22f, 0.95f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var rt  = go.GetComponent<RectTransform>();
        rt.anchoredPosition = apos; rt.sizeDelta = sdelta;

        MakeTMP(go, "Label", label, 26, Color.white,
            TextAlignmentOptions.Center, Vector2.zero, sdelta)
            .GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
        return go;
    }

    // ── RectTransform anchor shortcuts ───────────────────────
    private static void AnchorTopCentre(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
    }
    private static void AnchorTopLeft(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1);
        rt.pivot     = new Vector2(0, 1);
    }
    private static void AnchorTopRight(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 1); rt.anchorMax = new Vector2(1, 1);
        rt.pivot     = new Vector2(1, 1);
    }
    private static void AnchorBottomCentre(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0); rt.anchorMax = new Vector2(0.5f, 0);
        rt.pivot     = new Vector2(0.5f, 0);
    }
    private static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // ── Audio wiring ─────────────────────────────────────────
    private static void WireAudio(AudioManager am)
    {
        am.lightSwingSFX = Clip("Assets/Audio/SFX/12_Player_Movement_SFX/56_Attack_03.wav");
        am.heavySwingSFX = Clip("Assets/Audio/SFX/10_Battle_SFX/22_Slash_04.wav");
        am.lightHitSFX   = Clip("Assets/Audio/SFX/12_Player_Movement_SFX/61_Hit_03.wav");
        am.heavyHitSFX   = Clip("Assets/Audio/SFX/10_Battle_SFX/15_Impact_flesh_02.wav");
        am.deathSFX      = Clip("Assets/Audio/SFX/10_Battle_SFX/69_Enemy_death_01.wav");
        am.jumpSFX       = Clip("Assets/Audio/SFX/12_Player_Movement_SFX/30_Jump_03.wav");
        am.landSFX       = Clip("Assets/Audio/SFX/12_Player_Movement_SFX/45_Landing_01.wav");
        am.roundStartSFX = Clip("Assets/Audio/SFX/10_Battle_SFX/55_Encounter_02.wav");
        am.uiConfirmSFX  = Clip("Assets/Audio/SFX/10_UI_Menu_SFX/013_Confirm_03.wav");
        am.uiDeclineSFX  = Clip("Assets/Audio/SFX/10_UI_Menu_SFX/029_Decline_09.wav");
        am.battleMusicIntro = Clip("Assets/Audio/Music/10 Battle1 (8bit style)/Assets_for_Unity/8bit-Battle01_intro.ogg");
        am.battleMusicLoop  = Clip("Assets/Audio/Music/10 Battle1 (8bit style)/Assets_for_Unity/8bit-Battle01_loop.ogg");
    }
    private static AudioClip Clip(string path)
    {
        var c = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (c == null) Debug.LogWarning($"Audio missing: {path}");
        return c;
    }

    // ─────────────────────────────────────────────────────────
    // ⑤ Fix Physics2D collision matrix
    // ─────────────────────────────────────────────────────────
    [MenuItem("Tools/Arena Combat/⑤ Fix Physics2D Layer Matrix")]
    public static void FixPhysicsMatrix()
    {
        int g = LayerMask.NameToLayer("Ground");
        int p = LayerMask.NameToLayer("Player");
        if (g < 0 || p < 0) { Debug.LogError("Run ① first."); return; }

        Physics2D.IgnoreLayerCollision(p, g, false);  // Players land on ground ✓
        Physics2D.IgnoreLayerCollision(p, p, true);   // Bodies pass through each other
                                                       // (hits detected by CombatSystem overlap)
        Debug.Log("<color=lime>✔ Physics2D matrix: Players land on Ground, pass through each other's bodies.</color>");
    }

    // ── Scene utility ─────────────────────────────────────────
    private static void DestroyIfExists(string name)
    {
        var go = GameObject.Find(name);
        if (go) Object.DestroyImmediate(go);
    }
}
#endif
