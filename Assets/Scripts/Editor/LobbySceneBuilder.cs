using UnityEditor;
using UnityEngine;

// 에디터 메뉴 "Greed Bound > 로비 씬 생성" (Spec 6-3장 로비, 9장 씬 구조).
// 전투 씬을 연 상태에서 실행. 공용 절차(SideSceneBuilder)로 LobbyScene을 만들고
// LobbyLayout 값으로 지면·벽·2층 발판, LobbySystems(LobbyManager) 생성. 빌드 설정 맨 앞(게임 시작 씬)에 등록.
// 제단·포탈은 LobbyManager가 플레이 시작 시 만듦.
public static class LobbySceneBuilder
{
    const string Title = "로비 씬 생성";
    const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";

    [MenuItem("Greed Bound/로비 씬 생성")]
    static void Build()
    {
        SideSceneBuilder.Context context = SideSceneBuilder.Begin(Title, LobbyScenePath);
        if (context == null) return;

        SideSceneBuilder.BuildTerrain(context, "LobbyArena", LobbyLayout.HalfWidth, LobbyLayout.Platforms, LobbyLayout.PlayerStartX);

        GameObject systems = new GameObject("LobbySystems");
        systems.AddComponent<LobbyManager>().battleSceneName = context.battleName;

        SideSceneBuilder.Finish(context, Title, firstInBuild: true);
    }
}
