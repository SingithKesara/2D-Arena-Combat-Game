#if UNITY_EDITOR
// ============================================================
//  AutoSetup.cs  -  Tools -> Arena Combat -> run 1 2 3 4 5 6 7
//  Unity 6 | New Input System | Fixed:
//    - ASCII-only button text (no Unicode arrows missing from font)
//    - EventSystem added to all canvas scenes
//    - Tileset-based platforms from freecutetileset
//    - Correct gameplay scene name "Gameplayscene"
//    - AC_Player_OLD renamed warning fixed
// ============================================================
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public static class AutoSetup
{
    const string K1    = "Assets/Art/freeknight/Colour1/Outline/120x80_PNGSheets/";
    const string K2    = "Assets/Art/freeknight/Colour2/Outline/120x80_PNGSheets/";
    const string ANIM  = "Assets/Animations/";
    const string UI    = "Assets/UI/";
    const string BG    = "Assets/Art/freecutetileset/";
    const string AUDIO = "Assets/Audio/";
    const string CHAR  = "Assets/Characters/Player/";
    const string SCENES= "Assets/Scenes/";
    // Gameplay scene name must match build settings exactly
    const string GAMEPLAY_SCENE = "Gameplayscene";

    const float GROUND_Y      = -3f;
    const float GROUND_H      = 0.6f;
    const float GROUND_TOP    = GROUND_Y + GROUND_H / 2f;   // -2.7
    const float CAP_OFFSET_Y  = 0.4f;
    const float CAP_HEIGHT    = 1.2f;
    const float CAP_BOT_LOCAL = CAP_OFFSET_Y - CAP_HEIGHT / 2f; // -0.2
    const float PLAYER_SCALE  = 2f;
    const float SPAWN_Y       = GROUND_TOP - CAP_BOT_LOCAL * PLAYER_SCALE; // -2.3

    // =========================================================
    // 1 - Layers and Tags
    // =========================================================
    [MenuItem("Tools/Arena Combat/1 - Setup Layers and Tags")]
    public static void Step1()
    {
        var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (asset == null || asset.Length == 0) { Debug.LogError("Cannot open TagManager"); return; }
        var so = new SerializedObject(asset[0]);
        AddTag(so, "Ground");
        SetLayer(so, 6, "Ground");
        SetLayer(so, 7, "Player");
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset[0]);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();

        // Also delete the misnamed AC_Player_OLD if present
        string old = ANIM + "AC_Player_OLD.controller";
        if (AssetDatabase.LoadAssetAtPath<Object>(old) != null) AssetDatabase.DeleteAsset(old);

        Debug.Log("<color=lime>Step 1 done: Ground=Layer6, Player=Layer7</color>");
    }

    static void AddTag(SerializedObject so, string tag)
    {
        if (UnityEditorInternal.InternalEditorUtility.tags.Contains(tag)) return;
        var arr = so.FindProperty("tags");
        for (int i = 0; i < arr.arraySize; i++) if (arr.GetArrayElementAtIndex(i).stringValue == tag) return;
        arr.arraySize++;
        arr.GetArrayElementAtIndex(arr.arraySize - 1).stringValue = tag;
    }
    static void SetLayer(SerializedObject so, int idx, string name)
    {
        var l = so.FindProperty("layers");
        if (l.arraySize <= idx) l.arraySize = idx + 1;
        l.GetArrayElementAtIndex(idx).stringValue = name;
    }

    // =========================================================
    // 2 - Slice Sprite Sheets
    // =========================================================
    [MenuItem("Tools/Arena Combat/2 - Slice Sprite Sheets")]
    public static void Step2()
    {
        var sheets = new Dictionary<string, int>
        {
            {"_Idle",10},{"_Run",10},{"_Death",10},
            {"_Attack",4},{"_Attack2",6},{"_Hit",1},
            {"_Jump",3},{"_Fall",3},
        };
        int n = 0;
        foreach (var kv in sheets)
        {
            if (Slice(K1+kv.Key+".png", kv.Key, "k1", kv.Value)) n++;
            if (Slice(K2+kv.Key+".png", kv.Key, "k2", kv.Value)) n++;
        }

        // Background images
        ImportSingle(BG+"background_wide.png", 16f, FilterMode.Point);
        ImportSingle(BG+"BG1.png", 16f, FilterMode.Point);
        ImportSingle(BG+"BG2.png", 16f, FilterMode.Point);
        ImportSingle(BG+"BG3.png", 16f, FilterMode.Point);

        // Tileset as sprite for tiling platforms
        ImportSingle(BG+"Tileset.png", 16f, FilterMode.Point);
        ImportSingle(BG+"ground_platform.png",   16f, FilterMode.Point);
        ImportSingle(BG+"floating_platform.png", 16f, FilterMode.Point);
        ImportSingle(BG+"ground_top_tile.png",   16f, FilterMode.Point);
        ImportSingle(BG+"ground_fill_tile.png",  16f, FilterMode.Point);
        ImportSingle(BG+"platform_top_tile.png", 16f, FilterMode.Point);
        ImportSingle(BG+"grass_tile.png",     16f, FilterMode.Point);
        ImportSingle(BG+"platform_tile.png",  16f, FilterMode.Point);

        // UI sprites
        foreach (var f in new[]{"health_bar_bg.png","health_bar_fill.png","timer_bg.png",
            "nameplate_bg.png","panel_dark.png","button_normal.png","button_hover.png",
            "hud_top_bar.png","menu_bg.png","title_plate.png","menu_btn.png",
            "menu_btn_hover.png","divider.png","win_dot_filled.png","win_dot_empty.png"})
            ImportSingle(UI+f, 1f, FilterMode.Bilinear);

        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"<color=lime>Step 2 done: {n} knight sheets sliced.</color>");
    }

    static bool Slice(string path, string key, string prefix, int frames)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) { Debug.LogWarning("Missing: "+path); return false; }
        ti.textureType=TextureImporterType.Sprite; ti.spriteImportMode=SpriteImportMode.Multiple;
        ti.spritePixelsPerUnit=32f; ti.filterMode=FilterMode.Point; ti.mipmapEnabled=false;
        ti.textureCompression=TextureImporterCompression.Uncompressed; ti.alphaIsTransparency=true;
        EditorUtility.SetDirty(ti); ti.SaveAndReimport();
        var fac=new SpriteDataProviderFactories(); fac.Init();
        var dp=fac.GetSpriteEditorDataProviderFromObject(ti);
        if(dp==null) return false;
        dp.InitSpriteEditorDataProvider();
        var rects=new SpriteRect[frames];
        for(int i=0;i<frames;i++) rects[i]=new SpriteRect{
            name=prefix+key+"_"+i, rect=new Rect(i*120,0,120,80),
            pivot=new Vector2(0.5f,0.2f), alignment=SpriteAlignment.Custom, spriteID=GUID.Generate()};
        dp.SetSpriteRects(rects); dp.Apply();
        ((AssetImporter)dp.targetObject).SaveAndReimport();
        return true;
    }

    static void ImportSingle(string path, float ppu, FilterMode filter)
    {
        var ti=AssetImporter.GetAtPath(path) as TextureImporter; if(ti==null) return;
        ti.textureType=TextureImporterType.Sprite; ti.spriteImportMode=SpriteImportMode.Single;
        ti.spritePixelsPerUnit=ppu; ti.filterMode=filter; ti.mipmapEnabled=false;
        ti.textureCompression=TextureImporterCompression.Uncompressed; ti.alphaIsTransparency=true;
        EditorUtility.SetDirty(ti); ti.SaveAndReimport();
    }

    // =========================================================
    // 3 - Build Animator Controllers
    // =========================================================
    [MenuItem("Tools/Arena Combat/3 - Build Animator Controllers")]
    public static void Step3()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Animations")) AssetDatabase.CreateFolder("Assets","Animations");
        BuildCtrl("AC_Player1", K1, "k1");
        BuildCtrl("AC_Player2", K2, "k2");
        // Delete old misnamed file
        AssetDatabase.DeleteAsset(ANIM+"AC_Player_OLD.controller");
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("<color=lime>Step 3 done: AC_Player1 and AC_Player2 ready.</color>");
    }

    static void BuildCtrl(string name, string folder, string prefix)
    {
        string path = ANIM+name+".controller";
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
        var sI = St(sm,"Idle",  C(folder,prefix+"_Idle",  10, 8f,path,true),  true);
        var sR = St(sm,"Run",   C(folder,prefix+"_Run",   10,12f,path,true),  false);
        var sJ = St(sm,"Jump",  C(folder,prefix+"_Jump",   3,10f,path,false), false);
        var sF = St(sm,"Fall",  C(folder,prefix+"_Fall",   3,10f,path,false), false);
        var sL = St(sm,"Light", C(folder,prefix+"_Attack", 4,18f,path,false), false);
        var sH = St(sm,"Heavy", C(folder,prefix+"_Attack2",6,12f,path,false), false);
        var sHt= St(sm,"Hit",   C(folder,prefix+"_Hit",    1,12f,path,false), false);
        var sD = St(sm,"Death", C(folder,prefix+"_Death", 10, 8f,path,false), false);
        BoolT(sI,sR,"isMoving",true,0.05f); BoolT(sR,sI,"isMoving",false,0.05f);
        TrigT(sI,sJ,"jump"); TrigT(sR,sJ,"jump"); TrigT(sF,sJ,"jump");
        var jf=sJ.AddTransition(sF); jf.hasExitTime=false; jf.duration=0.05f;
        jf.AddCondition(AnimatorConditionMode.Less,0f,"velocityY");
        var fi=sF.AddTransition(sI); fi.hasExitTime=false; fi.duration=0.05f;
        fi.AddCondition(AnimatorConditionMode.If,0,"isGrounded");
        AnyT(sm,sL,"lightAttack"); ExT(sL,sI,0.9f,0.05f);
        AnyT(sm,sH,"heavyAttack"); ExT(sH,sI,0.9f,0.05f);
        AnyT(sm,sHt,"hit");        ExT(sHt,sI,0.85f,0.05f);
        AnyT(sm,sD,"death");
        EditorUtility.SetDirty(ctrl);
    }

    static AnimationClip C(string folder, string clipName, int frames, float fps, string ctrlPath, bool loop)
    {
        string cp = Path.GetDirectoryName(ctrlPath)+"/"+clipName+".anim";
        AssetDatabase.DeleteAsset(cp);
        var clip = new AnimationClip{frameRate=fps,name=clipName};
        var cs = AnimationUtility.GetAnimationClipSettings(clip); cs.loopTime=loop;
        AnimationUtility.SetAnimationClipSettings(clip,cs);
        int us = clipName.IndexOf('_');
        string sk = us>=0 ? clipName.Substring(us) : clipName;
        var sprites = AssetDatabase.LoadAllAssetsAtPath(folder+sk+".png").OfType<Sprite>()
            .OrderBy(s=>{int n=0;int.TryParse(s.name.Split('_').Last(),out n);return n;}).ToArray();
        if(sprites.Length==0){Debug.LogWarning("No sprites for "+clipName+" - run Step 2 first.");AssetDatabase.CreateAsset(clip,cp);return clip;}
        float dt=1f/fps;
        var b=new EditorCurveBinding{type=typeof(SpriteRenderer),path="",propertyName="m_Sprite"};
        var keys=new ObjectReferenceKeyframe[sprites.Length+(loop?1:0)];
        for(int i=0;i<sprites.Length;i++) keys[i]=new ObjectReferenceKeyframe{time=i*dt,value=sprites[i]};
        if(loop) keys[sprites.Length]=new ObjectReferenceKeyframe{time=sprites.Length*dt,value=sprites[0]};
        AnimationUtility.SetObjectReferenceCurve(clip,b,keys);
        AssetDatabase.CreateAsset(clip,cp); return clip;
    }

    static AnimatorState St(AnimatorStateMachine sm,string n,AnimationClip c,bool def)
        {var s=sm.AddState(n);s.motion=c;if(def)sm.defaultState=s;return s;}
    static void BoolT(AnimatorState f,AnimatorState t,string p,bool v,float d)
        {var x=f.AddTransition(t);x.hasExitTime=false;x.duration=d;x.AddCondition(v?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,p);}
    static void TrigT(AnimatorState f,AnimatorState t,string p)
        {var x=f.AddTransition(t);x.hasExitTime=false;x.duration=0.05f;x.AddCondition(AnimatorConditionMode.If,0,p);}
    static void AnyT(AnimatorStateMachine sm,AnimatorState t,string p)
        {var x=sm.AddAnyStateTransition(t);x.hasExitTime=false;x.duration=0.05f;x.canTransitionToSelf=false;x.AddCondition(AnimatorConditionMode.If,0,p);}
    static void ExT(AnimatorState f,AnimatorState t,float e,float d)
        {var x=f.AddTransition(t);x.hasExitTime=true;x.exitTime=e;x.duration=d;}

    // =========================================================
    // 4 - Build Gameplay Scene
    // =========================================================
    [MenuItem("Tools/Arena Combat/4 - Build Gameplay Scene")]
    public static void Step4()
    {
        int gL=LayerMask.NameToLayer("Ground"), pL=LayerMask.NameToLayer("Player");
        if(gL<0||pL<0){EditorUtility.DisplayDialog("Missing Layers","Run Step 1 first.","OK");return;}

        foreach(var n in new[]{"Background","Ground","LeftPlatform","RightPlatform","TopPlatform",
            "CenterPlatform","SpawnPoint1","SpawnPoint2","Player1","Player2",
            "GameManager","ArenaManagerGO","AudioManagerGO","HUD_Canvas","EventSystem"})
        {var g=GameObject.Find(n);if(g)Object.DestroyImmediate(g);}

        // EventSystem FIRST - required for any UI clicks
        CreateEventSystem();

        BuildBackground();

        // ── TILESET PLATFORMS ─────────────────────────────────
        // Ground (main floor) - using ground_platform.png tiled
        MakeTiledPlatform("Ground",
            new Vector3(0f, GROUND_Y, 0), new Vector2(26f, 0.6f), gL,
            BG+"ground_platform.png", BG+"ground_top_tile.png");

        // Side floating platforms - using platform textures
        MakeTiledPlatform("LeftPlatform",
            new Vector3(-5.5f, -1.2f, 0), new Vector2(5f, 0.4f), gL,
            BG+"floating_platform.png", BG+"platform_top_tile.png");

        MakeTiledPlatform("RightPlatform",
            new Vector3(5.5f, -1.2f, 0), new Vector2(5f, 0.4f), gL,
            BG+"floating_platform.png", BG+"platform_top_tile.png");

        MakeTiledPlatform("TopPlatform",
            new Vector3(0f, 1.5f, 0), new Vector2(4f, 0.4f), gL,
            BG+"floating_platform.png", BG+"platform_top_tile.png");

        var sp1=new GameObject("SpawnPoint1"); sp1.transform.position=new Vector3(-4f,SPAWN_Y,0);
        var sp2=new GameObject("SpawnPoint2"); sp2.transform.position=new Vector3(4f, SPAWN_Y,0);

        EnsureFolder("Assets/Characters"); EnsureFolder("Assets/Characters/Player");
        var p1=MakePlayer("Player1",1,pL,gL,sp1.transform.position);
        var p2=MakePlayer("Player2",2,pL,gL,sp2.transform.position);

        var gmGO=new GameObject("GameManager"); var gsm=gmGO.AddComponent<GameStateManager>();
        gsm.player1=p1.GetComponent<PlayerController>(); gsm.player2=p2.GetComponent<PlayerController>();
        gsm.spawnP1=sp1.transform; gsm.spawnP2=sp2.transform;
        gsm.roundsToWin=2; gsm.matchTimeSec=99f;

        var amGO=new GameObject("ArenaManagerGO"); var am=amGO.AddComponent<ArenaManager>();
        am.arenaCamera=Camera.main; am.player1Transform=p1.transform; am.player2Transform=p2.transform;
        am.deathZoneY=-9f; am.camMinSize=5f; am.camMaxSize=9f; am.camPadding=4f; am.camSmoothing=4f;

        var auGO=new GameObject("AudioManagerGO"); WireAudio(auGO.AddComponent<AudioManager>());

        if(Camera.main!=null) Camera.main.orthographicSize=6f;

        BuildGameplayHUD(gsm, p1.GetComponent<HealthManager>(), p2.GetComponent<HealthManager>());

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Gameplay Scene Built!",
            "Ctrl+S to save, then Play!\n\n"+
            "P1:  A/D move | W/Space jump | J light | K heavy\n"+
            "P2:  Numpad 4/6 move | Numpad 8 jump | Num0 light | NumEnter heavy",
            "OK");
    }

    // Creates a platform with the actual tileset texture tiled across it
    static void MakeTiledPlatform(string name, Vector3 pos, Vector2 size, int layer,
        string tiledTexPath, string topTexPath)
    {
        var go = new GameObject(name);
        go.layer = layer; go.tag = "Ground"; go.transform.position = pos;

        var sr = go.AddComponent<SpriteRenderer>();
        // Try to use the tileset sprites, fall back to solid colour
        Sprite spr = LSpr(tiledTexPath);
        if (spr == null) spr = LSpr(topTexPath);
        if (spr != null)
        {
            sr.sprite   = spr;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size     = size;
        }
        else
        {
            sr.sprite   = MakeSolidSpr(new Color32(90, 65, 30, 255));
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size     = size;
        }
        sr.sortingLayerName = "Foreground"; sr.sortingOrder = 1;

        var bc = go.AddComponent<BoxCollider2D>(); bc.size = size;
    }

    static void BuildBackground()
    {
        var root = new GameObject("Background");
        Sprite bgSpr = LSpr(BG+"background_wide.png");
        var bg = new GameObject("BG_Wide"); bg.transform.SetParent(root.transform);
        bg.transform.position = new Vector3(0, 1.5f, 0);
        var sr = bg.AddComponent<SpriteRenderer>();
        sr.sprite = bgSpr ?? MakeSolidSpr(new Color32(82,129,211,255));
        sr.drawMode = SpriteDrawMode.Tiled; sr.size = new Vector2(32f, 16f);
        sr.sortingLayerName = "Background"; sr.sortingOrder = 0;
        var pe = bg.AddComponent<ParallaxEffect>(); pe.cam=Camera.main; pe.parallaxStrength=0.12f;
    }

    static GameObject MakePlayer(string name,int idx,int pL,int gL,Vector3 pos)
    {
        var go=new GameObject(name); go.layer=pL; go.tag="Player";
        go.transform.position=pos;
        go.transform.localScale=new Vector3(idx==1?PLAYER_SCALE:-PLAYER_SCALE,PLAYER_SCALE,1f);
        var sr=go.AddComponent<SpriteRenderer>(); sr.sortingLayerName="Characters"; sr.sortingOrder=idx;
        var rb=go.AddComponent<Rigidbody2D>();
        rb.gravityScale=3f; rb.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
        rb.constraints=RigidbodyConstraints2D.FreezeRotation;
        var cap=go.AddComponent<CapsuleCollider2D>();
        cap.size=new Vector2(0.5f,CAP_HEIGHT); cap.offset=new Vector2(0f,CAP_OFFSET_Y);
        var anim=go.AddComponent<Animator>();
        string cn=idx==1?"AC_Player1":"AC_Player2";
        var ctrl=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ANIM+cn+".controller");
        if(ctrl!=null) anim.runtimeAnimatorController=ctrl; else Debug.LogWarning(cn+" not found - run Step 3.");
        var gc=new GameObject("GroundCheck"); gc.transform.SetParent(go.transform,false);
        gc.transform.localPosition=new Vector3(0f,CAP_BOT_LOCAL-0.05f,0f);
        var ap=new GameObject("AttackPoint"); ap.transform.SetParent(go.transform,false);
        ap.transform.localPosition=new Vector3(0.55f,0.5f,0f);
        var pc=go.AddComponent<PlayerController>();
        pc.playerIndex=idx; pc.walkSpeed=8f; pc.runSpeed=13f; pc.jumpForce=16f;
        pc.fastFallForce=22f; pc.groundCheckRadius=0.22f;
        pc.groundCheck=gc.transform; pc.groundLayer=1<<gL;
        go.AddComponent<HealthManager>();
        var cs=go.AddComponent<CombatSystem>();
        cs.attackPoint=ap.transform; cs.playerLayer=1<<pL;
        cs.lightAttackRadius=0.65f; cs.heavyAttackRadius=1.05f;
        go.AddComponent<AnimationController>(); go.AddComponent<DamageNumberSpawner>();
        string pp=CHAR+name+".prefab"; AssetDatabase.DeleteAsset(pp);
        bool ok; PrefabUtility.SaveAsPrefabAssetAndConnect(go,pp,InteractionMode.AutomatedAction,out ok);
        return go;
    }

    // ── Gameplay HUD ─────────────────────────────────────────
    static void BuildGameplayHUD(GameStateManager gsm, HealthManager hmP1, HealthManager hmP2)
    {
        var cvGO=new GameObject("HUD_Canvas");
        var cv=cvGO.AddComponent<Canvas>(); cv.renderMode=RenderMode.ScreenSpaceOverlay; cv.sortingOrder=20;
        var sc=cvGO.AddComponent<CanvasScaler>();
        sc.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution=new Vector2(1920,1080); sc.matchWidthOrHeight=0.5f;
        cvGO.AddComponent<GraphicRaycaster>();

        // Top bar
        Sprite sprTop=LSpr(UI+"hud_top_bar.png");
        if(sprTop!=null){
            var tb=new GameObject("TopBar"); tb.transform.SetParent(cvGO.transform,false);
            var i=tb.AddComponent<Image>(); i.sprite=sprTop; i.type=Image.Type.Sliced;
            var r=tb.GetComponent<RectTransform>();
            r.anchorMin=new Vector2(0,1);r.anchorMax=new Vector2(1,1);
            r.pivot=new Vector2(0.5f,1);r.sizeDelta=new Vector2(0,95);}

        // P1 and P2 HUD bars
        Image fillP1; TextMeshProUGUI hpP1, scoreP1;
        Image fillP2; TextMeshProUGUI hpP2, scoreP2;
        SideHUD(cvGO,false,"P1",out fillP1,out hpP1,out scoreP1);
        SideHUD(cvGO,true, "P2",out fillP2,out hpP2,out scoreP2);

        // Timer
        var tiRoot=new GameObject("TimerRoot"); tiRoot.transform.SetParent(cvGO.transform,false);
        Sprite sprTim=LSpr(UI+"timer_bg.png");
        var tiRT=tiRoot.AddComponent<RectTransform>();
        tiRT.anchorMin=new Vector2(0.5f,1);tiRT.anchorMax=new Vector2(0.5f,1);
        tiRT.pivot=new Vector2(0.5f,1);tiRT.anchoredPosition=new Vector2(0,-5);tiRT.sizeDelta=new Vector2(110,78);
        if(sprTim!=null){var ti=tiRoot.AddComponent<Image>();ti.sprite=sprTim;ti.type=Image.Type.Sliced;}
        var timerTMP=ATMP(tiRoot,"TimerText","99",new Vector2(0,-4),new Vector2(110,70),64f,Color.white,TextAlignmentOptions.Center);
        timerTMP.fontStyle=FontStyles.Bold;

        // Announcement text (centre)
        var annGO=new GameObject("AnnouncementText"); annGO.transform.SetParent(cvGO.transform,false);
        var annTMP=annGO.AddComponent<TextMeshProUGUI>();
        annTMP.text=""; annTMP.fontSize=72f; annTMP.color=Color.yellow;
        annTMP.alignment=TextAlignmentOptions.Center; annTMP.fontStyle=FontStyles.Bold;
        var annRT=annGO.GetComponent<RectTransform>();
        annRT.anchorMin=new Vector2(0.5f,0.5f); annRT.anchorMax=new Vector2(0.5f,0.5f);
        annRT.anchoredPosition=Vector2.zero; annRT.sizeDelta=new Vector2(1100,130);

        // Screen flash
        var fGO=new GameObject("ScreenFlash"); fGO.transform.SetParent(cvGO.transform,false);
        var fImg=fGO.AddComponent<Image>(); fImg.color=new Color(1,1,1,0); StretchRT(fGO);
        var sf=cvGO.AddComponent<ScreenFlash>(); sf.flashImage=fImg;

        // Match-over panel
        var panGO=new GameObject("MatchOverPanel"); panGO.transform.SetParent(cvGO.transform,false);
        var panImg=panGO.AddComponent<Image>();
        panImg.sprite=LSpr(UI+"panel_dark.png"); panImg.type=Image.Type.Sliced;
        panImg.color=new Color(0,0,0,0.88f); StretchRT(panGO);
        var resTMP=ATMP(panGO,"ResultText","PLAYER 1\nWINS!",
            new Vector2(0,110),new Vector2(800,220),96f,Color.yellow,TextAlignmentOptions.Center);
        resTMP.fontStyle=FontStyles.Bold;
        ATMP(panGO,"HintText",
            "P1: A/D move | W/Space = jump | J = light | K = heavy\n"+
            "P2: Numpad 4/6 move | Numpad 8 = jump | Num0 = light | NumEnter = heavy",
            new Vector2(0,-10),new Vector2(1000,70),20f,Color.white,TextAlignmentOptions.Center);
        // ASCII-only button labels (no Unicode arrows - missing from LiberationSans)
        var btnN=LSpr(UI+"button_normal.png"); var btnH=LSpr(UI+"button_hover.png");
        var rematch=MakeBtn(panGO,"RematchBtn","PLAY AGAIN", btnN,btnH,new Vector2(-200,-140),new Vector2(280,65));
        var menu   =MakeBtn(panGO,"MenuBtn",   "MAIN MENU",  btnN,btnH,new Vector2(0,-140),   new Vector2(240,65));
        var quit   =MakeBtn(panGO,"QuitBtn",   "QUIT",       btnN,btnH,new Vector2(190,-140),  new Vector2(180,65));
        panGO.SetActive(false);

        // Bottom controls hint
        var hGO=new GameObject("BottomHint"); hGO.transform.SetParent(cvGO.transform,false);
        var ht=hGO.AddComponent<TextMeshProUGUI>();
        ht.text="P1: A/D   W/Space=Jump   J=Light   K=Heavy          P2: Num4/6   Num8=Jump   Num0=Light   NumEnter=Heavy";
        ht.fontSize=16f; ht.color=new Color(1,1,1,0.4f); ht.alignment=TextAlignmentOptions.Center;
        var hRT=hGO.GetComponent<RectTransform>();
        hRT.anchorMin=new Vector2(0.5f,0);hRT.anchorMax=new Vector2(0.5f,0);
        hRT.pivot=new Vector2(0.5f,0);hRT.anchoredPosition=new Vector2(0,18);hRT.sizeDelta=new Vector2(1500,28);

        // UIManager
        var uimGO=new GameObject("UIManager"); uimGO.transform.SetParent(cvGO.transform,false);
        var uim=uimGO.AddComponent<UIManager>();
        uim.healthFillP1=fillP1; uim.healthFillP2=fillP2;
        uim.hpTextP1=hpP1; uim.hpTextP2=hpP2;
        uim.healthManagerP1=hmP1; uim.healthManagerP2=hmP2;
        uim.timerText=timerTMP; uim.announcementText=annTMP;
        uim.p1ScoreText=scoreP1; uim.p2ScoreText=scoreP2;
        uim.matchOverPanel=panGO; uim.resultText=resTMP;
        uim.rematchButton=rematch.GetComponent<Button>();
        uim.menuButton=menu.GetComponent<Button>();
        uim.quitButton=quit.GetComponent<Button>();
    }

    static void SideHUD(GameObject cv, bool right, string id,
        out Image fill, out TextMeshProUGUI hpTMP, out TextMeshProUGUI scoreTMP)
    {
        float a=right?1f:0f, x=right?-14f:14f;
        // Name plate
        var nGO=new GameObject(id+"_Name"); nGO.transform.SetParent(cv.transform,false);
        Sprite nSpr=LSpr(UI+"nameplate_bg.png");
        if(nSpr!=null){var ni=nGO.AddComponent<Image>();ni.sprite=nSpr;ni.type=Image.Type.Sliced;}
        var nRT=nGO.GetComponent<RectTransform>();
        nRT.anchorMin=new Vector2(a,1);nRT.anchorMax=new Vector2(a,1);nRT.pivot=new Vector2(a,1);
        nRT.anchoredPosition=new Vector2(x,-8f);nRT.sizeDelta=new Vector2(240,30);
        ATMP(nGO,id+"_Label",right?"PLAYER 2":"PLAYER 1",new Vector2(right?-8f:8f,0),new Vector2(224,28),20f,
            right?new Color(1f,0.45f,0.45f):new Color(0.45f,0.85f,1f),
            right?TextAlignmentOptions.Right:TextAlignmentOptions.Left);
        // Health bar
        var hbGO=new GameObject(id+"_HealthBar"); hbGO.transform.SetParent(cv.transform,false);
        var bg=hbGO.AddComponent<Image>();
        Sprite bgSpr=LSpr(UI+"health_bar_bg.png");
        if(bgSpr!=null){bg.sprite=bgSpr;bg.type=Image.Type.Sliced;}else bg.color=new Color(0.15f,0.03f,0.03f);
        var hRT=hbGO.GetComponent<RectTransform>();
        hRT.anchorMin=new Vector2(a,1);hRT.anchorMax=new Vector2(a,1);hRT.pivot=new Vector2(a,1);
        hRT.anchoredPosition=new Vector2(x,-42f);hRT.sizeDelta=new Vector2(590,32);
        // Fill image (Image.fillAmount - not Slider)
        var fGO=new GameObject("Fill"); fGO.transform.SetParent(hbGO.transform,false);
        var fi=fGO.AddComponent<Image>();
        Sprite fillSpr=LSpr(UI+"health_bar_fill.png");
        if(fillSpr!=null) fi.sprite=fillSpr;
        fi.color=new Color(0.1f,0.9f,0.1f);
        fi.type=Image.Type.Filled;
        fi.fillMethod=Image.FillMethod.Horizontal;
        fi.fillOrigin=right?1:0;  // P2 shrinks from right side
        fi.fillAmount=1f;
        var fRT=fGO.GetComponent<RectTransform>();
        fRT.anchorMin=Vector2.zero;fRT.anchorMax=Vector2.one;fRT.offsetMin=new Vector2(3,3);fRT.offsetMax=new Vector2(-3,-3);
        // HP label
        hpTMP=ATMP(hbGO,id+"_HP","100",new Vector2(right?8f:-8f,0),new Vector2(80,28),16f,Color.white,
            right?TextAlignmentOptions.Left:TextAlignmentOptions.Right);
        // Score
        var sGO=new GameObject(id+"_Score"); sGO.transform.SetParent(cv.transform,false);
        scoreTMP=sGO.AddComponent<TextMeshProUGUI>();
        scoreTMP.text="";scoreTMP.fontSize=22f;scoreTMP.color=Color.yellow;
        scoreTMP.alignment=right?TextAlignmentOptions.Right:TextAlignmentOptions.Left;
        var sRT=sGO.GetComponent<RectTransform>();
        sRT.anchorMin=new Vector2(a,1);sRT.anchorMax=new Vector2(a,1);sRT.pivot=new Vector2(a,1);
        sRT.anchoredPosition=new Vector2(x,-78f);sRT.sizeDelta=new Vector2(130,26);
        fill=fi;
    }

    // =========================================================
    // 5 - Physics2D Matrix
    // =========================================================
    [MenuItem("Tools/Arena Combat/5 - Fix Physics2D Layer Matrix")]
    public static void Step5()
    {
        int g=LayerMask.NameToLayer("Ground"),p=LayerMask.NameToLayer("Player");
        if(g<0||p<0){Debug.LogError("Run Step 1 first.");return;}
        Physics2D.IgnoreLayerCollision(p,g,false);
        Physics2D.IgnoreLayerCollision(p,p,true);
        Debug.Log("<color=lime>Step 5 done: Players land on Ground, pass through each other.</color>");
    }

    // =========================================================
    // 6 - Clean Stale Files
    // =========================================================
    [MenuItem("Tools/Arena Combat/6 - Clean Stale Files")]
    public static void Step6()
    {
        string[] stale={"Assets/Animations/AC_Player.controller","Assets/Animations/AC_Player_OLD.controller",
            "Assets/Animations/player_idle.anim","Assets/Animations/player_run.anim","Assets/Animations/player_walk.anim",
            "Assets/Characters/Player/AC_Player.controller","Assets/InputSystem_Actions.inputactions"};
        int n=0;
        foreach(var f in stale) if(AssetDatabase.LoadAssetAtPath<Object>(f)!=null){AssetDatabase.DeleteAsset(f);n++;}
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"<color=lime>Step 6 done: Removed {n} stale files.</color>");
    }

    // =========================================================
    // 7 - Build Main Menu Scene
    // =========================================================
    [MenuItem("Tools/Arena Combat/7 - Build Main Menu Scene")]
    public static void Step7()
    {
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        var menuScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Camera
        var cam = Camera.main;
        if(cam!=null){ cam.backgroundColor=new Color(0.04f,0.04f,0.12f); cam.clearFlags=CameraClearFlags.SolidColor; }

        // CRITICAL: EventSystem must exist for buttons to respond to mouse/keyboard
        CreateEventSystem();

        // Canvas
        var cvGO=new GameObject("MainMenu_Canvas");
        var cv=cvGO.AddComponent<Canvas>(); cv.renderMode=RenderMode.ScreenSpaceOverlay; cv.sortingOrder=10;
        var sc=cvGO.AddComponent<CanvasScaler>();
        sc.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution=new Vector2(1920,1080); sc.matchWidthOrHeight=0.5f;
        cvGO.AddComponent<GraphicRaycaster>();

        // Background
        var bgGO=new GameObject("Background"); bgGO.transform.SetParent(cvGO.transform,false);
        var bgImg=bgGO.AddComponent<Image>();
        Sprite menuBg=LSpr(UI+"menu_bg.png");
        if(menuBg!=null) bgImg.sprite=menuBg; else bgImg.color=new Color(0.04f,0.04f,0.15f);
        StretchRT(bgGO);

        // Title
        var titBg=new GameObject("TitleBg"); titBg.transform.SetParent(cvGO.transform,false);
        Sprite titSpr=LSpr(UI+"title_plate.png");
        if(titSpr!=null){var ti=titBg.AddComponent<Image>();ti.sprite=titSpr;ti.type=Image.Type.Sliced;}
        else{var ti=titBg.AddComponent<Image>();ti.color=new Color(0.1f,0.08f,0.02f,0.9f);}
        var titRT=titBg.GetComponent<RectTransform>();
        titRT.anchorMin=new Vector2(0.5f,1);titRT.anchorMax=new Vector2(0.5f,1);titRT.pivot=new Vector2(0.5f,1);
        titRT.anchoredPosition=new Vector2(0,-80);titRT.sizeDelta=new Vector2(900,160);
        var titleTMP=ATMP(titBg,"Title","2D ARENA COMBAT",Vector2.zero,new Vector2(880,155),64f,new Color(1f,0.85f,0.2f),TextAlignmentOptions.Center);
        titleTMP.fontStyle=FontStyles.Bold;

        // Subtitle
        var subTMP=ATMP(cvGO,"Subtitle","PUSL3190  |  10952382  |  Singith Kesara Wahalathanthri",
            new Vector2(0,-255),new Vector2(900,40),20f,new Color(1,1,1,0.5f),TextAlignmentOptions.Center);
        var subRT=subTMP.GetComponent<RectTransform>();
        subRT.anchorMin=new Vector2(0.5f,1);subRT.anchorMax=new Vector2(0.5f,1);subRT.pivot=new Vector2(0.5f,1);
        subRT.anchoredPosition=new Vector2(0,-255);

        // ── Menu Panel ────────────────────────────────────────
        var menuPanel=new GameObject("MenuPanel"); menuPanel.transform.SetParent(cvGO.transform,false);
        var mpRT=menuPanel.AddComponent<RectTransform>();
        mpRT.anchorMin=new Vector2(0.5f,0.5f);mpRT.anchorMax=new Vector2(0.5f,0.5f);
        mpRT.anchoredPosition=new Vector2(0,-20);mpRT.sizeDelta=new Vector2(500,350);

        Sprite btnN=LSpr(UI+"menu_btn.png"), btnH=LSpr(UI+"menu_btn_hover.png");
        // ASCII labels ONLY - no Unicode symbols that are missing from LiberationSans SDF
        var playBtn  =MakeBtn(menuPanel,"PlayBtn",    "PLAY",     btnN,btnH,new Vector2(0,110),new Vector2(380,70));
        var ctrlBtn  =MakeBtn(menuPanel,"CtrlBtn",    "CONTROLS", btnN,btnH,new Vector2(0, 20),new Vector2(380,70));
        var quitBtn  =MakeBtn(menuPanel,"QuitBtn",    "QUIT",     btnN,btnH,new Vector2(0,-70),new Vector2(380,70));

        // ── Controls Panel ────────────────────────────────────
        var ctrlPanel=new GameObject("ControlsPanel"); ctrlPanel.transform.SetParent(cvGO.transform,false);
        var cpRT=ctrlPanel.AddComponent<RectTransform>();
        cpRT.anchorMin=new Vector2(0.5f,0.5f);cpRT.anchorMax=new Vector2(0.5f,0.5f);
        cpRT.anchoredPosition=new Vector2(0,-30);cpRT.sizeDelta=new Vector2(1000,520);
        var cpBg=ctrlPanel.AddComponent<Image>(); cpBg.color=new Color(0.04f,0.04f,0.18f,0.97f);

        ATMP(ctrlPanel,"CtrlTitle","CONTROLS",new Vector2(0,210),new Vector2(800,50),38f,Color.yellow,TextAlignmentOptions.Center);

        ATMP(ctrlPanel,"P1Controls",
            "PLAYER 1\n\n"+
            "A / D           Move Left / Right\n"+
            "W or Space      Jump  (press twice for double jump)\n"+
            "S               Fast Fall\n"+
            "J               Light Attack\n"+
            "K               Heavy Attack",
            new Vector2(-220,20),new Vector2(420,280),22f,new Color(0.45f,0.85f,1f),TextAlignmentOptions.Left);

        ATMP(ctrlPanel,"P2Controls",
            "PLAYER 2\n\n"+
            "Numpad 4/6      Move Left / Right\n"+
            "Numpad 8        Jump  (press twice for double jump)\n"+
            "Numpad 5        Fast Fall\n"+
            "Numpad 0        Light Attack\n"+
            "Numpad Enter    Heavy Attack",
            new Vector2(220,20),new Vector2(420,280),22f,new Color(1f,0.45f,0.45f),TextAlignmentOptions.Left);

        var backBtn=MakeBtn(ctrlPanel,"BackBtn","BACK",btnN,btnH,new Vector2(0,-220),new Vector2(280,60));
        ctrlPanel.SetActive(false);

        // ── Fade overlay ──────────────────────────────────────
        var fadeGO=new GameObject("FadeOverlay"); fadeGO.transform.SetParent(cvGO.transform,false);
        var fadeImg=fadeGO.AddComponent<Image>(); fadeImg.color=Color.black; StretchRT(fadeGO);
        var fadeGroup=fadeGO.AddComponent<CanvasGroup>();

        // ── MainMenuManager ───────────────────────────────────
        var mmGO=new GameObject("MainMenuManager");
        var mm=mmGO.AddComponent<MainMenuManager>();
        mm.menuPanel=menuPanel; mm.controlsPanel=ctrlPanel;
        mm.playButton=playBtn.GetComponent<Button>();
        mm.controlsButton=ctrlBtn.GetComponent<Button>();
        mm.backButton=backBtn.GetComponent<Button>();
        mm.quitButton=quitBtn.GetComponent<Button>();
        mm.fadeGroup=fadeGroup;
        mm.gameplaySceneName=GAMEPLAY_SCENE;  // "Gameplayscene" - matches actual file
        mm.clickSFX=LAC(AUDIO+"SFX/10_UI_Menu_SFX/013_Confirm_03.wav");
        mm.menuMusicClip=LAC(AUDIO+"Music/10 Battle1 (8bit style)/Assets_for_Unity/8bit-Battle01_loop.ogg");
        var mAud=mmGO.AddComponent<AudioSource>(); mAud.playOnAwake=false; mAud.loop=true; mAud.volume=0.35f;
        mm.menuMusic=mAud;

        // ── Save scene ────────────────────────────────────────
        string scenePath=SCENES+"MainMenu.unity";
        EditorSceneManager.SaveScene(menuScene, scenePath);

        // ── Build settings ────────────────────────────────────
        EditorBuildSettings.scenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene(SCENES+"MainMenu.unity", true),
            new EditorBuildSettingsScene(SCENES+"Gameplayscene.unity", true)
        };

        AssetDatabase.Refresh();
        Debug.Log("<color=lime>Step 7 done: MainMenu.unity saved. Open it and press Play!</color>");
        EditorUtility.DisplayDialog("Main Menu Built!",
            "MainMenu.unity saved in Assets/Scenes/\n\n"+
            "Open the MainMenu scene and press Play.\n"+
            "Buttons respond to mouse clicks AND keyboard (arrow keys + Enter).",
            "Ready!");
    }

    // =========================================================
    // Utilities
    // =========================================================
    static void CreateEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        // InputSystemUIInputModule handles UI with New Input System
            esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        Debug.Log("<color=yellow>EventSystem created - buttons now respond to mouse and keyboard.</color>");
    }

    static TextMeshProUGUI ATMP(GameObject parent,string name,string text,
        Vector2 apos,Vector2 size,float fs,Color col,TextAlignmentOptions align)
    {
        var go=new GameObject(name); go.transform.SetParent(parent.transform,false);
        var t=go.AddComponent<TextMeshProUGUI>();
        t.text=text;t.fontSize=fs;t.color=col;t.alignment=align;
        var rt=go.GetComponent<RectTransform>();rt.anchoredPosition=apos;rt.sizeDelta=size;
        return t;
    }

    static GameObject MakeBtn(GameObject parent,string name,string label,
        Sprite normSpr,Sprite hovSpr,Vector2 apos,Vector2 size)
    {
        var go=new GameObject(name); go.transform.SetParent(parent.transform,false);
        var img=go.AddComponent<Image>();
        if(normSpr!=null){img.sprite=normSpr;img.type=Image.Type.Sliced;}
        else img.color=new Color(0.12f,0.12f,0.25f,0.95f);
        var btn=go.AddComponent<Button>();btn.targetGraphic=img;
        var colors=btn.colors;
        colors.highlightedColor=new Color(0.8f,0.9f,1f);
        colors.pressedColor=new Color(0.6f,0.7f,0.9f);
        btn.colors=colors;
        if(hovSpr!=null){var ss=btn.spriteState;ss.highlightedSprite=hovSpr;btn.spriteState=ss;btn.transition=Selectable.Transition.SpriteSwap;}
        var rt=go.GetComponent<RectTransform>();rt.anchoredPosition=apos;rt.sizeDelta=size;
        var lbl=ATMP(go,"Label",label,Vector2.zero,size,28f,Color.white,TextAlignmentOptions.Center);
        lbl.fontStyle=FontStyles.Bold;
        return go;
    }

    static void StretchRT(GameObject go)
    {
        var rt=go.GetComponent<RectTransform>();
        rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
    }

    static Sprite LSpr(string path)
        {return AssetDatabase.LoadAssetAtPath<Sprite>(path);}

    static Sprite MakeSolidSpr(Color32 c)
    {
        var t=new Texture2D(4,4,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
        var p=new Color32[16];for(int i=0;i<16;i++)p[i]=c;t.SetPixels32(p);t.Apply();
        return Sprite.Create(t,new Rect(0,0,4,4),Vector2.one*0.5f,4f);
    }

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
        {var c=AssetDatabase.LoadAssetAtPath<AudioClip>(p);if(c==null)Debug.LogWarning("Audio: "+p);return c;}

    static void EnsureFolder(string path)
    {
        if(!AssetDatabase.IsValidFolder(path))
        {
            string parent=System.IO.Path.GetDirectoryName(path).Replace('\\','/');
            string child =System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent,child);
        }
    }
}
#endif
