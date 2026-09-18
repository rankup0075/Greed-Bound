using UnityEditor;
using UnityEngine;

// 에디터 메뉴 "Greed Bound > 튜토리얼 씬 생성" (Spec 9장 "튜토리얼 씬 — Unity 구현 규칙").
// 전투 씬을 연 상태에서 실행. 공용 절차(SideSceneBuilder)로 TutorialScene을 만들고
// TutorialLayout 값으로 지면·벽·발판, TutorialSystems(TutorialManager + GameHud) 생성. 빌드 설정 뒤에 등록(시작 씬은 타이틀).
// 허수아비·표적·기둥·포탈은 TutorialManager가 플레이 시작 시 만든다.
public static class TutorialSceneBuilder
{
    const string Title = "튜토리얼 씬 생성";
    const string TutorialScenePath = "Assets/Scenes/TutorialScene.unity";

    [MenuItem("Greed Bound/튜토리얼 씬 생성")]
    static void Build()
    {
        // 연습용 적에 쓸 프리팹은 전투 씬이 지워지기 전에 챙겨 둔다
        RoundManager rounds = Object.FindFirstObjectByType<RoundManager>();
        Enemy enemyPrefab = rounds != null ? rounds.enemyPrefab : null;

        SideSceneBuilder.Context context = SideSceneBuilder.Begin(Title, TutorialScenePath);
        if (context == null) return;

        SideSceneBuilder.BuildTerrain(context, "TutorialArena", TutorialLayout.HalfWidth, TutorialLayout.Platforms, TutorialLayout.PlayerStartX);

        GameObject systems = new GameObject("TutorialSystems");
        TutorialManager manager = systems.AddComponent<TutorialManager>();
        manager.enemyPrefab = enemyPrefab;
        systems.AddComponent<GameHud>();   // 체력·단검·대쉬·스킬 쿨 표시를 실전과 같게

        if (enemyPrefab == null) context.notes.Add("※ 전투 씬 RoundManager에 Enemy Prefab이 없어 연습용 적을 넣지 못했습니다");

        SideSceneBuilder.Finish(context, Title, firstInBuild: false);
    }
}
