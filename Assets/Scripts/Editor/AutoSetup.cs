#if UNITY_EDITOR
// ============================================================
//  AutoSetup.cs — Tools → Arena Combat → run ①②③④⑤⑥⑦ in order
//  Unity 6 | New Input System | Health bars use Image.fillAmount
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
using UnityEngine.SceneManagement;
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
    const string SCENES= "Assets/Scenes/";

    // Physics constants
    const float GROUND_Y      = -3f;
    const float GROUND_H      = 0.6f;
    const float GROUND_TOP    = GROUND_Y + GROUND_H / 2f;  // -2.7
    const float CAP_OFFSET_Y  = 0.4f;
    const float CAP_HEIGHT    = 1.2f;
    const float CAP_BOT_LOCAL = CAP_OFFSET_Y - CAP_HEIGHT / 2f; // -0.2
    const float PLAYER_SCALE  = 2f;
    const float SPAWN_Y       = GROUND_TOP - CAP_BOT_LOCAL * PLAYER_SCALE; // -2.3

    // =========================================================
    // ① Layers & Tags
    // =========================================================
    [MenuItem("Tools/Arena Combat/\u2460 Setup Layers and Tags")]
    public static void Step1_SetupLayers()
    {
        var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (asset == null || asset.Length == 0) { Debug.LogError("Cannot open TagManager.asset"); return; }
        var so = new SerializedObject(asset[0]);

        AddTag(so, "Ground");
        SetLayer(so, 6, "Ground");
        SetLayer(so, 7, "Player");

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset[0]);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("<color=lime>\u2713 Step 1: Layer 6=Ground, Layer 7=Player.</color>");
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
    // ② Slice sprite sheets
    // =========================================================
    [MenuItem("Tools/Arena Combat/\u2461 Slice Sprite Sheets")]
    public static void Step2_SliceSheets()
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
        SingleSprite(BG+"background_wide.png", 16f);
        SingleSprite(BG+"BG1.png", 16f);
        foreach (var f in new[]{
            "health_bar_bg.png","health_bar_fill.png","timer_bg.png","nameplate_bg.png",
            "panel_dark.png","button_normal.png","button_hover.png","hud_top_bar.png",
            "menu_bg.png","title_plate.png","menu_btn.png","menu_btn_hover.png","divider.png"})
            SingleSprite(UI+f, 1f);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"<color=lime>\u2713 Step 2: {n} knight sheets sliced.</color>");
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
        if (dp==null) return false;
        dp.InitSpriteEditorDataProvider();
        var rects=new SpriteRect[frames];
        for (int i=0;i<frames;i++) rects[i]=new SpriteRect{
            name=prefix+key+"_"+i, rect=new Rect(i*120,0,120,80),
            pivot=new Vector2(0.5f,0.2f), alignment=SpriteAlignment.Custom, spriteID=GUID.Generate()};
        dp.SetSpriteRects(rects); dp.Apply();
        ((AssetImporter)dp.targetObject).SaveAndReimport();
        return true;
    }
    static void SingleSprite(string path, float ppu)
    {
        var ti=AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti==null) return;
        ti.textureType=TextureImporterType.Sprite; ti.spriteImportMode=SpriteImportMode.Single;
        ti.spritePixelsPerUnit=ppu; ti.filterMode=FilterMode.Bilinear; ti.mipmapEnabled=false;
        ti.textureCompression=TextureImporterCompression.Uncompressed; ti.alphaIsTransparency=true;
        EditorUtility.SetDirty(ti); ti.SaveAndReimport();
    }

    // =========================================================
    // ③ Build Animator Controllers
    // =========================================================
    [MenuItem("Tools/Arena Combat/\u2462 Build Animator Controllers")]
    public static void Step3_BuildControllers()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Animations"))
            AssetDatabase.CreateFolder("Assets","Animations");
        BuildCtrl("AC_Player1", K1, "k1");
        BuildCtrl("AC_Player2", K2, "k2");
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("<color=lime>\u2713 Step 3: Animator controllers ready.</color>");
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
        var sIdle  = St(sm,"Idle",  Clip(folder,prefix+"_Idle",  10, 8f,path,true), true);
        var sRun   = St(sm,"Run",   Clip(folder,prefix+"_Run",   10,12f,path,true), false);
        var sJump  = St(sm,"Jump",  Clip(folder,prefix+"_Jump",   3,10f,path,false),false);
        var sFall  = St(sm,"Fall",  Clip(folder,prefix+"_Fall",   3,10f,path,false),false);
        var sLight = St(sm,"Light", Clip(folder,prefix+"_Attack", 4,18f,path,false),false);
        var sHeavy = St(sm,"Heavy", Clip(folder,prefix+"_Attack2",6,12f,path,false),false);
        var sHit   = St(sm,"Hit",   Clip(folder,prefix+"_Hit",    1,12f,path,false),false);
        var sDeath = St(sm,"Death", Clip(folder,prefix+"_Death", 10, 8f,path,false),false);
        BoolT(sIdle,sRun, "isMoving",true, 0.05f); BoolT(sRun,sIdle,"isMoving",false,0.05f);
        TrigT(sIdle,sJump,"jump"); TrigT(sRun,sJump,"jump"); TrigT(sFall,sJump,"jump");
        var jf=sJump.AddTransition(sFall); jf.hasExitTime=false; jf.duration=0.05f;
        jf.AddCondition(AnimatorConditionMode.Less,0f,"velocityY");
        var fi=sFall.AddTransition(sIdle); fi.hasExitTime=false; fi.duration=0.05f;
        fi.AddCondition(AnimatorConditionMode.If,0,"isGrounded");
        AnyT(sm,sLight,"lightAttack"); ExT(sLight,sIdle,0.9f,0.05f);
        AnyT(sm,sHeavy,"heavyAttack"); ExT(sHeavy,sIdle,0.9f,0.05f);
        AnyT(sm,sHit,"hit");           ExT(sHit,sIdle,0.85f,0.05f);
        AnyT(sm,sDeath,"death");
        EditorUtility.SetDirty(ctrl);
    }

    static AnimationClip Clip(string folder,string clipName,int frames,float fps,string ctrlPath,bool loop)
    {
        string cp=Path.GetDirectoryName(ctrlPath)+"/"+clipName+".anim";
        AssetDatabase.DeleteAsset(cp);
        var clip=new AnimationClip{frameRate=fps,name=clipName};
        var cs=AnimationUtility.GetAnimationClipSettings(clip); cs.loopTime=loop;
        AnimationUtility.SetAnimationClipSettings(clip,cs);
        int us=clipName.IndexOf('_');
        string sk=us>=0?clipName.Substring(us):clipName;
        var sprites=AssetDatabase.LoadAllAssetsAtPath(folder+sk+".png").OfType<Sprite>()
            .OrderBy(s=>{int n=0;int.TryParse(s.name.Split('_').Last(),out n);return n;}).ToArray();
        if(sprites.Length==0){Debug.LogWarning("No sprites for "+clipName);AssetDatabase.CreateAsset(clip,cp);return clip;}
        float dt=1f/fps;
        var b=new EditorCurveBinding{type=typeof(SpriteRenderer),path="",propertyName="m_Sprite"};
        int kc=sprites.Length+(loop?1:0);
        var keys=new ObjectReferenceKeyframe[kc];
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
    // ④ Build Gameplay Scene
    // =========================================================
    [MenuItem("Tools/Arena Combat/\u2463 Build Gameplay Scene")]
    public static void Step4_BuildGameplay()
    {
        int gL=LayerMask.NameToLayer("Ground"), pL=LayerMask.NameToLayer("Player");
        if(gL<0||pL<0){EditorUtility.DisplayDialog("Layers Missing","Run Step ① first.","OK");return;}

        foreach(var n in new[]{"Background","Ground","LeftPlatform","RightPlatform","TopPlatform",
            "SpawnPoint1","SpawnPoint2","Player1","Player2","GameManager","ArenaManagerGO","AudioManagerGO","HUD_Canvas"})
        {var g=GameObject.Find(n);if(g)Object.DestroyImmediate(g);}

        BuildBackground();
        MakePlat("Ground",        new Vector3(0f,GROUND_Y,0),   new Vector2(26f,GROUND_H), gL, new Color32(110,70,30,255));
        MakePlat("LeftPlatform",  new Vector3(-5.5f,-1.2f,0),   new Vector2(5f,0.4f),      gL, new Color32(60,115,40,255));
        MakePlat("RightPlatform", new Vector3(5.5f,-1.2f,0),    new Vector2(5f,0.4f),      gL, new Color32(60,115,40,255));
        MakePlat("TopPlatform",   new Vector3(0f,1.5f,0),       new Vector2(4f,0.4f),      gL, new Color32(40,85,135,255));

        var sp1=new GameObject("SpawnPoint1"); sp1.transform.position=new Vector3(-4f,SPAWN_Y,0);
        var sp2=new GameObject("SpawnPoint2"); sp2.transform.position=new Vector3(4f, SPAWN_Y,0);

        if(!AssetDatabase.IsValidFolder("Assets/Characters")) AssetDatabase.CreateFolder("Assets","Characters");
        if(!AssetDatabase.IsValidFolder("Assets/Characters/Player")) AssetDatabase.CreateFolder("Assets/Characters","Player");
        var p1=MakePlayer("Player1",1,pL,gL,sp1.transform.position);
        var p2=MakePlayer("Player2",2,pL,gL,sp2.transform.position);

        var gmGO=new GameObject("GameManager");
        var gsm=gmGO.AddComponent<GameStateManager>();
        gsm.player1=p1.GetComponent<PlayerController>(); gsm.player2=p2.GetComponent<PlayerController>();
        gsm.spawnP1=sp1.transform; gsm.spawnP2=sp2.transform;
        gsm.roundsToWin=2; gsm.matchTimeSec=99f;

        var amGO=new GameObject("ArenaManagerGO"); var am=amGO.AddComponent<ArenaManager>();
        am.arenaCamera=Camera.main; am.player1Transform=p1.transform; am.player2Transform=p2.transform;
        am.deathZoneY=-9f; am.camMinSize=5f; am.camMaxSize=9f; am.camPadding=4f; am.camSmoothing=4f;

        var auGO=new GameObject("AudioManagerGO"); var au=auGO.AddComponent<AudioManager>();
        WireAudio(au);

        if(Camera.main!=null) Camera.main.orthographicSize=6f;

        BuildGameplayHUD(gsm, p1.GetComponent<HealthManager>(), p2.GetComponent<HealthManager>());

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Gameplay Scene Built!",
            "Ctrl+S to save, then Press Play!\n\n"+
            "P1: A/D move | W/Space = jump | J = light | K = heavy\n"+
            "P2: Numpad4/6 | Numpad8 = jump | Num0 = light | NumEnter = heavy","OK");
    }

    static void BuildBackground()
    {
        var root=new GameObject("Background");
        Sprite wide=AssetDatabase.LoadAssetAtPath<Sprite>(BG+"background_wide.png");
        var bg=new GameObject("BG_Layer"); bg.transform.SetParent(root.transform);
        bg.transform.position=new Vector3(0,1.5f,0);
        var sr=bg.AddComponent<SpriteRenderer>();
        sr.sprite=wide??MakeSolidSpr(new Color32(82,129,211,255));
        sr.drawMode=SpriteDrawMode.Tiled; sr.size=new Vector2(32f,15f);
        sr.sortingLayerName="Background"; sr.sortingOrder=0;
        var pe=bg.AddComponent<ParallaxEffect>(); pe.cam=Camera.main; pe.parallaxStrength=0.15f;
    }

    static void MakePlat(string name,Vector3 pos,Vector2 size,int layer,Color32 col)
    {
        var go=new GameObject(name); go.layer=layer; go.tag="Ground"; go.transform.position=pos;
        var sr=go.AddComponent<SpriteRenderer>(); sr.sprite=MakeSolidSpr(col);
        sr.drawMode=SpriteDrawMode.Tiled; sr.size=size; sr.sortingLayerName="Foreground"; sr.sortingOrder=0;
        var bc=go.AddComponent<BoxCollider2D>(); bc.size=size;
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
        if(ctrl!=null) anim.runtimeAnimatorController=ctrl; else Debug.LogWarning(cn+" not found — run Step ③.");
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
        go.AddComponent<AnimationController>();
        go.AddComponent<DamageNumberSpawner>();
        string prefabPath=CHAR+name+".prefab";
        AssetDatabase.DeleteAsset(prefabPath);
        bool ok; PrefabUtility.SaveAsPrefabAssetAndConnect(go,prefabPath,InteractionMode.AutomatedAction,out ok);
        return go;
    }

    // =========================================================
    // Gameplay HUD — uses Image.fillAmount for health bars
    // =========================================================
    static void BuildGameplayHUD(GameStateManager gsm, HealthManager hmP1, HealthManager hmP2)
    {
        var cvGO=new GameObject("HUD_Canvas");
        var cv=cvGO.AddComponent<Canvas>(); cv.renderMode=RenderMode.ScreenSpaceOverlay; cv.sortingOrder=20;
        var sc=cvGO.AddComponent<CanvasScaler>();
        sc.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution=new Vector2(1920,1080); sc.matchWidthOrHeight=0.5f;
        cvGO.AddComponent<GraphicRaycaster>();

        Sprite sprBg    = LSpr(UI+"health_bar_bg.png");
        Sprite sprFill  = LSpr(UI+"health_bar_fill.png");
        Sprite sprTimer = LSpr(UI+"timer_bg.png");
        Sprite sprName  = LSpr(UI+"nameplate_bg.png");
        Sprite sprPanel = LSpr(UI+"panel_dark.png");
        Sprite sprBtnN  = LSpr(UI+"button_normal.png");
        Sprite sprBtnH  = LSpr(UI+"button_hover.png");
        Sprite sprTop   = LSpr(UI+"hud_top_bar.png");

        // Top bar
        if(sprTop!=null){
            var tb=new GameObject("TopBar"); tb.transform.SetParent(cvGO.transform,false);
            var img=tb.AddComponent<Image>(); img.sprite=sprTop; img.type=Image.Type.Sliced;
            var rt=tb.GetComponent<RectTransform>();
            rt.anchorMin=new Vector2(0,1); rt.anchorMax=new Vector2(1,1);
            rt.pivot=new Vector2(0.5f,1); rt.sizeDelta=new Vector2(0,95);}

        // ── P1 HUD (left) ─────────────────────────────────────
        Image fillP1; TextMeshProUGUI hpP1, scoreP1;
        BuildSideHUD(cvGO, false, "P1", sprBg, sprFill, sprName,
            out fillP1, out hpP1, out scoreP1);

        // ── P2 HUD (right) ────────────────────────────────────
        Image fillP2; TextMeshProUGUI hpP2, scoreP2;
        BuildSideHUD(cvGO, true, "P2", sprBg, sprFill, sprName,
            out fillP2, out hpP2, out scoreP2);

        // ── Timer ─────────────────────────────────────────────
        var tiRoot=new GameObject("TimerRoot"); tiRoot.transform.SetParent(cvGO.transform,false);
        var tiRT=tiRoot.AddComponent<RectTransform>();
        tiRT.anchorMin=new Vector2(0.5f,1); tiRT.anchorMax=new Vector2(0.5f,1);
        tiRT.pivot=new Vector2(0.5f,1); tiRT.anchoredPosition=new Vector2(0,-5); tiRT.sizeDelta=new Vector2(110,78);
        if(sprTimer!=null){var ti=tiRoot.AddComponent<Image>();ti.sprite=sprTimer;ti.type=Image.Type.Sliced;}
        var timerTMP=ATMP(tiRoot,"TimerText","99",new Vector2(0,-4),new Vector2(110,70),64f,Color.white,TextAlignmentOptions.Center);
        timerTMP.fontStyle=FontStyles.Bold;

        // ── Announcement ──────────────────────────────────────
        var annGO=new GameObject("AnnouncementText"); annGO.transform.SetParent(cvGO.transform,false);
        var annTMP=annGO.AddComponent<TextMeshProUGUI>();
        annTMP.text=""; annTMP.fontSize=72f; annTMP.color=Color.yellow;
        annTMP.alignment=TextAlignmentOptions.Center; annTMP.fontStyle=FontStyles.Bold;
        var annRT=annGO.GetComponent<RectTransform>();
        annRT.anchorMin=new Vector2(0.5f,0.5f); annRT.anchorMax=new Vector2(0.5f,0.5f);
        annRT.anchoredPosition=Vector2.zero; annRT.sizeDelta=new Vector2(1100,130);

        // ── Screen flash ──────────────────────────────────────
        var fGO=new GameObject("ScreenFlash"); fGO.transform.SetParent(cvGO.transform,false);
        var fImg=fGO.AddComponent<Image>(); fImg.color=new Color(1,1,1,0); StretchRT(fGO);
        var sf=cvGO.AddComponent<ScreenFlash>(); sf.flashImage=fImg;

        // ── Match-over panel ──────────────────────────────────
        var panGO=new GameObject("MatchOverPanel"); panGO.transform.SetParent(cvGO.transform,false);
        var panImg=panGO.AddComponent<Image>();
        panImg.sprite=sprPanel; panImg.type=Image.Type.Sliced; panImg.color=new Color(0,0,0,0.88f);
        StretchRT(panGO);
        var resTMP=ATMP(panGO,"ResultText","PLAYER 1\nWINS!",
            new Vector2(0,110),new Vector2(800,220),96f,Color.yellow,TextAlignmentOptions.Center);
        resTMP.fontStyle=FontStyles.Bold;
        ATMP(panGO,"HintText",
            "P1: A/D move  |  W/Space = jump  |  J = light  |  K = heavy\n"+
            "P2: Numpad4/6  |  Numpad8 = jump  |  Num0 = light  |  NumEnter = heavy",
            new Vector2(0,-10),new Vector2(1000,70),20f,Color.white,TextAlignmentOptions.Center);
        var rematch = MakeBtn(panGO,"RematchBtn","▶  PLAY AGAIN",sprBtnN,sprBtnH,new Vector2(-200,-140),new Vector2(280,65));
        var menu    = MakeBtn(panGO,"MenuBtn",   "⌂  MAIN MENU", sprBtnN,sprBtnH,new Vector2(0,-140),   new Vector2(240,65));
        var quit    = MakeBtn(panGO,"QuitBtn",   "✕  QUIT",       sprBtnN,sprBtnH,new Vector2(190,-140), new Vector2(180,65));
        panGO.SetActive(false);

        // Bottom hint
        var hGO=new GameObject("BottomHint"); hGO.transform.SetParent(cvGO.transform,false);
        var ht=hGO.AddComponent<TextMeshProUGUI>();
        ht.text="P1: A/D  W/Space=Jump  J=Light  K=Heavy              P2: Num4/6  Num8=Jump  Num0=Light  NumEnter=Heavy";
        ht.fontSize=16f; ht.color=new Color(1,1,1,0.4f); ht.alignment=TextAlignmentOptions.Center;
        var hRT=hGO.GetComponent<RectTransform>();
        hRT.anchorMin=new Vector2(0.5f,0); hRT.anchorMax=new Vector2(0.5f,0);
        hRT.pivot=new Vector2(0.5f,0); hRT.anchoredPosition=new Vector2(0,18); hRT.sizeDelta=new Vector2(1500,28);

        // ── UIManager ─────────────────────────────────────────
        var uimGO=new GameObject("UIManager"); uimGO.transform.SetParent(cvGO.transform,false);
        var uim=uimGO.AddComponent<UIManager>();
        uim.healthFillP1    = fillP1;
        uim.healthFillP2    = fillP2;
        uim.hpTextP1        = hpP1;
        uim.hpTextP2        = hpP2;
        uim.healthManagerP1 = hmP1;
        uim.healthManagerP2 = hmP2;
        uim.timerText       = timerTMP;
        uim.announcementText= annTMP;
        uim.p1ScoreText     = scoreP1;
        uim.p2ScoreText     = scoreP2;
        uim.matchOverPanel  = panGO;
        uim.resultText      = resTMP;
        uim.rematchButton   = rematch.GetComponent<Button>();
        uim.menuButton      = menu.GetComponent<Button>();
        uim.quitButton      = quit.GetComponent<Button>();
    }

    // Builds one player's HUD side using IMAGE FILL (not Slider)
    static void BuildSideHUD(GameObject cv, bool right, string id,
        Sprite bgSpr, Sprite fillSpr, Sprite nameSpr,
        out Image fillImg, out TextMeshProUGUI hpTMP, out TextMeshProUGUI scoreTMP)
    {
        float a=right?1f:0f, x=right?-14f:14f;

        // Name plate
        var nGO=new GameObject(id+"_Name"); nGO.transform.SetParent(cv.transform,false);
        if(nameSpr!=null){var ni=nGO.AddComponent<Image>();ni.sprite=nameSpr;ni.type=Image.Type.Sliced;}
        var nRT=nGO.GetComponent<RectTransform>();
        nRT.anchorMin=new Vector2(a,1);nRT.anchorMax=new Vector2(a,1);nRT.pivot=new Vector2(a,1);
        nRT.anchoredPosition=new Vector2(x,-8f);nRT.sizeDelta=new Vector2(240,30);
        ATMP(nGO,id+"_Label",right?"PLAYER  2":"PLAYER  1",
            new Vector2(right?-8f:8f,0),new Vector2(224,28),20f,
            right?new Color(1f,0.45f,0.45f):new Color(0.45f,0.85f,1f),
            right?TextAlignmentOptions.Right:TextAlignmentOptions.Left);

        // Health bar background
        var hbGO=new GameObject(id+"_HealthBar"); hbGO.transform.SetParent(cv.transform,false);
        var bgImg=hbGO.AddComponent<Image>();
        if(bgSpr!=null){bgImg.sprite=bgSpr;bgImg.type=Image.Type.Sliced;}
        else bgImg.color=new Color(0.15f,0.03f,0.03f);
        var hbRT=hbGO.GetComponent<RectTransform>();
        hbRT.anchorMin=new Vector2(a,1);hbRT.anchorMax=new Vector2(a,1);hbRT.pivot=new Vector2(a,1);
        hbRT.anchoredPosition=new Vector2(x,-42f);hbRT.sizeDelta=new Vector2(590,32);

        // Fill image — child of health bar bg, stretched inside with padding
        var fGO=new GameObject("Fill"); fGO.transform.SetParent(hbGO.transform,false);
        var fi=fGO.AddComponent<Image>();
        if(fillSpr!=null) fi.sprite=fillSpr;
        fi.color=new Color(0.1f,0.9f,0.1f);
        // CRITICAL: set fill type for image-based health bar
        fi.type=Image.Type.Filled;
        fi.fillMethod=Image.FillMethod.Horizontal;
        fi.fillOrigin=right?1:0; // P2 fills from right so it shrinks inward
        fi.fillAmount=1f;
        var fRT=fGO.GetComponent<RectTransform>();
        fRT.anchorMin=Vector2.zero; fRT.anchorMax=Vector2.one;
        fRT.offsetMin=new Vector2(3,3); fRT.offsetMax=new Vector2(-3,-3);

        // HP number label
        hpTMP=ATMP(hbGO,id+"_HP","100",
            new Vector2(right?8f:-8f,0),new Vector2(80,28),16f,Color.white,
            right?TextAlignmentOptions.Left:TextAlignmentOptions.Right);

        // Score dots
        var sGO=new GameObject(id+"_Score"); sGO.transform.SetParent(cv.transform,false);
        scoreTMP=sGO.AddComponent<TextMeshProUGUI>();
        scoreTMP.text=""; scoreTMP.fontSize=22f; scoreTMP.color=Color.yellow;
        scoreTMP.alignment=right?TextAlignmentOptions.Right:TextAlignmentOptions.Left;
        var sRT=sGO.GetComponent<RectTransform>();
        sRT.anchorMin=new Vector2(a,1);sRT.anchorMax=new Vector2(a,1);sRT.pivot=new Vector2(a,1);
        sRT.anchoredPosition=new Vector2(x,-78f);sRT.sizeDelta=new Vector2(130,26);

        fillImg=fi;
    }

    // =========================================================
    // ⑤ Physics2D Matrix
    // =========================================================
    [MenuItem("Tools/Arena Combat/\u2464 Fix Physics2D Layer Matrix")]
    public static void Step5_FixPhysics()
    {
        int g=LayerMask.NameToLayer("Ground"), p=LayerMask.NameToLayer("Player");
        if(g<0||p<0){Debug.LogError("Run Step ① first.");return;}
        Physics2D.IgnoreLayerCollision(p,g,false);
        Physics2D.IgnoreLayerCollision(p,p,true);
        Debug.Log("<color=lime>\u2713 Step 5: Physics matrix set.</color>");
    }

    // =========================================================
    // ⑥ Clean Stale Files
    // =========================================================
    [MenuItem("Tools/Arena Combat/\u2465 Clean Stale Files")]
    public static void Step6_Clean()
    {
        string[] stale={"Assets/Animations/AC_Player.controller","Assets/Animations/player_idle.anim",
            "Assets/Characters/Player/AC_Player.controller","Assets/InputSystem_Actions.inputactions",
            "Assets/Animations/AC_Player_OLD.controller","Assets/Animations/player_run.anim",
            "Assets/Animations/player_walk.anim"};
        int n=0;
        foreach(var f in stale) if(AssetDatabase.LoadAssetAtPath<Object>(f)!=null){AssetDatabase.DeleteAsset(f);n++;}
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"<color=lime>\u2713 Step 6: Removed {n} stale file(s).</color>");
    }

    // =========================================================
    // ⑦ Build Main Menu Scene
    // =========================================================
    [MenuItem("Tools/Arena Combat/\u2466 Build Main Menu Scene")]
    public static void Step7_BuildMainMenu()
    {
        if(!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets","Scenes");

        // Save current scene first
        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        // Create new scene
        var menuScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        menuScene.name = "MainMenu";

        // ── Camera setup ──────────────────────────────────────
        var cam = Camera.main;
        if(cam != null)
        {
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.12f, 1f);
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        // ── Canvas ────────────────────────────────────────────
        var cvGO = new GameObject("MainMenu_Canvas");
        var cv   = cvGO.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 10;
        var sc = cvGO.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
        cvGO.AddComponent<GraphicRaycaster>();

        // ── Background image ──────────────────────────────────
        Sprite menuBgSpr = LSpr(UI + "menu_bg.png");
        if(menuBgSpr != null)
        {
            var bgGO = new GameObject("MenuBackground"); bgGO.transform.SetParent(cvGO.transform, false);
            var bgImg = bgGO.AddComponent<Image>(); bgImg.sprite = menuBgSpr;
            bgImg.color = Color.white; StretchRT(bgGO);
        }
        else
        {
            // Solid colour fallback
            var bgGO = new GameObject("MenuBackground"); bgGO.transform.SetParent(cvGO.transform, false);
            var bgImg = bgGO.AddComponent<Image>(); bgImg.color = new Color(0.05f, 0.05f, 0.15f, 1f);
            StretchRT(bgGO);
        }

        // ── Title plate ───────────────────────────────────────
        var titleBg = new GameObject("TitleBg"); titleBg.transform.SetParent(cvGO.transform, false);
        Sprite titSpr = LSpr(UI + "title_plate.png");
        if(titSpr != null) { var ti = titleBg.AddComponent<Image>(); ti.sprite = titSpr; ti.type = Image.Type.Sliced; }
        else { var ti = titleBg.AddComponent<Image>(); ti.color = new Color(0.1f, 0.08f, 0.02f, 0.9f); }
        var titRT = titleBg.GetComponent<RectTransform>();
        titRT.anchorMin = new Vector2(0.5f, 1f); titRT.anchorMax = new Vector2(0.5f, 1f);
        titRT.pivot     = new Vector2(0.5f, 1f);
        titRT.anchoredPosition = new Vector2(0, -80); titRT.sizeDelta = new Vector2(900, 160);

        var titleTMP = ATMP(titleBg, "Title", "2D ARENA\nCOMBAT", Vector2.zero, new Vector2(880, 155),
            64f, new Color(1f, 0.85f, 0.2f), TextAlignmentOptions.Center);
        titleTMP.fontStyle = FontStyles.Bold;

        // ── Subtitle ──────────────────────────────────────────
        ATMP(cvGO, "Subtitle", "PUSL3190  ·  10952382  ·  Singith Kesara Wahalathanthri",
            new Vector2(0, -255), new Vector2(900, 40), 20f, new Color(1,1,1,0.5f), TextAlignmentOptions.Center)
            .transform.SetParent(cvGO.transform, false);
        // Re-anchor subtitle to top centre
        var subGO = cvGO.transform.Find("Subtitle")?.gameObject ?? cvGO;
        if (subGO != cvGO)
        {
            var sRT = subGO.GetComponent<RectTransform>();
            sRT.anchorMin = new Vector2(0.5f,1); sRT.anchorMax = new Vector2(0.5f,1); sRT.pivot = new Vector2(0.5f,1);
            sRT.anchoredPosition = new Vector2(0,-255);
        }

        // ── Divider ───────────────────────────────────────────
        Sprite divSpr = LSpr(UI + "divider.png");
        if(divSpr != null)
        {
            var divGO = new GameObject("Divider"); divGO.transform.SetParent(cvGO.transform, false);
            var divImg = divGO.AddComponent<Image>(); divImg.sprite = divSpr; divImg.type = Image.Type.Sliced;
            var divRT = divGO.GetComponent<RectTransform>();
            divRT.anchorMin = new Vector2(0.5f,0.5f); divRT.anchorMax = new Vector2(0.5f,0.5f);
            divRT.anchoredPosition = new Vector2(0, 130); divRT.sizeDelta = new Vector2(700, 4);
        }

        // ── Menu Panel (main buttons) ─────────────────────────
        var menuPanel = new GameObject("MenuPanel"); menuPanel.transform.SetParent(cvGO.transform, false);
        var mpRT = menuPanel.AddComponent<RectTransform>();
        mpRT.anchorMin = new Vector2(0.5f,0.5f); mpRT.anchorMax = new Vector2(0.5f,0.5f);
        mpRT.anchoredPosition = new Vector2(0, -20); mpRT.sizeDelta = new Vector2(500, 400);

        Sprite btnN = LSpr(UI + "menu_btn.png");
        Sprite btnH = LSpr(UI + "menu_btn_hover.png");

        var playBtn   = MakeBtn(menuPanel,"PlayBtn",   "▶   PLAY",         btnN,btnH,new Vector2(0,100), new Vector2(380,70));
        var ctrlBtn   = MakeBtn(menuPanel,"CtrlBtn",   "⌨   CONTROLS",    btnN,btnH,new Vector2(0,20),  new Vector2(380,70));
        var quitBtn   = MakeBtn(menuPanel,"QuitBtn",   "✕   QUIT",         btnN,btnH,new Vector2(0,-60), new Vector2(380,70));

        // ── Controls Panel (hidden by default) ────────────────
        var ctrlPanel = new GameObject("ControlsPanel"); ctrlPanel.transform.SetParent(cvGO.transform, false);
        var cpRT = ctrlPanel.AddComponent<RectTransform>();
        cpRT.anchorMin = new Vector2(0.5f,0.5f); cpRT.anchorMax = new Vector2(0.5f,0.5f);
        cpRT.anchoredPosition = new Vector2(0, -30); cpRT.sizeDelta = new Vector2(900, 500);

        // Controls panel background
        var cpBg = ctrlPanel.AddComponent<Image>(); cpBg.color = new Color(0.05f,0.05f,0.18f,0.95f);
        // Panel title
        ATMP(ctrlPanel,"CtrlTitle","CONTROLS",
            new Vector2(0,190),new Vector2(800,50),36f,Color.yellow,TextAlignmentOptions.Center);
        // P1 controls
        ATMP(ctrlPanel,"P1Controls",
            "<color=#73d9ff>PLAYER 1</color>\n"+
            "A / D       ──  Move Left / Right\n"+
            "W or Space  ──  Jump  (press twice for double jump)\n"+
            "S           ──  Fast Fall\n"+
            "J           ──  Light Attack\n"+
            "K           ──  Heavy Attack",
            new Vector2(-200, 30),new Vector2(400,250),22f,Color.white,TextAlignmentOptions.Left);
        // P2 controls
        ATMP(ctrlPanel,"P2Controls",
            "<color=#ff7373>PLAYER 2</color>\n"+
            "Numpad 4/6  ──  Move Left / Right\n"+
            "Numpad 8    ──  Jump  (press twice for double jump)\n"+
            "Numpad 5    ──  Fast Fall\n"+
            "Numpad 0    ──  Light Attack\n"+
            "Numpad Enter──  Heavy Attack",
            new Vector2(200, 30),new Vector2(400,250),22f,Color.white,TextAlignmentOptions.Left);
        // Back button
        var backBtn = MakeBtn(ctrlPanel,"BackBtn","← BACK",btnN,btnH,new Vector2(0,-200),new Vector2(280,60));
        ctrlPanel.SetActive(false);

        // ── Fade overlay ──────────────────────────────────────
        var fadeGO = new GameObject("FadeOverlay"); fadeGO.transform.SetParent(cvGO.transform, false);
        var fadeImg = fadeGO.AddComponent<Image>(); fadeImg.color = Color.black;
        StretchRT(fadeGO);
        var fadeGroup = fadeGO.AddComponent<CanvasGroup>();

        // ── MainMenuManager ───────────────────────────────────
        var mmGO = new GameObject("MainMenuManager"); 
        var mm = mmGO.AddComponent<MainMenuManager>();
        mm.menuPanel      = menuPanel;
        mm.controlsPanel  = ctrlPanel;
        mm.playButton     = playBtn.GetComponent<Button>();
        mm.controlsButton = ctrlBtn.GetComponent<Button>();
        mm.backButton     = backBtn.GetComponent<Button>();
        mm.quitButton     = quitBtn.GetComponent<Button>();
        mm.fadeGroup      = fadeGroup;
        mm.gameplaySceneName = "Gameplay";
        // Wire audio
        mm.clickSFX   = LoadAC(AUDIO+"SFX/10_UI_Menu_SFX/013_Confirm_03.wav");
        mm.hoverSFX   = LoadAC(AUDIO+"SFX/10_UI_Menu_SFX/001_Hover_01.wav");
        mm.menuMusicClip = LoadAC(AUDIO+"Music/10 Battle1 (8bit style)/Assets_for_Unity/8bit-Battle01_loop.ogg");
        var mAudio = mmGO.AddComponent<AudioSource>();
        mAudio.playOnAwake = false; mAudio.volume = 0.4f; mAudio.loop = true;
        mm.menuMusic = mAudio;

        // ── Save scene ────────────────────────────────────────
        string scenePath = SCENES + "MainMenu.unity";
        EditorSceneManager.SaveScene(menuScene, scenePath);

        // ── Update build settings ─────────────────────────────
        UpdateBuildSettings();

        Debug.Log("<color=lime>\u2713 Step 7: Main Menu scene saved to " + scenePath + "</color>");
        EditorUtility.DisplayDialog("Main Menu Built!",
            "MainMenu.unity saved to Assets/Scenes/\n\n"+
            "Build order:\n  Scene 0: MainMenu\n  Scene 1: Gameplay\n\n"+
            "Press Play on the MainMenu scene to test.", "Great!");
    }

    static void UpdateBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>();
        // MainMenu first
        scenes.Add(new EditorBuildSettingsScene(SCENES+"MainMenu.unity", true));
        // Gameplay second
        var gpPath = SCENES + "Gameplayscene.unity";
        if(AssetDatabase.LoadAssetAtPath<Object>(gpPath) != null)
            scenes.Add(new EditorBuildSettingsScene(gpPath, true));
        // Also try Gameplay.unity
        gpPath = SCENES + "Gameplay.unity";
        if(AssetDatabase.LoadAssetAtPath<Object>(gpPath) != null)
            scenes.Add(new EditorBuildSettingsScene(gpPath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // =========================================================
    // UI helpers
    // =========================================================
    static TextMeshProUGUI ATMP(GameObject parent, string name, string text,
        Vector2 apos, Vector2 size, float fs, Color col, TextAlignmentOptions align)
    {
        var go=new GameObject(name); go.transform.SetParent(parent.transform,false);
        var t=go.AddComponent<TextMeshProUGUI>();
        t.text=text;t.fontSize=fs;t.color=col;t.alignment=align;
        var rt=go.GetComponent<RectTransform>(); rt.anchoredPosition=apos; rt.sizeDelta=size;
        return t;
    }

    static GameObject MakeBtn(GameObject parent,string name,string label,
        Sprite normSpr,Sprite hovSpr,Vector2 apos,Vector2 size)
    {
        var go=new GameObject(name); go.transform.SetParent(parent.transform,false);
        var img=go.AddComponent<Image>();
        if(normSpr!=null){img.sprite=normSpr;img.type=Image.Type.Sliced;}
        else img.color=new Color(0.12f,0.12f,0.22f);
        var btn=go.AddComponent<Button>(); btn.targetGraphic=img;
        if(hovSpr!=null){var ss=btn.spriteState;ss.highlightedSprite=hovSpr;btn.spriteState=ss;btn.transition=Selectable.Transition.SpriteSwap;}
        var rt=go.GetComponent<RectTransform>(); rt.anchoredPosition=apos; rt.sizeDelta=size;
        var lbl=ATMP(go,"Label",label,Vector2.zero,size,26f,Color.white,TextAlignmentOptions.Center);
        lbl.fontStyle=FontStyles.Bold;
        return go;
    }

    static void StretchRT(GameObject go)
    {
        var rt=go.GetComponent<RectTransform>();
        rt.anchorMin=Vector2.zero; rt.anchorMax=Vector2.one; rt.offsetMin=rt.offsetMax=Vector2.zero;
    }

    static Sprite LSpr(string path)
        {var s=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(s==null)Debug.LogWarning("Missing sprite: "+path);return s;}

    static Sprite MakeSolidSpr(Color32 c)
    {
        var t=new Texture2D(4,4,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
        var p=new Color32[16]; for(int i=0;i<16;i++)p[i]=c; t.SetPixels32(p);t.Apply();
        return Sprite.Create(t,new Rect(0,0,4,4),Vector2.one*0.5f,4f);
    }

    static void WireAudio(AudioManager am)
    {
        am.lightSwingSFX    = LoadAC(AUDIO+"SFX/12_Player_Movement_SFX/56_Attack_03.wav");
        am.heavySwingSFX    = LoadAC(AUDIO+"SFX/10_Battle_SFX/22_Slash_04.wav");
        am.lightHitSFX      = LoadAC(AUDIO+"SFX/12_Player_Movement_SFX/61_Hit_03.wav");
        am.heavyHitSFX      = LoadAC(AUDIO+"SFX/10_Battle_SFX/15_Impact_flesh_02.wav");
        am.deathSFX         = LoadAC(AUDIO+"SFX/10_Battle_SFX/69_Enemy_death_01.wav");
        am.jumpSFX          = LoadAC(AUDIO+"SFX/12_Player_Movement_SFX/30_Jump_03.wav");
        am.landSFX          = LoadAC(AUDIO+"SFX/12_Player_Movement_SFX/45_Landing_01.wav");
        am.roundStartSFX    = LoadAC(AUDIO+"SFX/10_Battle_SFX/55_Encounter_02.wav");
        am.uiConfirmSFX     = LoadAC(AUDIO+"SFX/10_UI_Menu_SFX/013_Confirm_03.wav");
        am.uiDeclineSFX     = LoadAC(AUDIO+"SFX/10_UI_Menu_SFX/029_Decline_09.wav");
        am.battleMusicIntro = LoadAC(AUDIO+"Music/10 Battle1 (8bit style)/Assets_for_Unity/8bit-Battle01_intro.ogg");
        am.battleMusicLoop  = LoadAC(AUDIO+"Music/10 Battle1 (8bit style)/Assets_for_Unity/8bit-Battle01_loop.ogg");
    }
    static AudioClip LoadAC(string p)
        {var c=AssetDatabase.LoadAssetAtPath<AudioClip>(p);if(c==null)Debug.LogWarning("Audio missing: "+p);return c;}
}
#endif
