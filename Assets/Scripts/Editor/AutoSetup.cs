#if UNITY_EDITOR
// ==========================================================
//  AutoSetup.cs — Tools → Arena Combat → run ①②③④⑤⑥ in order
//  Unity 6 (6000.x) | New Input System
//  Fixes: spawn height, GroundCheck position, UIManager
//         health wiring, player prefabs in Characters/Player
// ==========================================================
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class AutoSetup
{
    // Paths
    const string K1    = "Assets/Art/freeknight/Colour1/Outline/120x80_PNGSheets/";
    const string K2    = "Assets/Art/freeknight/Colour2/Outline/120x80_PNGSheets/";
    const string ANIM  = "Assets/Animations/";
    const string UI    = "Assets/UI/";
    const string BG    = "Assets/Art/freecutetileset/";
    const string AUDIO = "Assets/Audio/";
    const string CHAR  = "Assets/Characters/Player/";

    // Arena geometry constants — keep in sync with CreatePlatform calls below
    const float GROUND_Y      = -3f;
    const float GROUND_H      = 0.6f;
    const float GROUND_TOP    = GROUND_Y + GROUND_H / 2f;   // = -2.7
    // Player capsule (local space, scale=2 in world)
    const float CAP_OFFSET_Y  = 0.4f;
    const float CAP_HEIGHT    = 1.2f;
    const float CAP_BOT_LOCAL = CAP_OFFSET_Y - CAP_HEIGHT / 2f;  // = -0.2
    const float PLAYER_SCALE  = 2f;
    // spawn_y = GROUND_TOP - CAP_BOT_LOCAL * PLAYER_SCALE = -2.7 - (-0.4) = -2.3
    const float SPAWN_Y       = GROUND_TOP - CAP_BOT_LOCAL * PLAYER_SCALE;  // -2.3

    // =========================================================
    // ① Layers & Tags
    // =========================================================
    [MenuItem("Tools/Arena Combat/\u2460 Setup Layers and Tags")]
    public static void Step1_SetupLayers()
    {
        var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (asset == null || asset.Length == 0) { Debug.LogError("Cannot open TagManager.asset"); return; }

        var so = new SerializedObject(asset[0]);

        // "Ground" tag — "Player" is already built-in
        var tags = so.FindProperty("tags");
        bool hasGround = false;
        for (int i = 0; i < tags.arraySize; i++)
            if (tags.GetArrayElementAtIndex(i).stringValue == "Ground") { hasGround = true; break; }
        if (!hasGround)
        {
            tags.arraySize++;
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = "Ground";
        }

        var layers = so.FindProperty("layers");
        if (layers.arraySize < 8) layers.arraySize = 8;
        layers.GetArrayElementAtIndex(6).stringValue = "Ground";
        layers.GetArrayElementAtIndex(7).stringValue = "Player";

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset[0]);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=lime>\u2713 Step 1: Layer 6=Ground, Layer 7=Player, Tag 'Ground' added.</color>");
    }

    // =========================================================
    // ② Slice sprite sheets
    // =========================================================
    [MenuItem("Tools/Arena Combat/\u2461 Slice Sprite Sheets")]
    public static void Step2_SliceSheets()
    {
        var sheets = new Dictionary<string, int>
        {
            { "_Idle",7 }, { "_Run",10 }, { "_Death",10 },
            { "_Attack",4 }, { "_Attack2",6 }, { "_Hit",1 },
            { "_Jump",3 }, { "_Fall",3 },
        };

        int done = 0;
        foreach (var kv in sheets)
        {
            if (SliceOne(K1 + kv.Key + ".png", kv.Key, "k1", kv.Value)) done++;
            if (SliceOne(K2 + kv.Key + ".png", kv.Key, "k2", kv.Value)) done++;
        }

        ImportSingle(BG + "background_wide.png", 16f, FilterMode.Point);
        ImportSingle(BG + "BG1.png",             16f, FilterMode.Point);

        foreach (var f in new[]{
            "health_bar_bg.png","health_bar_fill.png","timer_bg.png","nameplate_bg.png",
            "win_dot_filled.png","win_dot_empty.png","panel_dark.png",
            "button_normal.png","button_hover.png","hud_top_bar.png"})
            ImportSingle(UI + f, 1f, FilterMode.Bilinear);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"<color=lime>\u2713 Step 2: {done} sheets sliced + textures imported.</color>");
    }

    static bool SliceOne(string path, string key, string prefix, int frames)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) { Debug.LogWarning("Missing: " + path); return false; }

        ti.textureType         = TextureImporterType.Sprite;
        ti.spriteImportMode    = SpriteImportMode.Multiple;
        ti.spritePixelsPerUnit = 32f;
        ti.filterMode          = FilterMode.Point;
        ti.mipmapEnabled       = false;
        ti.textureCompression  = TextureImporterCompression.Uncompressed;
        ti.alphaIsTransparency = true;
        EditorUtility.SetDirty(ti);
        ti.SaveAndReimport();

        var fac = new SpriteDataProviderFactories(); fac.Init();
        var dp  = fac.GetSpriteEditorDataProviderFromObject(ti);
        if (dp == null) return false;

        dp.InitSpriteEditorDataProvider();
        var rects = new SpriteRect[frames];
        for (int i = 0; i < frames; i++)
            rects[i] = new SpriteRect
            {
                name      = prefix + key + "_" + i,
                rect      = new Rect(i * 120, 0, 120, 80),
                pivot     = new Vector2(0.5f, 0.2f),
                alignment = SpriteAlignment.Custom,
                spriteID  = GUID.Generate()
            };

        dp.SetSpriteRects(rects);
        dp.Apply();
        ((AssetImporter)dp.targetObject).SaveAndReimport();
        return true;
    }

    static void ImportSingle(string path, float ppu, FilterMode filter)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;
        ti.textureType         = TextureImporterType.Sprite;
        ti.spriteImportMode    = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = ppu;
        ti.filterMode          = filter;
        ti.mipmapEnabled       = false;
        ti.textureCompression  = TextureImporterCompression.Uncompressed;
        ti.alphaIsTransparency = true;
        EditorUtility.SetDirty(ti);
        ti.SaveAndReimport();
    }

    // =========================================================
    // ③ Build Animator Controllers
    // =========================================================
    [MenuItem("Tools/Arena Combat/\u2462 Build Animator Controllers")]
    public static void Step3_BuildControllers()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Animations"))
            AssetDatabase.CreateFolder("Assets", "Animations");
        BuildCtrl("AC_Player1", K1, "k1");
        BuildCtrl("AC_Player2", K2, "k2");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=lime>\u2713 Step 3: AC_Player1 & AC_Player2 ready in Assets/Animations/</color>");
    }

    static void BuildCtrl(string name, string folder, string prefix)
    {
        string path = ANIM + name + ".controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);

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

        var sIdle  = St(sm, "Idle",        Clip(folder,prefix+"_Idle",   7,  8f,path,true),  true);
        var sRun   = St(sm, "Run",         Clip(folder,prefix+"_Run",   10, 12f,path,true),  false);
        var sJump  = St(sm, "Jump",        Clip(folder,prefix+"_Jump",   3, 10f,path,false), false);
        var sFall  = St(sm, "Fall",        Clip(folder,prefix+"_Fall",   3, 10f,path,false), false);
        var sLight = St(sm, "LightAttack", Clip(folder,prefix+"_Attack", 4, 18f,path,false), false);
        var sHeavy = St(sm, "HeavyAttack", Clip(folder,prefix+"_Attack2",6, 12f,path,false), false);
        var sHit   = St(sm, "Hit",         Clip(folder,prefix+"_Hit",    1, 12f,path,false), false);
        var sDeath = St(sm, "Death",       Clip(folder,prefix+"_Death", 10,  8f,path,false), false);

        BoolT(sIdle, sRun,  "isMoving", true,  0.05f);
        BoolT(sRun,  sIdle, "isMoving", false, 0.05f);
        TrigT(sIdle, sJump, "jump");
        TrigT(sRun,  sJump, "jump");
        TrigT(sFall, sJump, "jump");

        var jf = sJump.AddTransition(sFall);
        jf.hasExitTime = false; jf.duration = 0.05f;
        jf.AddCondition(AnimatorConditionMode.Less, 0f, "velocityY");

        var fi = sFall.AddTransition(sIdle);
        fi.hasExitTime = false; fi.duration = 0.05f;
        fi.AddCondition(AnimatorConditionMode.If, 0, "isGrounded");

        AnyT(sm, sLight, "lightAttack"); ExitT(sLight, sIdle, 0.9f, 0.05f);
        AnyT(sm, sHeavy, "heavyAttack"); ExitT(sHeavy, sIdle, 0.9f, 0.05f);
        AnyT(sm, sHit,   "hit");         ExitT(sHit,   sIdle, 0.85f, 0.05f);
        AnyT(sm, sDeath, "death");

        EditorUtility.SetDirty(ctrl);
    }

    static AnimationClip Clip(string folder, string clipName, int frames, float fps, string ctrlPath, bool loop)
    {
        string clipPath = Path.GetDirectoryName(ctrlPath) + "/" + clipName + ".anim";
        AssetDatabase.DeleteAsset(clipPath);
        var clip = new AnimationClip { frameRate = fps, name = clipName };
        var cs = AnimationUtility.GetAnimationClipSettings(clip);
        cs.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, cs);

        int us = clipName.IndexOf('_');
        string sheetKey = us >= 0 ? clipName.Substring(us) : clipName;
        var sprites = AssetDatabase.LoadAllAssetsAtPath(folder + sheetKey + ".png")
            .OfType<Sprite>()
            .OrderBy(s => { int n = 0; int.TryParse(s.name.Split('_').Last(), out n); return n; })
            .ToArray();

        if (sprites.Length == 0)
        {
            Debug.LogWarning("No sprites for " + clipName + " — run Step ② first.");
            AssetDatabase.CreateAsset(clip, clipPath);
            return clip;
        }

        float dt = 1f / fps;
        var binding = new EditorCurveBinding { type = typeof(SpriteRenderer), path = "", propertyName = "m_Sprite" };
        int kc = sprites.Length + (loop ? 1 : 0);
        var keys = new ObjectReferenceKeyframe[kc];
        for (int i = 0; i < sprites.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i * dt, value = sprites[i] };
        if (loop) keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length * dt, value = sprites[0] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        AssetDatabase.CreateAsset(clip, clipPath);
        return clip;
    }

    static AnimatorState St(AnimatorStateMachine sm, string n, AnimationClip c, bool def)
    { var s = sm.AddState(n); s.motion = c; if (def) sm.defaultState = s; return s; }
    static void BoolT(AnimatorState f, AnimatorState t, string p, bool v, float d)
    { var x = f.AddTransition(t); x.hasExitTime = false; x.duration = d; x.AddCondition(v ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, p); }
    static void TrigT(AnimatorState f, AnimatorState t, string p)
    { var x = f.AddTransition(t); x.hasExitTime = false; x.duration = 0.05f; x.AddCondition(AnimatorConditionMode.If, 0, p); }
    static void AnyT(AnimatorStateMachine sm, AnimatorState t, string p)
    { var x = sm.AddAnyStateTransition(t); x.hasExitTime = false; x.duration = 0.05f; x.canTransitionToSelf = false; x.AddCondition(AnimatorConditionMode.If, 0, p); }
    static void ExitT(AnimatorState f, AnimatorState t, float e, float d)
    { var x = f.AddTransition(t); x.hasExitTime = true; x.exitTime = e; x.duration = d; }

    // =========================================================
    // ④ Build Full Scene
    // =========================================================
    [MenuItem("Tools/Arena Combat/\u2463 Build Full Scene")]
    public static void Step4_BuildScene()
    {
        int gL = LayerMask.NameToLayer("Ground");
        int pL = LayerMask.NameToLayer("Player");
        if (gL < 0 || pL < 0) { EditorUtility.DisplayDialog("Missing Layers","Run Step ① first.","OK"); return; }

        // Clean previous
        foreach (var n in new[]{ "Background","Ground","LeftPlatform","RightPlatform","TopPlatform",
            "SpawnPoint1","SpawnPoint2","Player1","Player2",
            "GameManager","ArenaManagerGO","AudioManagerGO","HUD_Canvas" })
        { var g = GameObject.Find(n); if (g) Object.DestroyImmediate(g); }

        // Background
        BuildBackground();

        // Platforms — ground top = -2.7
        MakePlatform("Ground",        new Vector3( 0f,   GROUND_Y,   0), new Vector2(26f, GROUND_H), gL, new Color32(110, 70, 30, 255));
        MakePlatform("LeftPlatform",  new Vector3(-5.5f, -1.2f,      0), new Vector2( 5f, 0.4f),     gL, new Color32( 60,115, 40, 255));
        MakePlatform("RightPlatform", new Vector3( 5.5f, -1.2f,      0), new Vector2( 5f, 0.4f),     gL, new Color32( 60,115, 40, 255));
        MakePlatform("TopPlatform",   new Vector3( 0f,    1.5f,      0), new Vector2( 4f, 0.4f),     gL, new Color32( 40, 85,135, 255));

        // Spawn points — SPAWN_Y = -2.3 (capsule bottom exactly on ground top)
        var sp1 = new GameObject("SpawnPoint1"); sp1.transform.position = new Vector3(-4f, SPAWN_Y, 0);
        var sp2 = new GameObject("SpawnPoint2"); sp2.transform.position = new Vector3( 4f, SPAWN_Y, 0);

        // Ensure Characters/Player folder exists
        if (!AssetDatabase.IsValidFolder("Assets/Characters"))
            AssetDatabase.CreateFolder("Assets", "Characters");
        if (!AssetDatabase.IsValidFolder("Assets/Characters/Player"))
            AssetDatabase.CreateFolder("Assets/Characters", "Player");

        // Build players and save as prefabs in Characters/Player
        var p1GO = BuildAndSavePlayer("Player1", 1, pL, gL, sp1.transform.position);
        var p2GO = BuildAndSavePlayer("Player2", 2, pL, gL, sp2.transform.position);

        // GameStateManager
        var gmGO = new GameObject("GameManager");
        var gsm  = gmGO.AddComponent<GameStateManager>();
        gsm.player1 = p1GO.GetComponent<PlayerController>();
        gsm.player2 = p2GO.GetComponent<PlayerController>();
        gsm.spawnP1 = sp1.transform; gsm.spawnP2 = sp2.transform;
        gsm.roundsToWin = 2; gsm.matchTimeSec = 99f;

        // ArenaManager
        var amGO = new GameObject("ArenaManagerGO");
        var am   = amGO.AddComponent<ArenaManager>();
        am.arenaCamera = Camera.main; am.player1Transform = p1GO.transform; am.player2Transform = p2GO.transform;
        am.deathZoneY = -9f; am.camMinSize = 5f; am.camMaxSize = 9f; am.camPadding = 4f; am.camSmoothing = 4f;

        // AudioManager
        var auGO = new GameObject("AudioManagerGO");
        var au   = auGO.AddComponent<AudioManager>();
        WireAudio(au);

        // Camera ortho size
        if (Camera.main != null) Camera.main.orthographicSize = 6f;

        // HUD — pass HealthManagers directly
        var hmP1 = p1GO.GetComponent<HealthManager>();
        var hmP2 = p2GO.GetComponent<HealthManager>();
        BuildHUD(gsm, hmP1, hmP2);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Scene Built!",
            "Press Ctrl+S to save, then Press Play!\n\n" +
            "P1: A/D = move | W or Space = jump (double jump!) | J = light | K = heavy\n" +
            "P2: Numpad 4/6 = move | Numpad 8 = jump | Numpad 0 = light | NumpadEnter = heavy",
            "Let's fight!");
    }

    // ── Background ────────────────────────────────────────────
    static void BuildBackground()
    {
        var root = new GameObject("Background");
        Sprite wideSpr = AssetDatabase.LoadAssetAtPath<Sprite>(BG + "background_wide.png");
        if (wideSpr != null)
        {
            var bg = new GameObject("BG_Layer");
            bg.transform.SetParent(root.transform);
            bg.transform.position = new Vector3(0, 1.5f, 0);
            var sr = bg.AddComponent<SpriteRenderer>();
            sr.sprite = wideSpr; sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(32f, 15f);
            sr.sortingLayerName = "Background"; sr.sortingOrder = 0;
            var pe = bg.AddComponent<ParallaxEffect>();
            pe.cam = Camera.main; pe.parallaxStrength = 0.15f;
        }
        else
        {
            // Fallback
            var bg = new GameObject("BG_Solid");
            bg.transform.SetParent(root.transform);
            bg.transform.position = new Vector3(0, 0, 10);
            var sr = bg.AddComponent<SpriteRenderer>();
            sr.sprite = MakeSolidSpr(new Color32(82,129,211,255));
            sr.drawMode = SpriteDrawMode.Tiled; sr.size = new Vector2(50f,30f);
            sr.sortingLayerName = "Background"; sr.sortingOrder = -10;
        }
    }

    // ── Platform ──────────────────────────────────────────────
    static void MakePlatform(string name, Vector3 pos, Vector2 size, int layer, Color32 col)
    {
        var go = new GameObject(name);
        go.layer = layer; go.tag = "Ground";
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeSolidSpr(col); sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = size; sr.sortingLayerName = "Foreground"; sr.sortingOrder = 0;
        var bc = go.AddComponent<BoxCollider2D>(); bc.size = size;
    }

    // ── Player + prefab ───────────────────────────────────────
    static GameObject BuildAndSavePlayer(string name, int idx, int pL, int gL, Vector3 pos)
    {
        var go = new GameObject(name);
        go.layer = pL; go.tag = "Player";
        go.transform.position   = pos;
        go.transform.localScale = new Vector3(idx == 1 ? PLAYER_SCALE : -PLAYER_SCALE, PLAYER_SCALE, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Characters"; sr.sortingOrder = idx;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // Capsule: local offset=(0, 0.4), size=(0.5, 1.2)
        // In world (scale=2): bottom = pos.y + (0.4-0.6)*2 = pos.y - 0.4
        // At SPAWN_Y=-2.3: bottom = -2.3-0.4 = -2.7 = GROUND_TOP ✓
        var cap = go.AddComponent<CapsuleCollider2D>();
        cap.size   = new Vector2(0.5f, CAP_HEIGHT);
        cap.offset = new Vector2(0f, CAP_OFFSET_Y);

        // Animator
        var anim = go.AddComponent<Animator>();
        string cName = idx == 1 ? "AC_Player1" : "AC_Player2";
        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ANIM + cName + ".controller");
        if (ctrl != null) anim.runtimeAnimatorController = ctrl;
        else Debug.LogWarning(cName + " not found — run Step ③ first.");

        // GroundCheck: local y = CAP_BOT_LOCAL - 0.05 = -0.25
        // World y at SPAWN_Y: -2.3 + (-0.25 * 2) = -2.8 (just below GROUND_TOP -2.7) ✓
        var gc = new GameObject("GroundCheck");
        gc.transform.SetParent(go.transform, false);
        gc.transform.localPosition = new Vector3(0f, CAP_BOT_LOCAL - 0.05f, 0f);

        // AttackPoint
        var ap = new GameObject("AttackPoint");
        ap.transform.SetParent(go.transform, false);
        ap.transform.localPosition = new Vector3(0.55f, 0.5f, 0f);

        var pc = go.AddComponent<PlayerController>();
        pc.playerIndex = idx; pc.walkSpeed = 8f; pc.runSpeed = 13f;
        pc.jumpForce = 16f; pc.fastFallForce = 22f; pc.groundCheckRadius = 0.22f;
        pc.groundCheck = gc.transform; pc.groundLayer = 1 << gL;

        go.AddComponent<HealthManager>();

        var cs = go.AddComponent<CombatSystem>();
        cs.attackPoint = ap.transform; cs.playerLayer = 1 << pL;
        cs.lightAttackRadius = 0.65f; cs.heavyAttackRadius = 1.05f;

        go.AddComponent<AnimationController>();
        go.AddComponent<DamageNumberSpawner>();

        // Save as prefab in Assets/Characters/Player/
        string prefabPath = CHAR + name + ".prefab";
        AssetDatabase.DeleteAsset(prefabPath);
        bool success;
        PrefabUtility.SaveAsPrefabAssetAndConnect(go, prefabPath, InteractionMode.AutomatedAction, out success);
        if (!success) Debug.LogWarning("Failed to save prefab: " + prefabPath);
        else Debug.Log($"Saved prefab: {prefabPath}");

        return go;
    }

    // =========================================================
    // HUD Canvas
    // =========================================================
    static void BuildHUD(GameStateManager gsm, HealthManager hmP1, HealthManager hmP2)
    {
        var cvGO = new GameObject("HUD_Canvas");
        var cv   = cvGO.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 20;
        var sc = cvGO.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
        cvGO.AddComponent<GraphicRaycaster>();

        // Load sprites
        Sprite sprHBBg  = LSpr(UI + "health_bar_bg.png");
        Sprite sprHBFil = LSpr(UI + "health_bar_fill.png");
        Sprite sprTimer = LSpr(UI + "timer_bg.png");
        Sprite sprName  = LSpr(UI + "nameplate_bg.png");
        Sprite sprPanel = LSpr(UI + "panel_dark.png");
        Sprite sprBtnN  = LSpr(UI + "button_normal.png");
        Sprite sprBtnH  = LSpr(UI + "button_hover.png");
        Sprite sprTop   = LSpr(UI + "hud_top_bar.png");

        // Top bar
        if (sprTop != null)
        {
            var tb = new GameObject("TopBar");
            tb.transform.SetParent(cvGO.transform, false);
            var i = tb.AddComponent<Image>(); i.sprite = sprTop; i.type = Image.Type.Sliced;
            var r = tb.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0,1); r.anchorMax = new Vector2(1,1);
            r.pivot = new Vector2(0.5f,1); r.sizeDelta = new Vector2(0, 95);
        }

        // P1 and P2 HUD blocks
        Slider hbP1; Image fillP1; TextMeshProUGUI scoreP1; TextMeshProUGUI hpP1;
        Slider hbP2; Image fillP2; TextMeshProUGUI scoreP2; TextMeshProUGUI hpP2;
        PlayerHUD(cvGO, false, "P1", sprHBBg, sprHBFil, sprName, out hbP1, out fillP1, out scoreP1, out hpP1);
        PlayerHUD(cvGO, true,  "P2", sprHBBg, sprHBFil, sprName, out hbP2, out fillP2, out scoreP2, out hpP2);

        // Timer
        var tiRoot = new GameObject("TimerRoot");
        tiRoot.transform.SetParent(cvGO.transform, false);
        var tiRT = tiRoot.AddComponent<RectTransform>();
        tiRT.anchorMin = new Vector2(0.5f,1); tiRT.anchorMax = new Vector2(0.5f,1);
        tiRT.pivot = new Vector2(0.5f,1); tiRT.anchoredPosition = new Vector2(0,-5); tiRT.sizeDelta = new Vector2(110,78);
        if (sprTimer != null) { var ti = tiRoot.AddComponent<Image>(); ti.sprite = sprTimer; ti.type = Image.Type.Sliced; }
        var timerTMP = ATMP(tiRoot,"TimerText","99", new Vector2(0,-4), new Vector2(110,70), 64f, Color.white, TextAlignmentOptions.Center);
        timerTMP.fontStyle = FontStyles.Bold;

        // Announcement
        var annGO = new GameObject("AnnouncementText");
        annGO.transform.SetParent(cvGO.transform, false);
        var annTMP = annGO.AddComponent<TextMeshProUGUI>();
        annTMP.text = ""; annTMP.fontSize = 72f; annTMP.color = Color.yellow;
        annTMP.alignment = TextAlignmentOptions.Center; annTMP.fontStyle = FontStyles.Bold;
        var annRT = annGO.GetComponent<RectTransform>();
        annRT.anchorMin = new Vector2(0.5f,0.5f); annRT.anchorMax = new Vector2(0.5f,0.5f);
        annRT.anchoredPosition = Vector2.zero; annRT.sizeDelta = new Vector2(1100,130);

        // Screen flash
        var flashGO = new GameObject("ScreenFlash");
        flashGO.transform.SetParent(cvGO.transform, false);
        var flashImg = flashGO.AddComponent<Image>(); flashImg.color = new Color(1,1,1,0);
        StretchRT(flashGO);
        var sf = cvGO.AddComponent<ScreenFlash>(); sf.flashImage = flashImg;

        // Match-over panel
        var panelGO = new GameObject("MatchOverPanel");
        panelGO.transform.SetParent(cvGO.transform, false);
        var panImg = panelGO.AddComponent<Image>();
        panImg.sprite = sprPanel; panImg.type = Image.Type.Sliced; panImg.color = new Color(0,0,0,0.88f);
        StretchRT(panelGO);
        var resultTMP = ATMP(panelGO,"ResultText","PLAYER 1\nWINS!",
            new Vector2(0,100), new Vector2(800,220), 96f, Color.yellow, TextAlignmentOptions.Center);
        resultTMP.fontStyle = FontStyles.Bold;
        ATMP(panelGO,"HintText",
            "P1: A/D move | W/Space = jump | J = light | K = heavy\n" +
            "P2: Numpad 4/6 = move | Numpad 8 = jump | Numpad 0 = light | NumpadEnter = heavy",
            new Vector2(0,-20), new Vector2(1000,70), 20f, Color.white, TextAlignmentOptions.Center);
        var rematch = MakeBtn(panelGO,"RematchBtn","▶ PLAY AGAIN", sprBtnN,sprBtnH, new Vector2(-160,-140), new Vector2(270,65));
        var quit    = MakeBtn(panelGO,"QuitBtn",   "✕ QUIT",       sprBtnN,sprBtnH, new Vector2(160,-140),  new Vector2(200,65));
        panelGO.SetActive(false);

        // Bottom hint
        var hintGO = new GameObject("BottomHint");
        hintGO.transform.SetParent(cvGO.transform, false);
        var ht = hintGO.AddComponent<TextMeshProUGUI>();
        ht.text = "P1: A/D  W/Space=Jump  J=Light  K=Heavy         P2: Num4/6  Num8=Jump  Num0=Light  NumEnter=Heavy";
        ht.fontSize = 16f; ht.color = new Color(1,1,1,0.4f); ht.alignment = TextAlignmentOptions.Center;
        var hRT = hintGO.GetComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0.5f,0); hRT.anchorMax = new Vector2(0.5f,0);
        hRT.pivot = new Vector2(0.5f,0); hRT.anchoredPosition = new Vector2(0,18); hRT.sizeDelta = new Vector2(1500,28);

        // UIManager — wire HealthManagers directly to fix Start() timing issue
        var uimGO = new GameObject("UIManager");
        uimGO.transform.SetParent(cvGO.transform, false);
        var uim = uimGO.AddComponent<UIManager>();
        uim.healthBarP1      = hbP1;      uim.healthBarP2  = hbP2;
        uim.healthFillP1     = fillP1;    uim.healthFillP2 = fillP2;
        uim.hpTextP1         = hpP1;      uim.hpTextP2     = hpP2;
        uim.healthManagerP1  = hmP1;      uim.healthManagerP2 = hmP2;   // ← direct refs fix health bars
        uim.timerText        = timerTMP;
        uim.announcementText = annTMP;
        uim.p1ScoreText      = scoreP1;   uim.p2ScoreText = scoreP2;
        uim.matchOverPanel   = panelGO;   uim.resultText  = resultTMP;
        uim.rematchButton    = rematch.GetComponent<Button>();
        uim.quitButton       = quit.GetComponent<Button>();
    }

    // Per-player HUD row
    static void PlayerHUD(GameObject cv, bool right, string id,
        Sprite bgSpr, Sprite fillSpr, Sprite nameSpr,
        out Slider slider, out Image fill, out TextMeshProUGUI score, out TextMeshProUGUI hpText)
    {
        float a = right ? 1f : 0f;
        float x = right ? -14f : 14f;

        // Name plate
        var nameGO = new GameObject(id+"_Name");
        nameGO.transform.SetParent(cv.transform, false);
        if (nameSpr != null) { var ni = nameGO.AddComponent<Image>(); ni.sprite = nameSpr; ni.type = Image.Type.Sliced; }
        var nRT = nameGO.GetComponent<RectTransform>();
        nRT.anchorMin = new Vector2(a,1); nRT.anchorMax = new Vector2(a,1);
        nRT.pivot = new Vector2(a,1); nRT.anchoredPosition = new Vector2(x,-8f); nRT.sizeDelta = new Vector2(230,30);
        ATMP(nameGO, id+"_Label", right ? "PLAYER  2" : "PLAYER  1",
            new Vector2(right ? -8f : 8f, 0), new Vector2(214,28), 20f,
            right ? new Color(1f,0.45f,0.45f) : new Color(0.45f,0.85f,1f),
            right ? TextAlignmentOptions.Right : TextAlignmentOptions.Left);

        // Health bar slider
        var hbGO = new GameObject(id+"_HealthBar");
        hbGO.transform.SetParent(cv.transform, false);
        var bg = hbGO.AddComponent<Image>();
        if (bgSpr != null) { bg.sprite = bgSpr; bg.type = Image.Type.Sliced; }
        else bg.color = new Color(0.15f, 0.03f, 0.03f);
        var sl = hbGO.AddComponent<Slider>();
        sl.minValue = 0; sl.maxValue = 1; sl.value = 1; sl.wholeNumbers = false;
        sl.direction = right ? Slider.Direction.RightToLeft : Slider.Direction.LeftToRight;
        sl.targetGraphic = bg;
        var hRT = hbGO.GetComponent<RectTransform>();
        hRT.anchorMin = new Vector2(a,1); hRT.anchorMax = new Vector2(a,1);
        hRT.pivot = new Vector2(a,1); hRT.anchoredPosition = new Vector2(x,-42f); hRT.sizeDelta = new Vector2(590,32);

        // Fill area
        var faGO = new GameObject("FillArea"); faGO.transform.SetParent(hbGO.transform, false);
        var faRT = faGO.AddComponent<RectTransform>();
        faRT.anchorMin = Vector2.zero; faRT.anchorMax = Vector2.one;
        faRT.offsetMin = new Vector2(3,3); faRT.offsetMax = new Vector2(-3,-3);
        var fillGO = new GameObject("Fill"); fillGO.transform.SetParent(faGO.transform, false);
        var fillImg = fillGO.AddComponent<Image>();
        if (fillSpr != null) { fillImg.sprite = fillSpr; fillImg.type = Image.Type.Sliced; }
        else fillImg.color = new Color(0.1f, 0.9f, 0.1f);
        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;
        sl.fillRect = fillRT;

        // HP number text
        hpText = ATMP(hbGO, id+"_HP", "100",
            new Vector2(right ? 8f : -8f, 0), new Vector2(80,28), 16f, Color.white,
            right ? TextAlignmentOptions.Left : TextAlignmentOptions.Right);

        // Score row
        var sGO = new GameObject(id+"_Score"); sGO.transform.SetParent(cv.transform, false);
        score = sGO.AddComponent<TextMeshProUGUI>();
        score.text = ""; score.fontSize = 22f; score.color = Color.yellow;
        score.alignment = right ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
        var sRT = sGO.GetComponent<RectTransform>();
        sRT.anchorMin = new Vector2(a,1); sRT.anchorMax = new Vector2(a,1);
        sRT.pivot = new Vector2(a,1); sRT.anchoredPosition = new Vector2(x,-78f); sRT.sizeDelta = new Vector2(130,26);

        slider = sl; fill = fillImg;
    }

    // UI helpers
    static TextMeshProUGUI ATMP(GameObject parent, string name, string text,
        Vector2 apos, Vector2 size, float fs, Color col, TextAlignmentOptions align)
    {
        var go = new GameObject(name); go.transform.SetParent(parent.transform, false);
        var t  = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = fs; t.color = col; t.alignment = align;
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = apos; rt.sizeDelta = size;
        return t;
    }

    static GameObject MakeBtn(GameObject parent, string name, string label,
        Sprite normSpr, Sprite hovSpr, Vector2 apos, Vector2 size)
    {
        var go = new GameObject(name); go.transform.SetParent(parent.transform, false);
        var img = go.AddComponent<Image>();
        if (normSpr != null) { img.sprite = normSpr; img.type = Image.Type.Sliced; }
        else img.color = new Color(0.12f, 0.12f, 0.22f);
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        if (hovSpr != null)
        {
            var ss = btn.spriteState; ss.highlightedSprite = hovSpr;
            btn.spriteState = ss; btn.transition = Selectable.Transition.SpriteSwap;
        }
        var rt = go.GetComponent<RectTransform>(); rt.anchoredPosition = apos; rt.sizeDelta = size;
        var lbl = ATMP(go,"Label",label,Vector2.zero,size,26f,Color.white,TextAlignmentOptions.Center);
        lbl.fontStyle = FontStyles.Bold;
        return go;
    }

    static void StretchRT(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Sprite LSpr(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null) Debug.LogWarning("Sprite not found: " + path);
        return s;
    }

    static Sprite MakeSolidSpr(Color32 c)
    {
        var t = new Texture2D(4,4,TextureFormat.RGBA32,false) { filterMode = FilterMode.Point };
        var p = new Color32[16]; for (int i=0;i<16;i++) p[i]=c; t.SetPixels32(p); t.Apply();
        return Sprite.Create(t, new Rect(0,0,4,4), Vector2.one*0.5f, 4f);
    }

    // =========================================================
    // ⑤ Physics2D Matrix
    // =========================================================
    [MenuItem("Tools/Arena Combat/\u2464 Fix Physics2D Layer Matrix")]
    public static void Step5_FixPhysics()
    {
        int g = LayerMask.NameToLayer("Ground");
        int p = LayerMask.NameToLayer("Player");
        if (g < 0 || p < 0) { Debug.LogError("Run Step ① first."); return; }
        Physics2D.IgnoreLayerCollision(p, g, false);
        Physics2D.IgnoreLayerCollision(p, p, true);
        Debug.Log("<color=lime>\u2713 Step 5: Players land on Ground, pass through each other.</color>");
    }

    // =========================================================
    // ⑥ Clean stale files
    // =========================================================
    [MenuItem("Tools/Arena Combat/\u2465 Clean Stale Files")]
    public static void Step6_Clean()
    {
        string[] stale = {
            "Assets/Animations/AC_Player.controller","Assets/Animations/player_idle.anim",
            "Assets/Characters/Player/AC_Player.controller","Assets/Characters/Player/player_idle.anim",
            "Assets/Characters/Player/player_run.anim","Assets/Characters/Player/player_walk.anim",
            "Assets/Characters/Player/PlayerinputActions.inputactions",
            "Assets/Characters/Player/Player.prefab",   // old prefab
            "Assets/InputSystem_Actions.inputactions","Assets/SETUP_GUIDE.md",
        };
        int n = 0;
        foreach (string f in stale)
            if (AssetDatabase.LoadAssetAtPath<Object>(f) != null) { AssetDatabase.DeleteAsset(f); n++; }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"<color=lime>\u2713 Step 6: Removed {n} stale file(s).</color>");
    }

    // Audio
    static void WireAudio(AudioManager am)
    {
        am.lightSwingSFX    = LAC(AUDIO+"SFX/12_Player_Movement_SFX/56_Attack_03.wav");
        am.heavySwingSFX    = LAC(AUDIO+"SFX/10_Battle_SFX/22_Slash_04.wav");
        am.lightHitSFX      = LAC(AUDIO+"SFX/12_Player_Movement_SFX/61_Hit_03.wav");
        am.heavyHitSFX      = LAC(AUDIO+"SFX/10_Battle_SFX/15_Impact_flesh_02.wav");
        am.deathSFX         = LAC(AUDIO+"SFX/10_Battle_SFX/69_Enemy_death_01.wav");
        am.jumpSFX          = LAC(AUDIO+"SFX/12_Player_Movement_SFX/30_Jump_03.wav");
        am.landSFX          = LAC(AUDIO+"SFX/12_Player_Movement_SFX/45_Landing_01.wav");
        am.roundStartSFX    = LAC(AUDIO+"SFX/10_Battle_SFX/55_Encounter_02.wav");
        am.uiConfirmSFX     = LAC(AUDIO+"SFX/10_UI_Menu_SFX/013_Confirm_03.wav");
        am.uiDeclineSFX     = LAC(AUDIO+"SFX/10_UI_Menu_SFX/029_Decline_09.wav");
        am.battleMusicIntro = LAC(AUDIO+"Music/10 Battle1 (8bit style)/Assets_for_Unity/8bit-Battle01_intro.ogg");
        am.battleMusicLoop  = LAC(AUDIO+"Music/10 Battle1 (8bit style)/Assets_for_Unity/8bit-Battle01_loop.ogg");
    }
    static AudioClip LAC(string p)
    {
        var c = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
        if (c == null) Debug.LogWarning("Audio missing: " + p); return c;
    }
}
#endif
