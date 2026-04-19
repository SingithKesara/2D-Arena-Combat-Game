using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class AutoSetup : EditorWindow
{
    private const string ScenePath = "Assets/Scenes/GameplayScene.unity";
    private const string AnimFolder = "Assets/Animations";

    private const string P1IdleSheet  = "Assets/Art/freeknight/Colour1/Outline/120x80_PNGSheets/_Idle.png";
    private const string P1RunSheet   = "Assets/Art/freeknight/Colour1/Outline/120x80_PNGSheets/_Run.png";
    private const string P1JumpSheet  = "Assets/Art/freeknight/Colour1/Outline/120x80_PNGSheets/_Jump.png";
    private const string P1FallSheet  = "Assets/Art/freeknight/Colour1/Outline/120x80_PNGSheets/_Fall.png";
    private const string P1LightSheet = "Assets/Art/freeknight/Colour1/Outline/120x80_PNGSheets/_Attack.png";
    private const string P1HeavySheet = "Assets/Art/freeknight/Colour1/Outline/120x80_PNGSheets/_Attack2.png";
    private const string P1HitSheet   = "Assets/Art/freeknight/Colour1/Outline/120x80_PNGSheets/_Hit.png";
    private const string P1DeathSheet = "Assets/Art/freeknight/Colour1/Outline/120x80_PNGSheets/_Death.png";

    private const string P2IdleSheet  = "Assets/Art/freeknight/Colour2/Outline/120x80_PNGSheets/_Idle.png";
    private const string P2RunSheet   = "Assets/Art/freeknight/Colour2/Outline/120x80_PNGSheets/_Run.png";
    private const string P2JumpSheet  = "Assets/Art/freeknight/Colour2/Outline/120x80_PNGSheets/_Jump.png";
    private const string P2FallSheet  = "Assets/Art/freeknight/Colour2/Outline/120x80_PNGSheets/_Fall.png";
    private const string P2LightSheet = "Assets/Art/freeknight/Colour2/Outline/120x80_PNGSheets/_Attack.png";
    private const string P2HeavySheet = "Assets/Art/freeknight/Colour2/Outline/120x80_PNGSheets/_Attack2.png";
    private const string P2HitSheet   = "Assets/Art/freeknight/Colour2/Outline/120x80_PNGSheets/_Hit.png";
    private const string P2DeathSheet = "Assets/Art/freeknight/Colour2/Outline/120x80_PNGSheets/_Death.png";

    // Visual-only downward nudge so the rendered sprite sits on the ground better.
    private const float VisualYOffset = -0.22f;

    [MenuItem("Tools/Arena Combat/2 - Slice Sprite Sheets")]
    public static void SliceSprites()
    {
        EnsureFolder(AnimFolder);

        string[] files = Directory.GetFiles("Assets", "*.png", SearchOption.AllDirectories);

        foreach (string file in files)
        {
            string unityPath = file.Replace("\\", "/");
            TextureImporter tex = AssetImporter.GetAtPath(unityPath) as TextureImporter;
            if (tex == null) continue;

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(unityPath);
            if (texture == null) continue;

            bool isKnightSheet = unityPath.Contains("120x80_PNGSheets");
            int frameWidth = isKnightSheet ? 120 : 64;
            int frameHeight = isKnightSheet ? 80 : 64;

            if (texture.width < frameWidth || texture.height < frameHeight)
                continue;

            tex.textureType = TextureImporterType.Sprite;
            tex.spriteImportMode = SpriteImportMode.Multiple;
            tex.spritePixelsPerUnit = 32f;
            tex.filterMode = FilterMode.Point;
            tex.mipmapEnabled = false;
            tex.alphaIsTransparency = true;
            tex.textureCompression = TextureImporterCompression.Uncompressed;

            int cols = texture.width / frameWidth;
            int rows = texture.height / frameHeight;

            var factory = new SpriteDataProviderFactories();
            factory.Init();

            var dataProvider = factory.GetSpriteEditorDataProviderFromObject(tex);
            if (dataProvider == null) continue;

            dataProvider.InitSpriteEditorDataProvider();

            List<SpriteRect> rects = new List<SpriteRect>();

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    rects.Add(new SpriteRect
                    {
                        name = $"{Path.GetFileNameWithoutExtension(unityPath)}_{x}_{y}",
                        rect = new Rect(x * frameWidth, y * frameHeight, frameWidth, frameHeight),
                        pivot = isKnightSheet ? new Vector2(0.5f, 0.0f) : new Vector2(0.5f, 0.5f),
                        alignment = SpriteAlignment.Custom
                    });
                }
            }

            dataProvider.SetSpriteRects(rects.ToArray());
            dataProvider.Apply();
            tex.SaveAndReimport();
        }

        AssetDatabase.Refresh();
        Debug.Log("Step 2 done: sprite sheets sliced.");
    }

    [MenuItem("Tools/Arena Combat/3 - Build Animator Controllers")]
    public static void BuildAnimatorControllers()
    {
        EnsureFolder(AnimFolder);

        BuildOneController(
            "AC_Player1",
            P1IdleSheet, P1RunSheet, P1JumpSheet, P1FallSheet, P1LightSheet, P1HeavySheet, P1HitSheet, P1DeathSheet);

        BuildOneController(
            "AC_Player2",
            P2IdleSheet, P2RunSheet, P2JumpSheet, P2FallSheet, P2LightSheet, P2HeavySheet, P2HitSheet, P2DeathSheet);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Step 3 done: animator controllers rebuilt.");
    }

    [MenuItem("Tools/Arena Combat/4 - Build Gameplay Scene")]
    public static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        int groundLayer = LayerMask.NameToLayer("Ground");
        int playerLayer = LayerMask.NameToLayer("Player");

        if (groundLayer == -1)
        {
            Debug.LogError("Create a layer named Ground first.");
            return;
        }

        if (playerLayer == -1)
        {
            Debug.LogError("Create a layer named Player first.");
            return;
        }

        SetupCamera();
        CreateEventSystemIfMissing();
        CreateHitSystems();

        CreateSolidPlatform(
            "Ground",
            new Vector3(0f, -2.5f, 0f),
            new Vector2(20f, 1f),
            groundLayer,
            new Color(0.35f, 0.22f, 0.10f)
        );

        CreateOneWayPlatform(
            "LeftPlatform",
            new Vector3(-5.5f, 1.4f, 0f),
            new Vector2(5f, 0.4f),
            groundLayer,
            new Color(0.45f, 0.28f, 0.14f)
        );

        CreateOneWayPlatform(
            "RightPlatform",
            new Vector3(5.5f, 1.4f, 0f),
            new Vector2(5f, 0.4f),
            groundLayer,
            new Color(0.45f, 0.28f, 0.14f)
        );

        CreateOneWayPlatform(
            "TopPlatform",
            new Vector3(0f, 3.6f, 0f),
            new Vector2(4f, 0.4f),
            groundLayer,
            new Color(0.48f, 0.30f, 0.15f)
        );

        GameObject p1 = CreatePlayer("Player1", new Vector3(-4f, -1.6f, 0f), 1, playerLayer);
        GameObject p2 = CreatePlayer("Player2", new Vector3(4f, -1.6f, 0f), 2, playerLayer);

        GameObject spawn1 = new GameObject("SpawnPoint1");
        spawn1.transform.position = new Vector3(-4f, -1.6f, 0f);

        GameObject spawn2 = new GameObject("SpawnPoint2");
        spawn2.transform.position = new Vector3(4f, -1.6f, 0f);

        GameObject gameManagerGO = new GameObject("GameManager");
        GameStateManager gsm = gameManagerGO.AddComponent<GameStateManager>();
        gsm.player1 = p1.GetComponent<PlayerController>();
        gsm.player2 = p2.GetComponent<PlayerController>();
        gsm.spawnP1 = spawn1.transform;
        gsm.spawnP2 = spawn2.transform;
        gsm.roundsToWin = 2;
        gsm.matchTimeSec = 99f;

        BuildHUD(gsm, p1.GetComponent<HealthManager>(), p2.GetComponent<HealthManager>());

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("Step 4 done: gameplay scene built.");
    }

    private static void BuildOneController(
        string controllerName,
        string idleSheet, string runSheet, string jumpSheet, string fallSheet,
        string lightSheet, string heavySheet, string hitSheet, string deathSheet)
    {
        string controllerPath = $"{AnimFolder}/{controllerName}.controller";
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

        AnimationClip idle = CreateClip($"{AnimFolder}/{controllerName}_Idle.anim", idleSheet, 8f, true);
        AnimationClip run = CreateClip($"{AnimFolder}/{controllerName}_Run.anim", runSheet, 12f, true);
        AnimationClip jump = CreateClip($"{AnimFolder}/{controllerName}_Jump.anim", jumpSheet, 10f, false);
        AnimationClip fall = CreateClip($"{AnimFolder}/{controllerName}_Fall.anim", fallSheet, 10f, true);
        AnimationClip light = CreateClip($"{AnimFolder}/{controllerName}_Light.anim", lightSheet, 16f, false);
        AnimationClip heavy = CreateClip($"{AnimFolder}/{controllerName}_Heavy.anim", heavySheet, 12f, false);
        AnimationClip hit = CreateClip($"{AnimFolder}/{controllerName}_Hit.anim", hitSheet, 12f, false);
        AnimationClip death = CreateClip($"{AnimFolder}/{controllerName}_Death.anim", deathSheet, 8f, false);

        AnimatorState idleState = sm.AddState("Idle");
        AnimatorState runState = sm.AddState("Run");
        AnimatorState jumpState = sm.AddState("Jump");
        AnimatorState fallState = sm.AddState("Fall");
        AnimatorState lightState = sm.AddState("Light");
        AnimatorState heavyState = sm.AddState("Heavy");
        AnimatorState hitState = sm.AddState("Hit");
        AnimatorState deathState = sm.AddState("Death");

        idleState.motion = idle;
        runState.motion = run;
        jumpState.motion = jump;
        fallState.motion = fall;
        lightState.motion = light;
        heavyState.motion = heavy;
        hitState.motion = hit;
        deathState.motion = death;

        sm.defaultState = idleState;

        var idleToRun = idleState.AddTransition(runState);
        idleToRun.hasExitTime = false;
        idleToRun.duration = 0.05f;
        idleToRun.AddCondition(AnimatorConditionMode.If, 0, "isMoving");

        var runToIdle = runState.AddTransition(idleState);
        runToIdle.hasExitTime = false;
        runToIdle.duration = 0.05f;
        runToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "isMoving");

        var idleToJump = idleState.AddTransition(jumpState);
        idleToJump.hasExitTime = false;
        idleToJump.duration = 0.05f;
        idleToJump.AddCondition(AnimatorConditionMode.If, 0, "jump");

        var runToJump = runState.AddTransition(jumpState);
        runToJump.hasExitTime = false;
        runToJump.duration = 0.05f;
        runToJump.AddCondition(AnimatorConditionMode.If, 0, "jump");

        var jumpToFall = jumpState.AddTransition(fallState);
        jumpToFall.hasExitTime = false;
        jumpToFall.duration = 0.05f;
        jumpToFall.AddCondition(AnimatorConditionMode.Less, 0f, "velocityY");

        var fallToIdle = fallState.AddTransition(idleState);
        fallToIdle.hasExitTime = false;
        fallToIdle.duration = 0.05f;
        fallToIdle.AddCondition(AnimatorConditionMode.If, 0, "isGrounded");
        fallToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "isMoving");

        var fallToRun = fallState.AddTransition(runState);
        fallToRun.hasExitTime = false;
        fallToRun.duration = 0.05f;
        fallToRun.AddCondition(AnimatorConditionMode.If, 0, "isGrounded");
        fallToRun.AddCondition(AnimatorConditionMode.If, 0, "isMoving");

        var anyToLight = sm.AddAnyStateTransition(lightState);
        anyToLight.hasExitTime = false;
        anyToLight.duration = 0.03f;
        anyToLight.canTransitionToSelf = false;
        anyToLight.AddCondition(AnimatorConditionMode.If, 0, "lightAttack");

        var lightToIdle = lightState.AddTransition(idleState);
        lightToIdle.hasExitTime = true;
        lightToIdle.exitTime = 0.95f;
        lightToIdle.duration = 0.05f;

        var anyToHeavy = sm.AddAnyStateTransition(heavyState);
        anyToHeavy.hasExitTime = false;
        anyToHeavy.duration = 0.03f;
        anyToHeavy.canTransitionToSelf = false;
        anyToHeavy.AddCondition(AnimatorConditionMode.If, 0, "heavyAttack");

        var heavyToIdle = heavyState.AddTransition(idleState);
        heavyToIdle.hasExitTime = true;
        heavyToIdle.exitTime = 0.95f;
        heavyToIdle.duration = 0.05f;

        var anyToHit = sm.AddAnyStateTransition(hitState);
        anyToHit.hasExitTime = false;
        anyToHit.duration = 0.02f;
        anyToHit.canTransitionToSelf = false;
        anyToHit.AddCondition(AnimatorConditionMode.If, 0, "hit");

        var hitToIdle = hitState.AddTransition(idleState);
        hitToIdle.hasExitTime = true;
        hitToIdle.exitTime = 0.95f;
        hitToIdle.duration = 0.05f;

        var anyToDeath = sm.AddAnyStateTransition(deathState);
        anyToDeath.hasExitTime = false;
        anyToDeath.duration = 0.02f;
        anyToDeath.canTransitionToSelf = false;
        anyToDeath.AddCondition(AnimatorConditionMode.If, 0, "death");
    }

    private static AnimationClip CreateClip(string clipPath, string sheetPath, float fps, bool loop)
    {
        AssetDatabase.DeleteAsset(clipPath);

        AnimationClip clip = new AnimationClip();
        clip.frameRate = fps;

        EditorCurveBinding binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "Visual",
            propertyName = "m_Sprite"
        };

        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(sheetPath)
            .OfType<Sprite>()
            .OrderBy(s => s.name)
            .ToArray();

        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
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

    private static void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.80f, 1.00f, 1f);

        if (cam.GetComponent<CameraShake>() == null)
            cam.gameObject.AddComponent<CameraShake>();
    }

    private static void CreateSolidPlatform(string name, Vector3 position, Vector2 size, int layer, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.position = position;
        go.layer = layer;
        go.tag = "Ground";

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSolidSprite();
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        sr.color = color;

        BoxCollider2D bc = go.AddComponent<BoxCollider2D>();
        bc.size = size;
    }

    private static void CreateOneWayPlatform(string name, Vector3 position, Vector2 size, int layer, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.position = position;
        go.layer = layer;
        go.tag = "Ground";

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSolidSprite();
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        sr.color = color;

        BoxCollider2D bc = go.AddComponent<BoxCollider2D>();
        bc.size = size;
        bc.usedByEffector = true;

        PlatformEffector2D effector = go.AddComponent<PlatformEffector2D>();
        effector.useOneWay = true;
        effector.surfaceArc = 180f;
        effector.sideArc = 0f;
        effector.rotationalOffset = 0f;
    }

    private static GameObject CreatePlayer(string name, Vector3 position, int playerIndex, int playerLayer)
    {
        GameObject go = new GameObject(name);
        go.transform.position = position;
        go.layer = playerLayer;
        go.tag = "Player";

        go.transform.localScale = playerIndex == 1
            ? new Vector3(2f, 2f, 1f)
            : new Vector3(-2f, 2f, 1f);

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        CapsuleCollider2D col = go.AddComponent<CapsuleCollider2D>();
        col.size = new Vector2(0.5f, 0.95f);
        col.offset = new Vector2(0f, -0.02f);

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        visual.transform.localPosition = new Vector3(0f, VisualYOffset, 0f);

        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Characters";
        sr.sortingOrder = playerIndex;

        Animator anim = go.AddComponent<Animator>();
        string controllerPath = playerIndex == 1
            ? $"{AnimFolder}/AC_Player1.controller"
            : $"{AnimFolder}/AC_Player2.controller";
        RuntimeAnimatorController controller =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
        if (controller != null) anim.runtimeAnimatorController = controller;

        GameObject groundCheck = new GameObject("GroundCheck");
        groundCheck.transform.SetParent(go.transform, false);
        groundCheck.transform.localPosition = new Vector3(0f, -0.50f, 0f);

        GameObject attackPoint = new GameObject("AttackPoint");
        attackPoint.transform.SetParent(go.transform, false);
        attackPoint.transform.localPosition = new Vector3(0.75f, 0.10f, 0f);

        PlayerController pc = go.AddComponent<PlayerController>();
        pc.playerIndex = playerIndex;
        pc.walkSpeed = 8f;
        pc.runSpeed = 13f;
        pc.jumpForce = 16f;
        pc.fastFallForce = 22f;
        pc.maxFallSpeed = -28f;
        pc.groundCheck = groundCheck.transform;
        pc.groundCheckRadius = 0.14f;
        pc.groundLayer = LayerMask.GetMask("Ground");

        HealthManager hm = go.AddComponent<HealthManager>();
        hm.maxHealth = 100;
        hm.iFrameDuration = 0.25f;

        CombatSystem cs = go.AddComponent<CombatSystem>();
        cs.attackPoint = attackPoint.transform;
        cs.playerLayer = LayerMask.GetMask("Player");

        go.AddComponent<AnimationController>();

        if (go.GetComponent<DamageNumberSpawner>() == null)
            go.AddComponent<DamageNumberSpawner>();

        return go;
    }

    private static void CreateHitSystems()
    {
        GameObject go = new GameObject("HitSystems");
        go.AddComponent<HitStop>();
        go.AddComponent<HitEffect>();
    }

    private static void BuildHUD(GameStateManager gsm, HealthManager hmP1, HealthManager hmP2)
    {
        GameObject canvasGO = new GameObject("HUD_Canvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        Image fillP1;
        TextMeshProUGUI hpP1;
        TextMeshProUGUI scoreP1;
        CreatePlayerHUD(canvasGO.transform, true, out fillP1, out hpP1, out scoreP1);

        Image fillP2;
        TextMeshProUGUI hpP2;
        TextMeshProUGUI scoreP2;
        CreatePlayerHUD(canvasGO.transform, false, out fillP2, out hpP2, out scoreP2);

        TextMeshProUGUI timerText = CreateTMP(
            canvasGO.transform,
            "TimerText",
            "99",
            new Vector2(160f, 80f),
            56f,
            Color.white,
            TextAlignmentOptions.Center
        );
        SetTopCenter(timerText.rectTransform, -35f);

        TextMeshProUGUI announcementText = CreateTMP(
            canvasGO.transform,
            "AnnouncementText",
            "",
            new Vector2(1100f, 140f),
            72f,
            Color.yellow,
            TextAlignmentOptions.Center
        );
        SetMiddleCenter(announcementText.rectTransform, 0f, 120f);

        GameObject matchPanel = CreatePanel(canvasGO.transform, "MatchOverPanel", new Color(0f, 0f, 0f, 0.82f));
        matchPanel.SetActive(false);

        TextMeshProUGUI resultText = CreateTMP(
            matchPanel.transform,
            "ResultText",
            "PLAYER 1\nWINS!",
            new Vector2(900f, 220f),
            84f,
            Color.yellow,
            TextAlignmentOptions.Center
        );
        SetMiddleCenter(resultText.rectTransform, 0f, 110f);

        Button rematchButton = CreateButton(matchPanel.transform, "RematchButton", "PLAY AGAIN", new Vector2(-220f, -120f));
        Button menuButton = CreateButton(matchPanel.transform, "MenuButton", "MAIN MENU", new Vector2(0f, -120f));
        Button quitButton = CreateButton(matchPanel.transform, "QuitButton", "QUIT", new Vector2(220f, -120f));

        GameObject uiGO = new GameObject("UIManager");
        uiGO.transform.SetParent(canvasGO.transform, false);
        UIManager ui = uiGO.AddComponent<UIManager>();

        ui.healthFillP1 = fillP1;
        ui.healthFillP2 = fillP2;
        ui.hpTextP1 = hpP1;
        ui.hpTextP2 = hpP2;
        ui.healthManagerP1 = hmP1;
        ui.healthManagerP2 = hmP2;
        ui.timerText = timerText;
        ui.announcementText = announcementText;
        ui.p1ScoreText = scoreP1;
        ui.p2ScoreText = scoreP2;
        ui.matchOverPanel = matchPanel;
        ui.resultText = resultText;
        ui.rematchButton = rematchButton;
        ui.menuButton = menuButton;
        ui.quitButton = quitButton;
    }

    private static void CreatePlayerHUD(
        Transform parent,
        bool isLeft,
        out Image fillImage,
        out TextMeshProUGUI hpText,
        out TextMeshProUGUI scoreText)
    {
        float xAnchor = isLeft ? 0f : 1f;
        float xPos = isLeft ? 25f : -25f;

        GameObject nameGO = new GameObject(isLeft ? "P1_Name" : "P2_Name");
        nameGO.transform.SetParent(parent, false);
        Image nameBG = nameGO.AddComponent<Image>();
        nameBG.color = new Color(0f, 0f, 0f, 0.55f);
        RectTransform nameRT = nameGO.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(xAnchor, 1f);
        nameRT.anchorMax = new Vector2(xAnchor, 1f);
        nameRT.pivot = new Vector2(xAnchor, 1f);
        nameRT.anchoredPosition = new Vector2(xPos, -18f);
        nameRT.sizeDelta = new Vector2(240f, 34f);

        TextMeshProUGUI nameText = CreateTMP(
            nameGO.transform,
            "Label",
            isLeft ? "PLAYER 1" : "PLAYER 2",
            new Vector2(220f, 28f),
            22f,
            isLeft ? new Color(0.40f, 0.90f, 1f) : new Color(1f, 0.45f, 0.45f),
            isLeft ? TextAlignmentOptions.Left : TextAlignmentOptions.Right
        );
        Stretch(nameText.rectTransform, 8f, 8f);

        GameObject barGO = new GameObject(isLeft ? "P1_HealthBar" : "P2_HealthBar");
        barGO.transform.SetParent(parent, false);
        Image barBG = barGO.AddComponent<Image>();
        barBG.color = new Color(0.15f, 0.02f, 0.02f, 0.95f);
        RectTransform barRT = barGO.GetComponent<RectTransform>();
        barRT.anchorMin = new Vector2(xAnchor, 1f);
        barRT.anchorMax = new Vector2(xAnchor, 1f);
        barRT.pivot = new Vector2(xAnchor, 1f);
        barRT.anchoredPosition = new Vector2(xPos, -60f);
        barRT.sizeDelta = new Vector2(590f, 34f);

        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(barGO.transform, false);
        fillImage = fillGO.AddComponent<Image>();
        fillImage.color = new Color(0.1f, 0.9f, 0.1f);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = isLeft ? 0 : 1;
        fillImage.fillAmount = 1f;
        RectTransform fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = new Vector2(3f, 3f);
        fillRT.offsetMax = new Vector2(-3f, -3f);

        hpText = CreateTMP(
            barGO.transform,
            "HPText",
            "100",
            new Vector2(80f, 28f),
            18f,
            Color.white,
            isLeft ? TextAlignmentOptions.Right : TextAlignmentOptions.Left
        );
        RectTransform hpRT = hpText.rectTransform;
        hpRT.anchorMin = isLeft ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
        hpRT.anchorMax = isLeft ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
        hpRT.pivot = isLeft ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
        hpRT.anchoredPosition = isLeft ? new Vector2(-8f, 0f) : new Vector2(8f, 0f);
        hpRT.sizeDelta = new Vector2(80f, 28f);

        GameObject scoreGO = new GameObject(isLeft ? "P1_Score" : "P2_Score");
        scoreGO.transform.SetParent(parent, false);
        scoreText = scoreGO.AddComponent<TextMeshProUGUI>();
        scoreText.text = "○○";
        scoreText.fontSize = 26f;
        scoreText.color = Color.yellow;
        scoreText.alignment = isLeft ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
        RectTransform scoreRT = scoreGO.GetComponent<RectTransform>();
        scoreRT.anchorMin = new Vector2(xAnchor, 1f);
        scoreRT.anchorMax = new Vector2(xAnchor, 1f);
        scoreRT.pivot = new Vector2(xAnchor, 1f);
        scoreRT.anchoredPosition = new Vector2(xPos, -102f);
        scoreRT.sizeDelta = new Vector2(140f, 28f);
    }

    private static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        Image img = panel.AddComponent<Image>();
        img.color = color;

        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return panel;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.12f, 0.12f, 0.25f, 0.95f);

        Button btn = go.AddComponent<Button>();
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = new Vector2(220f, 58f);

        TextMeshProUGUI text = CreateTMP(go.transform, "Label", label, new Vector2(200f, 40f), 24f, Color.white, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 0f, 0f);

        return btn;
    }

    private static TextMeshProUGUI CreateTMP(
        Transform parent,
        string name,
        string text,
        Vector2 size,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.rectTransform.sizeDelta = size;

        return tmp;
    }

    private static void SetTopCenter(RectTransform rt, float y)
    {
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
    }

    private static void SetMiddleCenter(RectTransform rt, float x, float y)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
    }

    private static void Stretch(RectTransform rt, float padX, float padY)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padX, padY);
        rt.offsetMax = new Vector2(-padX, -padY);
    }

    private static void CreateEventSystemIfMissing()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null)
            return;

        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    private static Sprite CreateSolidSprite()
    {
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 1f);
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