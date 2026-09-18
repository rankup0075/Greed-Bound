using UnityEditor;
using UnityEngine;

// 에디터 메뉴 "Greed Bound > 상점 씬 생성" (Spec 7장, 9장 씬 구조).
// 전투 씬을 연 상태에서 실행. 공용 절차(SideSceneBuilder)로 ShopScene을 만들고
// ShopLayout 값으로 지면·벽·발판 2개, ShopSystems(RunState·ShopManager·GameHud) 생성.
// 좌판·NPC·포탈은 ShopManager가 플레이 시작 시 만듦.
public static class ShopSceneBuilder
{
    const string Title = "상점 씬 생성";
    const string ShopScenePath = "Assets/Scenes/ShopScene.unity";

    [MenuItem("Greed Bound/상점 씬 생성")]
    static void Build()
    {
        SideSceneBuilder.Context context = SideSceneBuilder.Begin(Title, ShopScenePath);
        if (context == null) return;

        SideSceneBuilder.BuildTerrain(context, "ShopArena", ShopLayout.HalfWidth, ShopLayout.Platforms, ShopLayout.PlayerStartX);

        GameObject systems = new GameObject("ShopSystems");
        systems.AddComponent<RunState>();
        systems.AddComponent<ShopManager>().battleSceneName = context.battleName;
        systems.AddComponent<GameHud>();

        SideSceneBuilder.Finish(context, Title, firstInBuild: false);
    }
}
