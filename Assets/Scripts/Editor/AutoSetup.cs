/*  ============================================================
 *  AutoSetup.cs  —  Editor-only scene builder
 *  ============================================================
 *  Unity 6 (6000.x) compatible
 *
 *  HOW TO USE:
 *    Top menu → Tools → Arena Combat → ① Setup Layers & Tags
 *                                    → ② Slice All Sprite Sheets
 *                                    → ③ Build Animator Controllers
 *                                    → ④ Build Full Scene
 *    
 *    Run them IN ORDER, top to bottom. After each step Unity
 *    may reimport assets — wait for the progress bar to clear.
 *  ============================================================
 */
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class AutoSetup
{
    // ─── Paths ────────────────────────────────────────────────
    private const string K1_PATH = "Assets/Art/freeknight/Colour1/Outline/120x80_PNGSheets/";
    private const string K2_PATH = "Assets/Art/freeknight/Colour2/Outline/120x80_PNGSheets/";
    private const string ANIM_PATH = "Assets/Animations/";
    private const string CHAR_PATH = "Assets/Characters/Player/";

    // ─── Step 1 ── Layers & Tags ──────────────────────────────
    [MenuItem("Tools/Arena Combat/① Setup Layers and Tags")]
    public static void SetupLayersAndTags()
    {
        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

        // Tags
        SetTag(tagManager, "Player");
        SetTag(tagManager, "Ground");

        // Layers
        SetLayer(tagManager, 6, "Ground");
        SetLayer(tagManager, 7, "Player");

        tagManager.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();

        // Sorting layers
        EnsureSortingLayer("Background");
        EnsureSortingLayer("Midground");
        EnsureSortingLayer("Foreground");
        EnsureSortingLayer("Characters");
        EnsureSortingLayer("UI_World");

        Debug.Log("<color=lime>✔ Layers & Tags set up. Layer 6=Ground, Layer 7=Player</color>");
    }

    private static void SetTag(SerializedObject tm, string tag)
    {
        var tags = tm.FindProperty("tags");
        for (int i = 0; i < tags.arraySize; i++)
            if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;
        tags.arraySize++;
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
    }

    private static void SetLayer(SerializedObject tm, int idx, string name)
    {
        var layers = tm.FindProperty("layers");
        if (layers.arraySize <= idx)
            layers.arraySize = idx + 1;
        layers.GetArrayElementAtIndex(idx).stringValue = name;
    }

    private static void EnsureSortingLayer(string name)
    {
        // Uses reflection because there's no public API for sorting layers
        var asm = System.Reflection.Assembly.GetAssembly(typeof(Editor));
        var t   = asm.GetType("UnityEditorInternal.InternalEditorUtility");
        var prop = t?.GetProperty("sortingLayerNames",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        if (prop == null) return;
        var names = (string[])prop.GetValue(null);
        foreach (var n in names)
            if (n == name) return;

        var add = t.GetMethod("AddSortingLayer",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        add?.Invoke(null, null);
        // Rename last added
        var ids = t.GetProperty("sortingLayerUniqueIDs",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        if (ids == null) return;
        int[] idArr = (int[])ids.GetValue(null);
        var rename = t.GetMethod("SetSortingLayerName",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        rename?.Invoke(null, new object[] { idArr[idArr.Length - 1], name });
    }

    // ─── Step 2 ── Slice all sprite sheets ────────────────────
    [MenuItem("Tools/Arena Combat/② Slice All Sprite Sheets")]
    public static void SliceAllSpriteSheets()
    {
        // Slicing data: filename → frame count (all are 120×80 per frame)
        var sheets1 = new Dictionary<string, int>
        {
            { "_Idle",    10 }, { "_Run",     10 }, { "_Death",   10 },
            { "_Attack",   4 }, { "_Attack2",  6 }, { "_Hit",      1 },
            { "_Jump",     3 }, { "_Fall",     3 }, { "_Dash",     2 },
            { "_AttackCombo2hit", 10 },
        };
        var sheets2 = new Dictionary<string, int>(sheets1); // same for Colour2

        SliceSheetsInFolder(K1_PATH, sheets1, "knight1");
        SliceSheetsInFolder(K2_PATH, sheets2, "knight2");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=lime>✔ Sprite sheets sliced. Each frame = 120×80 px, PPU=32</color>");
    }

    private static void SliceSheetsInFolder(string folder, Dictionary<string,int> sheets, string prefix)
    {
        foreach (var kv in sheets)
        {
            string path = folder + kv.Key + ".png";
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { Debug.LogWarning($"Not found: {path}"); continue; }

            ti.textureType          = TextureImporterType.Sprite;
            ti.spriteImportMode     = SpriteImportMode.Multiple;
            ti.spritePixelsPerUnit  = 32;
            ti.filterMode           = FilterMode.Point;
            ti.textureCompression   = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled        = false;

            int frameCount = kv.Value;
            int frameW     = 120;
            int frameH     = 80;

            var spritesheet = new SpriteMetaData[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                spritesheet[i] = new SpriteMetaData
                {
                    name      = $"{prefix}{kv.Key}_{i}",
                    rect      = new Rect(i * frameW, 0, frameW, frameH),
                    alignment = (int)SpriteAlignment.Center,
                    pivot     = new Vector2(0.5f, 0.5f)
                };
            }
            ti.spritesheet = spritesheet;
            EditorUtility.SetDirty(ti);
            ti.SaveAndReimport();
        }
    }

    // ─── Step 3 ── Build AnimatorControllers ──────────────────
    [MenuItem("Tools/Arena Combat/③ Build Animator Controllers")]
    public static void BuildAnimatorControllers()
    {
        if (!AssetDatabase.IsValidFolder(ANIM_PATH.TrimEnd('/')))
            AssetDatabase.CreateFolder("Assets", "Animations");

        BuildController("AC_Player1", K1_PATH, "knight1");
        BuildController("AC_Player2", K2_PATH, "knight2");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=lime>✔ Animator Controllers created in Assets/Animations/</color>");
    }

    private static AnimatorController BuildController(string ctrlName, string folder, string prefix)
    {
        string ctrlPath = ANIM_PATH + ctrlName + ".controller";

        // Delete old if exists
        AssetDatabase.DeleteAsset(ctrlPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);

        // ── Parameters ──────────────────────────────────
        ctrl.AddParameter("isMoving",   AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("isRunning",  AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("isGrounded", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("velocityY",  AnimatorControllerParameterType.Float);
        ctrl.AddParameter("jump",       AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("lightAttack",AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("heavyAttack",AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("hit",        AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("death",      AnimatorControllerParameterType.Trigger);

        var root = ctrl.layers[0].stateMachine;

        // ── Build animation clips ──────────────────────
        var clipIdle    = MakeClip(folder, prefix + "_Idle",    10,  8, ctrlPath);
        var clipRun     = MakeClip(folder, prefix + "_Run",     10, 12, ctrlPath);
        var clipJump    = MakeClip(folder, prefix + "_Jump",     3, 10, ctrlPath);
        var clipFall    = MakeClip(folder, prefix + "_Fall",     3, 10, ctrlPath);
        var clipLight   = MakeClip(folder, prefix + "_Attack",   4, 16, ctrlPath);
        var clipHeavy   = MakeClip(folder, prefix + "_Attack2",  6, 12, ctrlPath);
        var clipHit     = MakeClip(folder, prefix + "_Hit",      1, 12, ctrlPath);
        var clipDeath   = MakeClip(folder, prefix + "_Death",   10,  8, ctrlPath);

        // ── States ────────────────────────────────────
        var sIdle  = AddState(root, "Idle",        clipIdle,  true);
        var sRun   = AddState(root, "Run",         clipRun,   false);
        var sJump  = AddState(root, "Jump",        clipJump,  false);
        var sFall  = AddState(root, "Fall",        clipFall,  false);
        var sLight = AddState(root, "LightAttack", clipLight, false);
        var sHeavy = AddState(root, "HeavyAttack", clipHeavy, false);
        var sHit   = AddState(root, "Hit",         clipHit,   false);
        var sDeath = AddState(root, "Death",       clipDeath, false);
        sDeath.speed = 0.9f;

        // ── Transitions ─────────────────────────────
        // Idle ↔ Run
        AddBoolTrans(sIdle, sRun,  "isMoving", true,  0.05f);
        AddBoolTrans(sRun,  sIdle, "isMoving", false, 0.05f);

        // Jump
        AddTriggerTrans(sIdle,  sJump, "jump");
        AddTriggerTrans(sRun,   sJump, "jump");
        AddTriggerTrans(sFall,  sJump, "jump");

        // Jump → Fall (when velocity goes negative)
        var j2f = sJump.AddTransition(sFall);
        j2f.hasExitTime = false;
        j2f.duration    = 0.05f;
        j2f.AddCondition(AnimatorConditionMode.Less, 0f, "velocityY");

        // Fall → Idle (when grounded)
        var f2i = sFall.AddTransition(sIdle);
        f2i.hasExitTime = false;
        f2i.duration    = 0.05f;
        f2i.AddCondition(AnimatorConditionMode.If, 0, "isGrounded");

        // AnyState → Attacks (no exit time so they interrupt movement)
        AddTriggerTransFromAny(root, sLight, "lightAttack");
        AddTriggerTransFromAny(root, sHeavy, "heavyAttack");

        // Attacks → Idle (exit time based)
        AddExitTimeTrans(sLight, sIdle, 0.9f, 0.05f);
        AddExitTimeTrans(sHeavy, sIdle, 0.9f, 0.05f);

        // AnyState → Hit
        AddTriggerTransFromAny(root, sHit, "hit");
        AddExitTimeTrans(sHit, sIdle, 0.9f, 0.05f);

        // AnyState → Death (can't recover)
        AddTriggerTransFromAny(root, sDeath, "death");

        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    private static AnimationClip MakeClip(string folder, string spriteName, int frames, float fps, string ctrlPath)
    {
        string clipPath = Path.GetDirectoryName(ctrlPath) + "/" + spriteName + ".anim";
        AssetDatabase.DeleteAsset(clipPath);

        var clip = new AnimationClip();
        clip.frameRate = fps;

        // Load sprites from the sliced sheet
        string sheetKey = spriteName.Substring(spriteName.LastIndexOf('_'));  // e.g. "_Idle"
        // The actual sprite name in the sheet uses the format prefix+sheetKey+_0, prefix+sheetKey+_1 ...
        var sprites = new List<Sprite>();
        Object[] all = AssetDatabase.LoadAllAssetsAtPath(folder + sheetKey + ".png");
        foreach (var o in all)
            if (o is Sprite sp && sp.name.Contains(sheetKey))
                sprites.Add(sp);
        sprites.Sort((a, b) =>
        {
            // sort by trailing number
            int ai = int.Parse(a.name.Split('_')[^1]);
            int bi = int.Parse(b.name.Split('_')[^1]);
            return ai.CompareTo(bi);
        });

        if (sprites.Count == 0)
        {
            Debug.LogWarning($"No sprites found for {spriteName} in {folder}. Did you run Step 2?");
            clip.name = spriteName;
            AssetDatabase.CreateAsset(clip, clipPath);
            return clip;
        }

        // Build object reference curve
        var settings = new AnimationClipSettings { loopTime = true };
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        float dt = 1f / fps;
        var binding = new EditorCurveBinding
        {
            type         = typeof(SpriteRenderer),
            path         = "",
            propertyName = "m_Sprite"
        };

        var keyframes = new ObjectReferenceKeyframe[sprites.Count + 1];
        for (int i = 0; i < sprites.Count; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time  = i * dt,
                value = sprites[i]
            };
        }
        // Last frame = first frame (seamless loop)
        keyframes[sprites.Count] = new ObjectReferenceKeyframe
        {
            time  = sprites.Count * dt,
            value = sprites[0]
        };

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
        clip.name = spriteName;
        AssetDatabase.CreateAsset(clip, clipPath);
        return clip;
    }

    private static AnimatorState AddState(AnimatorStateMachine sm, string name, AnimationClip clip, bool isDefault)
    {
        var state = sm.AddState(name);
        state.motion = clip;
        if (isDefault) sm.defaultState = state;
        return state;
    }

    private static void AddBoolTrans(AnimatorState from, AnimatorState to, string param, bool val, float dur)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration    = dur;
        if (val) t.AddCondition(AnimatorConditionMode.If,    0, param);
        else     t.AddCondition(AnimatorConditionMode.IfNot, 0, param);
    }

    private static void AddTriggerTrans(AnimatorState from, AnimatorState to, string trigger)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration    = 0.05f;
        t.AddCondition(AnimatorConditionMode.If, 0, trigger);
    }

    private static void AddTriggerTransFromAny(AnimatorStateMachine sm, AnimatorState to, string trigger)
    {
        var t = sm.AddAnyStateTransition(to);
        t.hasExitTime = false;
        t.duration    = 0.05f;
        t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If, 0, trigger);
    }

    private static void AddExitTimeTrans(AnimatorState from, AnimatorState to, float exitTime, float dur)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = true;
        t.exitTime    = exitTime;
        t.duration    = dur;
    }

    // ─── Step 4 ── Build full scene ───────────────────────────
    [MenuItem("Tools/Arena Combat/④ Build Full Scene (Adds All GameObjects)")]
    public static void BuildFullScene()
    {
        // ── Layers prerequisite check ──────────────────
        if (LayerMask.NameToLayer("Ground") == -1 || LayerMask.NameToLayer("Player") == -1)
        {
            EditorUtility.DisplayDialog("Missing Layers",
                "Run Step ① first to create Ground (6) and Player (7) layers.", "OK");
            return;
        }

        int groundLayer = LayerMask.NameToLayer("Ground");
        int playerLayer = LayerMask.NameToLayer("Player");

        // ── Delete any previous auto-setup objects ─────
        DestroyIfExists("Ground");
        DestroyIfExists("LeftPlatform");
        DestroyIfExists("RightPlatform");
        DestroyIfExists("Player1");
        DestroyIfExists("Player2");
        DestroyIfExists("SpawnPoint1");
        DestroyIfExists("SpawnPoint2");
        DestroyIfExists("GameManager");
        DestroyIfExists("ArenaManager");
        DestroyIfExists("AudioManager");
        DestroyIfExists("HUD_Canvas");

        // ── Ground ────────────────────────────────────
        var ground = CreatePlatform("Ground", new Vector3(0, -2.5f, 0),
            new Vector3(18f, 0.5f, 1f), groundLayer, new Color32(80, 50, 20, 255));

        // ── Side Platforms (Brawlhalla-style) ─────────
        CreatePlatform("LeftPlatform",  new Vector3(-5f,  0f, 0), new Vector3(4f, 0.3f, 1f),  groundLayer, new Color32(60, 100, 40, 255));
        CreatePlatform("RightPlatform", new Vector3( 5f,  0f, 0), new Vector3(4f, 0.3f, 1f),  groundLayer, new Color32(60, 100, 40, 255));
        CreatePlatform("TopPlatform",   new Vector3( 0f,  2.5f, 0), new Vector3(3f, 0.3f, 1f), groundLayer, new Color32(40, 80, 120, 255));

        // ── Spawn points ──────────────────────────────
        var sp1 = new GameObject("SpawnPoint1");
        sp1.transform.position = new Vector3(-4f, -1.5f, 0);
        var sp2 = new GameObject("SpawnPoint2");
        sp2.transform.position = new Vector3( 4f, -1.5f, 0);

        // ── Players ───────────────────────────────────
        var p1go = BuildPlayer("Player1", 1, playerLayer, groundLayer,
            new Vector3(-4f, -1.5f, 0), "AC_Player1");
        var p2go = BuildPlayer("Player2", 2, playerLayer, groundLayer,
            new Vector3( 4f, -1.5f, 0), "AC_Player2");

        // ── GameManager ───────────────────────────────
        var gmGO = new GameObject("GameManager");
        var gsm  = gmGO.AddComponent<GameStateManager>();
        gsm.player1   = p1go.GetComponent<PlayerController>();
        gsm.player2   = p2go.GetComponent<PlayerController>();
        gsm.spawnP1   = sp1.transform;
        gsm.spawnP2   = sp2.transform;
        gsm.roundsToWin  = 2;
        gsm.matchTimeSec = 99f;

        // ── ArenaManager ──────────────────────────────
        var amGO = new GameObject("ArenaManager");
        var am   = amGO.AddComponent<ArenaManager>();
        am.arenaCamera        = Camera.main;
        am.player1Transform   = p1go.transform;
        am.player2Transform   = p2go.transform;
        am.deathZoneY         = -8f;

        // ── AudioManager ──────────────────────────────
        var audioGO = new GameObject("AudioManager");
        var audio   = audioGO.AddComponent<AudioManager>();
        // Wire up audio clips automatically
        WireAudioClips(audio);

        // ── UI Canvas ─────────────────────────────────
        BuildHUDCanvas(gsm, p1go, p2go);

        // ── Mark scene dirty ──────────────────────────
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("<color=lime>✔ Full scene built! Press Play to test. Ctrl+S to save.</color>");
        EditorUtility.DisplayDialog("✔ Scene Built!",
            "All GameObjects created and wired.\n\n" +
            "Controls:\n" +
            "  P1: A/D move, W/Space jump, J light, K heavy\n" +
            "  P2: Numpad 4/6 move, Numpad 8 jump, Numpad0 light, NumpadEnter heavy\n\n" +
            "Press Ctrl+S to save the scene, then press Play!", "Got it!");
    }

    // ── Creates a coloured platform with collider ──────────────
    private static GameObject CreatePlatform(string name, Vector3 pos, Vector3 scale,
        int layer, Color32 color)
    {
        var go = new GameObject(name);
        go.layer          = layer;
        go.transform.position = pos;
        go.tag            = "Ground";

        // Visible quad
        var sr            = go.AddComponent<SpriteRenderer>();
        sr.sprite         = CreateSolidSprite(color);
        sr.drawMode       = SpriteDrawMode.Tiled;
        sr.size           = new Vector2(scale.x, scale.y);
        sr.sortingLayerName = "Foreground";
        sr.sortingOrder   = -1;

        // Collider
        var col           = go.AddComponent<BoxCollider2D>();
        col.size          = new Vector2(scale.x, scale.y);

        return go;
    }

    // ── Creates a 1×1 white sprite and tints it ────────────────
    private static Sprite CreateSolidSprite(Color32 color)
    {
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        var pixels = new Color32[16];
        for (int i = 0; i < 16; i++) pixels[i] = color;
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
    }

    // ── Builds a full player GameObject ───────────────────────
    private static GameObject BuildPlayer(string name, int index, int playerLayer,
        int groundLayer, Vector3 spawnPos, string controllerName)
    {
        var go = new GameObject(name);
        go.layer = playerLayer;
        go.tag   = "Player";
        go.transform.position = spawnPos;

        // SpriteRenderer
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Characters";
        sr.sortingOrder     = index;

        // Rigidbody2D
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.constraints  = RigidbodyConstraints2D.FreezeRotation;

        // Capsule Collider (body)
        var cc          = go.AddComponent<CapsuleCollider2D>();
        cc.size         = new Vector2(0.6f, 1.4f);
        cc.offset       = new Vector2(0f, 0.05f);

        // Animator
        var anim = go.AddComponent<Animator>();
        // Try to load the controller we just created
        string ctrlPath = ANIM_PATH + controllerName + ".controller";
        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ctrlPath);
        if (ctrl != null)
            anim.runtimeAnimatorController = ctrl;
        else
            Debug.LogWarning($"Controller not found at {ctrlPath} — run Step ③ first.");

        // PlayerController
        var pc              = go.AddComponent<PlayerController>();
        pc.playerIndex      = index;
        pc.walkSpeed        = 8f;
        pc.runSpeed         = 13f;
        pc.jumpForce        = 16f;
        pc.fastFallForce    = 22f;
        pc.groundCheckRadius = 0.18f;

        // GroundCheck child
        var gcGO = new GameObject("GroundCheck");
        gcGO.transform.SetParent(go.transform);
        gcGO.transform.localPosition = new Vector3(0f, -0.75f, 0f);
        pc.groundCheck   = gcGO.transform;
        pc.groundLayer   = 1 << groundLayer;

        // AttackPoint child (at sword/fist side)
        var apGO = new GameObject("AttackPoint");
        apGO.transform.SetParent(go.transform);
        apGO.transform.localPosition = new Vector3(0.7f, 0.1f, 0f);

        // HealthManager
        go.AddComponent<HealthManager>();

        // CombatSystem
        var cs = go.AddComponent<CombatSystem>();
        cs.attackPoint  = apGO.transform;
        cs.playerLayer  = 1 << playerLayer;
        cs.lightAttackRadius = 0.6f;
        cs.heavyAttackRadius = 0.9f;

        // AnimationController helper
        go.AddComponent<AnimationController>();

        // DamageNumberSpawner (prefab assigned later via Inspector if needed)
        go.AddComponent<DamageNumberSpawner>();

        // Scale up so it's visible (knight frames are small)
        go.transform.localScale = index == 2
            ? new Vector3(-2f, 2f, 1f)  // P2 faces left
            : new Vector3( 2f, 2f, 1f);

        return go;
    }

    // ── Build HUD Canvas ──────────────────────────────────────
    private static void BuildHUDCanvas(GameStateManager gsm, GameObject p1go, GameObject p2go)
    {
        // Root canvas
        var canvasGO = new GameObject("HUD_Canvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode       = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder     = 10;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        var cs = canvasGO.GetComponent<CanvasScaler>();
        cs.uiScaleMode          = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution  = new Vector2(1920, 1080);
        cs.matchWidthOrHeight   = 0.5f;

        // ── Timer ─────────────────────────────────────
        var timerGO  = CreateTMPText(canvasGO, "TimerText", "99",
            new Vector2(0, -35), new Vector2(120, 80), 72, Color.white, TextAlignmentOptions.Center);
        var timerAnch = timerGO.GetComponent<RectTransform>();
        timerAnch.anchorMin    = new Vector2(0.5f, 1f);
        timerAnch.anchorMax    = new Vector2(0.5f, 1f);
        timerAnch.pivot        = new Vector2(0.5f, 1f);

        // ── P1 Health bar area ─────────────────────────
        var hbP1GO = CreateHealthBar(canvasGO, "HealthBarP1",
            new Vector2(280, -30), new Vector2(500, 35), false);

        // ── P2 Health bar area (mirrored) ──────────────
        var hbP2GO = CreateHealthBar(canvasGO, "HealthBarP2",
            new Vector2(-280, -30), new Vector2(500, 35), true);

        // ── Player name labels ─────────────────────────
        var p1Label = CreateTMPText(canvasGO, "P1Label", "PLAYER 1",
            new Vector2(280, -8), new Vector2(200, 30), 20, new Color(0.4f, 0.8f, 1f), TextAlignmentOptions.Left);
        p1Label.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 1f);
        p1Label.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 1f);

        var p2Label = CreateTMPText(canvasGO, "P2Label", "PLAYER 2",
            new Vector2(-280, -8), new Vector2(200, 30), 20, new Color(1f, 0.4f, 0.4f), TextAlignmentOptions.Right);
        p2Label.GetComponent<RectTransform>().anchorMin = new Vector2(1f, 1f);
        p2Label.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);

        // ── Score / round win dots ─────────────────────
        var p1Score = CreateTMPText(canvasGO, "P1Score", "",
            new Vector2(280, -70), new Vector2(150, 30), 24, Color.yellow, TextAlignmentOptions.Left);
        p1Score.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 1f);
        p1Score.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 1f);

        var p2Score = CreateTMPText(canvasGO, "P2Score", "",
            new Vector2(-280, -70), new Vector2(150, 30), 24, Color.yellow, TextAlignmentOptions.Right);
        p2Score.GetComponent<RectTransform>().anchorMin = new Vector2(1f, 1f);
        p2Score.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);

        // ── Announcement text (centre) ─────────────────
        var announceGO = CreateTMPText(canvasGO, "AnnouncementText", "",
            new Vector2(0, 0), new Vector2(900, 120), 72, Color.yellow, TextAlignmentOptions.Center);
        var annTMP = announceGO.GetComponent<TextMeshProUGUI>();
        annTMP.fontStyle = FontStyles.Bold;

        // ── Screen flash overlay ───────────────────────
        var flashGO  = new GameObject("ScreenFlash");
        flashGO.transform.SetParent(canvasGO.transform, false);
        var flashImg = flashGO.AddComponent<Image>();
        flashImg.color = new Color(1, 1, 1, 0);
        var flashRT  = flashGO.GetComponent<RectTransform>();
        flashRT.anchorMin = Vector2.zero;
        flashRT.anchorMax = Vector2.one;
        flashRT.offsetMin = flashRT.offsetMax = Vector2.zero;
        var sf = canvasGO.AddComponent<ScreenFlash>();
        sf.flashImage = flashImg;

        // ── Match Over Panel ───────────────────────────
        var panelGO = new GameObject("MatchOverPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        var panelImg = panelGO.AddComponent<Image>();
        panelImg.color = new Color(0, 0, 0, 0.75f);
        var panelRT  = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = panelRT.offsetMax = Vector2.zero;

        var resultGO = CreateTMPText(panelGO, "ResultText", "PLAYER 1\nWINS!",
            new Vector2(0, 80), new Vector2(700, 200), 96, Color.yellow, TextAlignmentOptions.Center);
        resultGO.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        var controlsGO = CreateTMPText(panelGO, "ControlsHint",
            "P1: A/D Move  W/Space Jump  J Light  K Heavy\nP2: Numpad 4/6 Move  Num8 Jump  Num0 Light  NumEnter Heavy",
            new Vector2(0, -20), new Vector2(900, 80), 22, Color.white, TextAlignmentOptions.Center);

        var rematchBtn = CreateButton(panelGO, "RematchButton", "▶  PLAY AGAIN",
            new Vector2(-160, -130), new Vector2(260, 60));
        var quitBtn    = CreateButton(panelGO, "QuitButton",    "✕  QUIT",
            new Vector2(160, -130),  new Vector2(200, 60));

        panelGO.SetActive(false);

        // ── Controls hint (always visible) ────────────
        var ctrlHintGO = CreateTMPText(canvasGO, "ControlsHint",
            "P1: A/D  W=Jump  J=Light  K=Heavy   |   P2: Numpad 4/6  8=Jump  0=Light  Enter=Heavy",
            new Vector2(0, 20), new Vector2(1400, 30), 18, new Color(1,1,1,0.5f), TextAlignmentOptions.Center);
        ctrlHintGO.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0f);
        ctrlHintGO.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0f);

        // ── UIManager component ────────────────────────
        var uimGO = new GameObject("UIManager");
        uimGO.transform.SetParent(canvasGO.transform, false);
        var uim = uimGO.AddComponent<UIManager>();

        // Wire references
        uim.healthBarP1    = hbP1GO.GetComponent<Slider>();
        uim.healthBarP2    = hbP2GO.GetComponent<Slider>();
        uim.healthFillP1   = GetFillImage(hbP1GO);
        uim.healthFillP2   = GetFillImage(hbP2GO);
        uim.timerText      = timerGO.GetComponent<TextMeshProUGUI>();
        uim.announcementText = announceGO.GetComponent<TextMeshProUGUI>();
        uim.p1ScoreText    = p1Score.GetComponent<TextMeshProUGUI>();
        uim.p2ScoreText    = p2Score.GetComponent<TextMeshProUGUI>();
        uim.matchOverPanel = panelGO;
        uim.resultText     = resultGO.GetComponent<TextMeshProUGUI>();
        uim.rematchButton  = rematchBtn.GetComponent<Button>();
        uim.quitButton     = quitBtn.GetComponent<Button>();
    }

    // ── Helper: TMP text ──────────────────────────────────────
    private static GameObject CreateTMPText(GameObject parent, string name, string text,
        Vector2 anchoredPos, Vector2 sizeDelta, float fontSize, Color color,
        TextAlignmentOptions alignment)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = color;
        tmp.alignment = alignment;

        var rt        = go.GetComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta  = sizeDelta;
        return go;
    }

    // ── Helper: Slider health bar ─────────────────────────────
    private static GameObject CreateHealthBar(GameObject parent, string name,
        Vector2 anchoredPos, Vector2 sizeDelta, bool rightAligned)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);

        // Slider
        var slider        = go.AddComponent<Slider>();
        slider.minValue   = 0f;
        slider.maxValue   = 1f;
        slider.value      = 1f;
        slider.wholeNumbers = false;
        slider.direction  = rightAligned
            ? Slider.Direction.RightToLeft
            : Slider.Direction.LeftToRight;

        var rt            = go.GetComponent<RectTransform>();
        rt.sizeDelta      = sizeDelta;
        rt.anchoredPosition = anchoredPos;
        if (!rightAligned) { rt.anchorMin = new Vector2(0f,1f); rt.anchorMax = new Vector2(0f,1f); rt.pivot = new Vector2(0f,1f); }
        else               { rt.anchorMin = new Vector2(1f,1f); rt.anchorMax = new Vector2(1f,1f); rt.pivot = new Vector2(1f,1f); }

        // Background image (dark red)
        var bg          = go.AddComponent<Image>();
        bg.color        = new Color(0.2f, 0.05f, 0.05f, 1f);
        slider.targetGraphic = bg;

        // Fill area
        var fillArea    = new GameObject("Fill Area");
        fillArea.transform.SetParent(go.transform, false);
        var faRT        = fillArea.AddComponent<RectTransform>();
        faRT.anchorMin  = Vector2.zero;
        faRT.anchorMax  = Vector2.one;
        faRT.offsetMin  = new Vector2(2, 2);
        faRT.offsetMax  = new Vector2(-2, -2);

        var fillGO      = new GameObject("Fill");
        fillGO.transform.SetParent(fillArea.transform, false);
        var fillImg     = fillGO.AddComponent<Image>();
        fillImg.color   = new Color(0.1f, 0.9f, 0.1f);
        var fillRT      = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;

        slider.fillRect = fillRT;

        return go;
    }

    // ── Helper: Get fill Image from a Slider ──────────────────
    private static Image GetFillImage(GameObject sliderGO)
    {
        var fills = sliderGO.GetComponentsInChildren<Image>(true);
        foreach (var img in fills)
            if (img.gameObject.name == "Fill") return img;
        return null;
    }

    // ── Helper: UI Button ─────────────────────────────────────
    private static GameObject CreateButton(GameObject parent, string name, string label,
        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var img  = go.AddComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.25f, 0.95f);
        var btn  = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var rt   = go.GetComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        var txtGO = CreateTMPText(go, "Label", label,
            Vector2.zero, sizeDelta, 26, Color.white, TextAlignmentOptions.Center);
        txtGO.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        return go;
    }

    // ── Wire audio clips ──────────────────────────────────────
    private static void WireAudioClips(AudioManager am)
    {
        am.lightSwingSFX = LoadAudio("Assets/Audio/SFX/12_Player_Movement_SFX/56_Attack_03.wav");
        am.heavySwingSFX = LoadAudio("Assets/Audio/SFX/10_Battle_SFX/22_Slash_04.wav");
        am.lightHitSFX   = LoadAudio("Assets/Audio/SFX/12_Player_Movement_SFX/61_Hit_03.wav");
        am.heavyHitSFX   = LoadAudio("Assets/Audio/SFX/10_Battle_SFX/15_Impact_flesh_02.wav");
        am.deathSFX      = LoadAudio("Assets/Audio/SFX/10_Battle_SFX/69_Enemy_death_01.wav");
        am.jumpSFX       = LoadAudio("Assets/Audio/SFX/12_Player_Movement_SFX/30_Jump_03.wav");
        am.landSFX       = LoadAudio("Assets/Audio/SFX/12_Player_Movement_SFX/45_Landing_01.wav");
        am.roundStartSFX = LoadAudio("Assets/Audio/SFX/10_Battle_SFX/55_Encounter_02.wav");
        am.uiConfirmSFX  = LoadAudio("Assets/Audio/SFX/10_UI_Menu_SFX/013_Confirm_03.wav");
        am.uiDeclineSFX  = LoadAudio("Assets/Audio/SFX/10_UI_Menu_SFX/029_Decline_09.wav");
        am.battleMusicIntro = LoadAudio("Assets/Audio/Music/10 Battle1 (8bit style)/Assets_for_Unity/8bit-Battle01_intro.ogg");
        am.battleMusicLoop  = LoadAudio("Assets/Audio/Music/10 Battle1 (8bit style)/Assets_for_Unity/8bit-Battle01_loop.ogg");
    }

    private static AudioClip LoadAudio(string path)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null) Debug.LogWarning($"Audio not found: {path}");
        return clip;
    }

    // ── Destroy existing by name ───────────────────────────────
    private static void DestroyIfExists(string name)
    {
        var existing = GameObject.Find(name);
        if (existing != null) Object.DestroyImmediate(existing);
    }

    // ─── Bonus: Fix Physics2D collision matrix ─────────────────
    [MenuItem("Tools/Arena Combat/⑤ Fix Physics2D Layer Matrix")]
    public static void FixPhysicsMatrix()
    {
        int ground = LayerMask.NameToLayer("Ground");
        int player = LayerMask.NameToLayer("Player");
        if (ground < 0 || player < 0)
        {
            Debug.LogError("Run Step ① first to create layers.");
            return;
        }
        // Player↔Ground = collide
        Physics2D.IgnoreLayerCollision(player, ground, false);
        // Player↔Player = ignore body collision (hitbox handled by overlap check)
        Physics2D.IgnoreLayerCollision(player, player, true);
        // Default↔Player
        Physics2D.IgnoreLayerCollision(0, player, false);
        Debug.Log("<color=lime>✔ Physics2D matrix set: Players pass through each other's bodies but detect hits via CombatSystem overlap.</color>");
    }
}
#endif
