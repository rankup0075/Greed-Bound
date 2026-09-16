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

    static readonly Color GroundColor = new Color(0.25f, 0.22f, 0.28f);
    static readonly Color PlatformColor = new Color(0.48f, 0.42f, 0.36f);

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

        // 지면: 윗면 y = GroundTop, 두께 2, 맵보다 양쪽으로 2씩 넓게
        GameObject ground = CreateBlock(root.transform, "Ground", square, GroundColor,
            new Vector2(0f, ArenaLayout.GroundTop - 1f), new Vector2(ArenaLayout.MapHalfWidth * 2f + 4f, 2f));
        Undo.AddComponent<BoxCollider2D>(ground);
        ground.GetComponent<SpriteRenderer>().sortingOrder = -10;

        // 보이지 않는 양끝 벽
        CreateWall(root.transform, "Wall_Left", -ArenaLayout.MapHalfWidth - WallThickness * 0.5f);
        CreateWall(root.transform, "Wall_Right", ArenaLayout.MapHalfWidth + WallThickness * 0.5f);

        // 발판 8개
        for (int i = 0; i < ArenaLayout.Platforms.Length; i++)
        {
            ArenaLayout.PlatformSpec spec = ArenaLayout.Platforms[i];
            GameObject platform = CreateBlock(root.transform, $"Platform_{i + 1}", square, PlatformColor,
                new Vector2(spec.x, spec.top - ArenaLayout.PlatformThickness * 0.5f),
                new Vector2(spec.width, ArenaLayout.PlatformThickness));
            Undo.AddComponent<OneWayPlatform>(platform);  // BoxCollider2D·PlatformEffector2D 자동 추가
            platform.GetComponent<SpriteRenderer>().sortingOrder = -5;
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
    // GameSystems 오브젝트에 RunState·GameLoopQueue·RoundManager·DebugHud를 붙이고 적 프리팹을 연결.
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
        if (systems.GetComponent<DebugHud>() == null)
        {
            Undo.AddComponent<DebugHud>(systems);
            notes.Add("DebugHud 추가");
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

    static GameObject CreateBlock(Transform parent, string name, Sprite sprite, Color color, Vector2 center, Vector2 size)
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

    static void CreateWall(Transform parent, string name, float x)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "전투 맵 생성");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(x, ArenaLayout.GroundTop + WallHeight * 0.5f - 2f, 0f);

        BoxCollider2D wall = go.AddComponent<BoxCollider2D>();
        wall.size = new Vector2(WallThickness, WallHeight);
    }

    // 오브젝트 발이 지면 윗면에 닿도록 배치 (Square 스프라이트 기준 세로 크기 = scale.y)
    static void PlaceOnGround(Transform target, float x)
    {
        Undo.RecordObject(target, "전투 맵 생성");
        float halfHeight = Mathf.Abs(target.localScale.y) * 0.5f;
        target.position = new Vector3(x, ArenaLayout.GroundTop + halfHeight + 0.01f, target.position.z);
    }

    static Sprite LoadSquareSprite()
    {
        string path = AssetDatabase.GUIDToAssetPath(SquareSpriteGuid);
        if (string.IsNullOrEmpty(path)) return null;
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
    }
}
