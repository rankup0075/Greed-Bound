using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 에디터 메뉴 "Greed Bound > 전투 맵 생성".
// ArenaLayout(Spec 5장) 값으로 지면·벽·발판 8개를 현재 씬에 만들고, 플레이어·카메라·스포너 위치를 맞춤.
// 만들어진 오브젝트는 일반 씬 오브젝트라 이후 직접 옮겨도 됨. Ctrl+Z로 한 번에 되돌릴 수 있음.
public static class BattleArenaBuilder
{
    const string SquareSpriteGuid = "311925a002f4447b3a28927169b83ea6";  // 2D Sprite 패키지 기본 Square
    const float WallThickness = 1f;
    const float WallHeight = 30f;

    internal static readonly Color GroundColor = new Color(0.25f, 0.22f, 0.28f);
    internal static readonly Color PlatformColor = new Color(0.48f, 0.42f, 0.36f);

    // 배경·지형 그림 (AI_Source/tools/build_environment.py 가 만든다. 없으면 예전처럼 색 사각형)
    const string EnvFolder = "Assets/Art/Sprites/Environment";
    // 배경 층: 파일 이름, 카메라 따라가는 비율, 화면 깊이, 세로 중심, 세로 높이(0 = 그림 높이 그대로), 밝기.
    // bg_far 는 세로로도 이음매가 없어 화면 위아래를 넘기도록 늘린다.
    // bg_walls 는 위아래를 이으면 자국이 보여 원래 높이 그대로 둔다 (남는 곳은 bg_far 가 채움).
    // 먼 층일수록 어둡게 — 원본대로 두면 먼 배경이 더 밝아 깊이감이 뒤집힌다.
    static readonly (string file, float follow, int order, float y, float height, float shade)[] BackgroundLayers =
    {
        ("bg_far",   0.85f, -40, 4f, 13f, 0.62f),
        ("bg_walls", 0.55f, -30, 4f,  0f, 0.85f),
    };

    // 로비·상점·튜토리얼(청록 성 내부). 이 씬들은 카메라가 더 넓다 — 세로 15u (UseWideView).
    // 그림 높이가 8.4u 뿐이라 화면을 한 장으로 못 덮는데, **세로로 이어 붙이면 안 된다** —
    // 같은 창문이 위아래로 두 번 보여 건물이 이상해진다(2026-09-20 사용자 보고).
    // 그래서 위쪽은 **무늬 없는 단색**으로 메우고, 창문은 눈높이에 한 줄만 둔다.
    internal static readonly (string file, float follow, int order, float y, float height, float shade)[] HallBackgroundLayers =
    {
        ("hall_fill", 1f,    -45, 6.4f, 17f, 1f),   // 단색 — 배경 맨 윗줄과 같은 색이라 경계가 안 보인다
        ("hall_bg",   0.72f, -40, 3.1f,  0f, 1f),   // 창문·기둥 한 줄
    };

    // 이전 테스트용으로 만든 오브젝트들 — 새 맵으로 교체
    static readonly string[] ReplacedObjectNames = { "BattleArena", "Ground", "Platform_A", "Platform_B", "Platform_C" };

    [MenuItem("Greed Bound/전투 맵 생성")]
    static void Build()
    {
        Sprite square = LoadSquareSprite();
        if (square == null)
        {
            EditorUtility.DisplayDialog("전투 맵 생성", "기본 Square 스프라이트를 찾지 못했습니다. (2D Sprite 패키지 확인)", "확인");
            return;
        }

        List<GameObject> replaced = ReplacedObjectNames
            .Select(GameObject.Find)
            .Where(go => go != null && go.transform.parent == null)
            .ToList();

        if (replaced.Count > 0)
        {
            string names = string.Join(", ", replaced.Select(go => go.name));
            if (!EditorUtility.DisplayDialog("전투 맵 생성", $"기존 오브젝트를 삭제하고 새 맵을 만듭니다.\n\n삭제: {names}", "진행", "취소"))
                return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("전투 맵 생성");

        foreach (GameObject go in replaced) Undo.DestroyObjectImmediate(go);

        GameObject root = new GameObject("BattleArena");
        Undo.RegisterCreatedObjectUndo(root, "전투 맵 생성");

        CreateBackground(root.transform, ArenaLayout.MapHalfWidth, BackgroundLayers);

        // 지면: 윗면 y = GroundTop, 두께 2, 맵보다 양쪽으로 2씩 넓게
        Vector2 groundSize = new Vector2(ArenaLayout.MapHalfWidth * 2f + 4f, 2f);
        GameObject ground = CreateTerrain(root.transform, "Ground", square, GroundColor, "ground",
            new Vector2(0f, ArenaLayout.GroundTop - 1f), groundSize, groundSize, -10);
        BoxCollider2D groundBox = Undo.AddComponent<BoxCollider2D>(ground);
        groundBox.size = groundSize;

        // 보이지 않는 양끝 벽
        CreateWall(root.transform, "Wall_Left", -ArenaLayout.MapHalfWidth - WallThickness * 0.5f);
        CreateWall(root.transform, "Wall_Right", ArenaLayout.MapHalfWidth + WallThickness * 0.5f);

        // 발판 8개
        for (int i = 0; i < ArenaLayout.Platforms.Length; i++)
        {
            ArenaLayout.PlatformSpec spec = ArenaLayout.Platforms[i];
            Vector2 hit = new Vector2(spec.width, ArenaLayout.PlatformThickness);
            // 그림은 판정보다 조금 두껍다(타일 한 칸 = 0.5u). **윗면을 맞추고 아래로 넘치게** 둔다 —
            // 판정을 그림에 맞춰 두껍게 하면 발판 아래 통과 높이가 줄어 보스가 끼기 시작한다
            Vector2 art = new Vector2(spec.width, PlatformArtHeight);
            GameObject platform = CreateTerrain(root.transform, $"Platform_{i + 1}", square, PlatformColor, "platform",
                new Vector2(spec.x, spec.top - ArenaLayout.PlatformThickness * 0.5f), hit, art, -5);
            Undo.AddComponent<OneWayPlatform>(platform);  // BoxCollider2D·PlatformEffector2D 자동 추가
            if (platform.TryGetComponent(out BoxCollider2D platformBox)) platformBox.size = hit;
        }

        List<string> notes = new List<string>();

        // 플레이어: 맵 중앙 지면 위에서 시작
        PlayerMovement player = Object.FindFirstObjectByType<PlayerMovement>();
        if (player != null)
        {
            PlaceOnGround(player.transform, 0f);
            notes.Add("Player → (0, 지면 위)");
        }

        // 카메라: 세로 고정 높이 + CameraFollow
        Camera cam = Camera.main;
        if (cam != null)
        {
            Undo.RecordObject(cam.transform, "전투 맵 생성");
            cam.transform.position = new Vector3(0f, ArenaLayout.CameraY, cam.transform.position.z);
            if (cam.GetComponent<CameraFollow>() == null) Undo.AddComponent<CameraFollow>(cam.gameObject);
            notes.Add("Main Camera → y 3.89, CameraFollow 추가");
        }

        // 허수아비: 새 지면 위로 (x는 유지)
        foreach (TrainingDummy dummy in Object.FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None))
        {
            PlaceOnGround(dummy.transform, dummy.transform.position.x);
            notes.Add($"{dummy.name} → 지면 위");
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;

        Debug.Log("전투 맵 생성 완료 — 지면, 벽 2개, 발판 8개\n" + string.Join("\n", notes));
    }

    // 에디터 메뉴 "Greed Bound > 게임 시스템 설정".
    // GameSystems 오브젝트에 RunState·GameLoopQueue·RoundManager·GameHud를 붙이고 적 프리팹을 연결.
    // 삭제된 스크립트(예: EnemyTestSpawner)가 남긴 "Missing Script" 컴포넌트도 정리.
    [MenuItem("Greed Bound/게임 시스템 설정")]
    static void SetupGameSystems()
    {
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("게임 시스템 설정");

        List<string> notes = new List<string>();

        GameObject systems = GameObject.Find("GameSystems");
        if (systems == null)
        {
            systems = new GameObject("GameSystems");
            Undo.RegisterCreatedObjectUndo(systems, "게임 시스템 설정");
            notes.Add("GameSystems 오브젝트 생성");
        }

        int removed = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(systems);
        if (removed > 0)
        {
            Undo.RegisterCompleteObjectUndo(systems, "게임 시스템 설정");
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(systems);
            notes.Add($"Missing Script 컴포넌트 {removed}개 제거");
        }

        // RoundManager가 RunState·GameLoopQueue를 RequireComponent로 함께 붙임
        RoundManager rounds = systems.GetComponent<RoundManager>();
        if (rounds == null)
        {
            rounds = Undo.AddComponent<RoundManager>(systems);
            notes.Add("RoundManager·RunState·GameLoopQueue 추가");
        }
        if (systems.GetComponent<GameHud>() == null && systems.GetComponent<DebugHud>() == null)
        {
            Undo.AddComponent<GameHud>(systems);
            notes.Add("GameHud 추가");
        }

        if (systems.GetComponent<BattleCardEffects>() == null)
        {
            Undo.AddComponent<BattleCardEffects>(systems);
            notes.Add("BattleCardEffects 추가 (행동 변화·증식 카드)");
        }
        if (systems.GetComponent<EnvironmentCardEffects>() == null)
        {
            Undo.AddComponent<EnvironmentCardEffects>(systems);
            notes.Add("EnvironmentCardEffects 추가 (환경 카드·죽음의 시계)");
        }

        CardSelection cardSelection = systems.GetComponent<CardSelection>();
        if (cardSelection == null)
        {
            cardSelection = Undo.AddComponent<CardSelection>(systems);
            notes.Add("CardSelection 추가");
        }
        if (cardSelection.library == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:CardLibrary");
            if (guids.Length > 0)
            {
                Undo.RecordObject(cardSelection, "게임 시스템 설정");
                cardSelection.library = AssetDatabase.LoadAssetAtPath<CardLibrary>(AssetDatabase.GUIDToAssetPath(guids[0]));
                notes.Add("CardLibrary 연결");
            }
            else
            {
                notes.Add("※ CardLibrary가 없음 — 메뉴 \"Greed Bound > 카드 데이터 생성\"을 먼저 실행하세요");
            }
        }

        if (rounds.enemyPrefab == null)
        {
            Enemy prefab = FindEnemyPrefab();
            if (prefab != null)
            {
                Undo.RecordObject(rounds, "게임 시스템 설정");
                rounds.enemyPrefab = prefab;
                notes.Add($"Enemy Prefab 연결: {AssetDatabase.GetAssetPath(prefab)}");
            }
            else
            {
                notes.Add("※ Enemy 컴포넌트가 붙은 프리팹을 찾지 못함 — RoundManager의 Enemy Prefab을 직접 연결하세요");
            }
        }

        // 한 씬에 하나만 있어야 하는 컴포넌트가 다른 오브젝트에 또 있는지 확인
        if (Object.FindObjectsByType<RoundManager>(FindObjectsSortMode.None).Length > 1)
            notes.Add("※ RoundManager가 여러 개 있습니다 — GameSystems의 것만 남기세요");

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(systems.scene);
        Selection.activeGameObject = systems;

        Debug.Log("게임 시스템 설정 완료\n" + string.Join("\n", notes));
    }

    static Enemy FindEnemyPrefab()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            Enemy enemy = go != null ? go.GetComponent<Enemy>() : null;
            if (enemy != null) return enemy;
        }
        return null;
    }

    // 발판 그림 한 칸 높이 (platform.png 18px ÷ PPU 36)
    const float PlatformArtHeight = 0.5f;

    // 배경·지형 그림을 불러오면서 임포트 설정을 확인한다.
    // SpriteRenderer 의 Tiled 모드는 메시가 **Full Rect** 여야 동작한다 — Tight 면 그림이
    // 반복되지 않고 늘어나거나 깨진다. 이 프로젝트 스프라이트는 전부 Tight 로 들어와 있어서
    // (임포트 규칙의 설정이 먹지 않았음) 여기서 고쳐 준다.
    static Sprite LoadEnv(string file)
    {
        string path = $"{EnvFolder}/{file}.png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) return null;

        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteMeshType != SpriteMeshType.FullRect)
            {
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
        }
        return sprite;
    }

    // 지형 한 덩이. 루트는 판정만(스케일 1), 그림은 자식 "Art" 가 그린다.
    // 그림이 판정보다 두꺼울 수 있으므로 **윗면을 맞추고** 남는 만큼 아래로 넘치게 둔다.
    internal static GameObject CreateTerrain(Transform parent, string name, Sprite fallback, Color fallbackColor,
                                    string envFile, Vector2 center, Vector2 hit, Vector2 art, int sortingOrder)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "전투 맵 생성");
        go.transform.SetParent(parent, false);
        go.transform.position = center;

        GameObject visual = new GameObject("Art");
        visual.transform.SetParent(go.transform, false);
        visual.transform.localPosition = new Vector3(0f, hit.y * 0.5f - art.y * 0.5f, 0f);

        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = sortingOrder;

        Sprite env = LoadEnv(envFile);
        if (env != null)
        {
            renderer.sprite = env;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.size = art;
        }
        else
        {
            // 그림이 아직 없으면 예전처럼 색 사각형을 늘려 쓴다
            renderer.sprite = fallback;
            renderer.color = fallbackColor;
            visual.transform.localScale = new Vector3(art.x, art.y, 1f);
        }
        return go;
    }

    // 패럴랙스 배경. 층마다 카메라를 다른 비율로 따라가 깊이감을 만든다.
    internal static void CreateBackground(Transform parent, float halfWidth,
        (string file, float follow, int order, float y, float height, float shade)[] layers)
    {
        GameObject root = new GameObject("Background");
        Undo.RegisterCreatedObjectUndo(root, "전투 맵 생성");
        root.transform.SetParent(parent, false);

        float width = halfWidth * 2f + 16f;   // 카메라가 끝까지 가도 비지 않게 여유
        foreach ((string file, float follow, int order, float y, float height, float shade) in layers)
        {
            Sprite sprite = LoadEnv(file);
            if (sprite == null) continue;

            GameObject layer = new GameObject($"{file}_{-order}");
            Undo.RegisterCreatedObjectUndo(layer, "전투 맵 생성");
            layer.transform.SetParent(root.transform, false);

            SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.size = new Vector2(width, height > 0f ? height : sprite.bounds.size.y);
            renderer.color = new Color(shade, shade, shade, 1f);
            renderer.sortingOrder = order;

            ParallaxLayer parallax = layer.AddComponent<ParallaxLayer>();
            parallax.follow = follow;
            parallax.SetOrigin(new Vector3(0f, y, 0f));
        }
    }

    internal static GameObject CreateBlock(Transform parent, string name, Sprite sprite, Color color, Vector2 center, Vector2 size)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "전투 맵 생성");
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);  // Square 스프라이트(1×1) 기준

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        return go;
    }

    internal static void CreateWall(Transform parent, string name, float x)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "전투 맵 생성");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(x, ArenaLayout.GroundTop + WallHeight * 0.5f - 2f, 0f);

        BoxCollider2D wall = go.AddComponent<BoxCollider2D>();
        wall.size = new Vector2(WallThickness, WallHeight);
    }

    // 오브젝트 발이 지면 윗면에 닿도록 배치. 판정 크기는 BoxCollider2D(루트 스케일 1 구조), 없으면 Square 스프라이트 기준 scale.y
    internal static void PlaceOnGround(Transform target, float x)
    {
        Undo.RecordObject(target, "전투 맵 생성");
        BoxCollider2D box = target.GetComponent<BoxCollider2D>();
        float halfHeight = box != null
            ? (box.size.y * 0.5f - box.offset.y) * Mathf.Abs(target.localScale.y)
            : Mathf.Abs(target.localScale.y) * 0.5f;
        target.position = new Vector3(x, ArenaLayout.GroundTop + halfHeight + 0.01f, target.position.z);
    }

    internal static Sprite LoadSquareSprite()
    {
        string path = AssetDatabase.GUIDToAssetPath(SquareSpriteGuid);
        if (string.IsNullOrEmpty(path)) return null;
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
    }
}
