using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 전투 씬을 바탕으로 걸어다니는 부속 씬(상점·로비)을 만드는 공용 절차.
//   1. 씬의 Player를 프리팹(Assets/Prefabs/Player.prefab)으로 저장하고 연결 — 모든 씬이 같은 플레이어를 쓰도록
//   2. 전투 씬을 복사해 카메라·조명·플레이어만 남김
//   3. 평지 지형(지면·벽·발판), 카메라 경계, 플레이어 위치
//   4. 저장 → 빌드 설정 등록 → 전투 씬으로 돌아옴
public static class SideSceneBuilder
{
    public const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";

    // 복사본에서 남길 오브젝트 판별 (이 컴포넌트가 하나라도 있으면 유지)
    static readonly HashSet<string> KeptComponentTypes = new HashSet<string> { "Camera", "Light2D", "Volume", "PlayerHealth" };

    public class Context
    {
        public string battleName;
        public string battlePath;
        public string targetPath;
        public Scene scene;
        public readonly List<string> notes = new List<string>();
    }

    // 1~2단계. 실패하거나 취소하면 null. 성공하면 새 씬이 열린 상태
    public static Context Begin(string title, string targetPath)
    {
        Scene battleScene = SceneManager.GetActiveScene();
        // 다른 씬을 열면 Scene 값의 name·path가 비어버리므로 미리 문자열로 저장
        Context context = new Context { battleName = battleScene.name, battlePath = battleScene.path, targetPath = targetPath };

        if (Object.FindFirstObjectByType<RoundManager>() == null || string.IsNullOrEmpty(context.battlePath))
        {
            EditorUtility.DisplayDialog(title, "전투 씬(RoundManager가 있는 저장된 씬)을 연 상태에서 실행하세요.", "확인");
            return null;
        }
        PlayerHealth player = Object.FindFirstObjectByType<PlayerHealth>();
        if (player == null)
        {
            EditorUtility.DisplayDialog(title, "전투 씬에 Player(PlayerHealth)가 없습니다.", "확인");
            return null;
        }

        bool isPrefab = PrefabUtility.IsPartOfPrefabInstance(player.gameObject);
        bool overwrite = File.Exists(targetPath);
        string message = $"전투 씬을 저장하고 {Path.GetFileNameWithoutExtension(targetPath)}을(를) 만듭니다.\n\n" +
                         (isPrefab ? "" : $"· Player를 프리팹으로 저장하고 씬의 Player와 연결합니다 ({PlayerPrefabPath})\n") +
                         (overwrite ? $"· 기존 {targetPath}을(를) 덮어씁니다\n" : "") +
                         "· 빌드 설정에 등록합니다";
        if (!EditorUtility.DisplayDialog(title, message, "진행", "취소")) return null;

        if (!isPrefab)
        {
            string path = File.Exists(PlayerPrefabPath) ? AssetDatabase.GenerateUniqueAssetPath(PlayerPrefabPath) : PlayerPrefabPath;
            PrefabUtility.SaveAsPrefabAssetAndConnect(player.gameObject, path, InteractionMode.AutomatedAction);
            context.notes.Add($"Player 프리팹 저장·연결: {path}");
        }
        EditorSceneManager.SaveScene(battleScene);

        if (overwrite) AssetDatabase.DeleteAsset(targetPath);
        if (!AssetDatabase.CopyAsset(context.battlePath, targetPath))
        {
            EditorUtility.DisplayDialog(title, "씬 복사에 실패했습니다.", "확인");
            return null;
        }
        context.scene = EditorSceneManager.OpenScene(targetPath, OpenSceneMode.Single);

        foreach (GameObject root in context.scene.GetRootGameObjects())
        {
            bool keep = root.GetComponentsInChildren<Component>(true)
                .Any(c => c != null && KeptComponentTypes.Contains(c.GetType().Name));
            if (!keep) Object.DestroyImmediate(root);
        }
        return context;
    }

    // 3단계: 지면·벽·발판, 카메라 경계, 플레이어 위치
    public static void BuildTerrain(Context context, string rootName, float halfWidth, ArenaLayout.PlatformSpec[] platforms, float playerStartX)
    {
        Sprite square = BattleArenaBuilder.LoadSquareSprite();
        GameObject root = new GameObject(rootName);
        if (square != null)
        {
            GameObject ground = BattleArenaBuilder.CreateBlock(root.transform, "Ground", square, BattleArenaBuilder.GroundColor,
                new Vector2(0f, ArenaLayout.GroundTop - 1f), new Vector2(halfWidth * 2f + 4f, 2f));
            ground.AddComponent<BoxCollider2D>();
            ground.GetComponent<SpriteRenderer>().sortingOrder = -10;

            for (int i = 0; i < platforms.Length; i++)
            {
                ArenaLayout.PlatformSpec spec = platforms[i];
                GameObject platform = BattleArenaBuilder.CreateBlock(root.transform, $"Platform_{i + 1}", square, BattleArenaBuilder.PlatformColor,
                    new Vector2(spec.x, spec.top - ArenaLayout.PlatformThickness * 0.5f),
                    new Vector2(spec.width, ArenaLayout.PlatformThickness));
                platform.AddComponent<OneWayPlatform>();
                platform.GetComponent<SpriteRenderer>().sortingOrder = -5;
            }
        }
        else
        {
            context.notes.Add("※ 기본 Square 스프라이트를 찾지 못해 지형을 만들지 못했습니다 (2D Sprite 패키지 확인)");
        }
        BattleArenaBuilder.CreateWall(root.transform, "Wall_Left", -halfWidth - 0.5f);
        BattleArenaBuilder.CreateWall(root.transform, "Wall_Right", halfWidth + 0.5f);

        PlayerHealth player = Object.FindFirstObjectByType<PlayerHealth>();
        if (player != null) BattleArenaBuilder.PlaceOnGround(player.transform, playerStartX);

        Camera cam = Camera.main;
        CameraFollow follow = cam != null ? cam.GetComponent<CameraFollow>() : null;
        if (follow != null)
        {
            follow.mapMinX = -halfWidth;
            follow.mapMaxX = halfWidth;
            EditorUtility.SetDirty(follow);
        }
        else
        {
            context.notes.Add("※ Main Camera에 CameraFollow가 없습니다 — 전투 맵 생성 메뉴를 먼저 실행했는지 확인하세요");
        }
    }

    // 4단계. firstInBuild = true면 빌드 설정 맨 앞(게임 시작 씬)에 둠
    public static void Finish(Context context, string title, bool firstInBuild)
    {
        EditorSceneManager.MarkSceneDirty(context.scene);
        EditorSceneManager.SaveScene(context.scene);
        context.notes.Add($"씬 저장: {context.targetPath}");

        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.Where(s => s.path != context.targetPath).ToList();
        if (!scenes.Any(s => s.path == context.battlePath)) scenes.Insert(0, new EditorBuildSettingsScene(context.battlePath, true));
        EditorBuildSettingsScene added = new EditorBuildSettingsScene(context.targetPath, true);
        if (firstInBuild) scenes.Insert(0, added);
        else scenes.Add(added);
        foreach (EditorBuildSettingsScene s in scenes)
        {
            if (s.path == context.battlePath || s.path == context.targetPath) s.enabled = true;
        }
        EditorBuildSettings.scenes = scenes.ToArray();
        context.notes.Add(firstInBuild ? "빌드 설정 맨 앞(시작 씬)에 등록" : "빌드 설정에 등록");

        EditorSceneManager.OpenScene(context.battlePath, OpenSceneMode.Single);
        Debug.Log($"{title} 완료\n" + string.Join("\n", context.notes));
    }
}
