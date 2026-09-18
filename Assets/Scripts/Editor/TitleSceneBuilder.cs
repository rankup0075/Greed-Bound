using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 에디터 메뉴 "Greed Bound > 타이틀 씬 생성" (Spec 9장 "타이틀 화면").
// 카메라 + TitleSystems(TitleScreen)만 있는 가벼운 씬을 만들고 빌드 설정 맨 앞(게임 시작 씬)에 등록한다.
// 실행 후에는 원래 열려 있던 씬으로 돌아온다.
public static class TitleSceneBuilder
{
    const string Title = "타이틀 씬 생성";
    const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
    const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";

    [MenuItem("Greed Bound/타이틀 씬 생성")]
    static void Build()
    {
        string previousScenePath = SceneManager.GetActiveScene().path;
        bool overwrite = File.Exists(TitleScenePath);
        string message = $"{TitleScenePath}을(를) 만들고 빌드 설정 맨 앞(게임 시작 씬)에 등록합니다.\n\n" +
                         (overwrite ? "· 기존 타이틀 씬을 덮어씁니다\n" : "") +
                         "· 열려 있는 씬은 저장 여부를 먼저 묻습니다";
        if (!EditorUtility.DisplayDialog(Title, message, "진행", "취소")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.06f, 0.05f, 0.09f);
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);

        GameObject systems = new GameObject("TitleSystems");
        TitleScreen title = systems.AddComponent<TitleScreen>();
        title.lobbySceneName = Path.GetFileNameWithoutExtension(LobbyScenePath);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, TitleScenePath);

        // 빌드 설정: 타이틀을 맨 앞으로, 나머지 순서는 그대로
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.Where(s => s.path != TitleScenePath).ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(TitleScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();

        List<string> notes = new List<string>
        {
            $"씬 저장: {TitleScenePath}",
            "빌드 설정 맨 앞(시작 씬)에 등록",
            $"게임 시작 → {title.lobbySceneName}",
        };
        if (!scenes.Any(s => s.path == LobbyScenePath))
            notes.Add("※ 로비 씬이 빌드 설정에 없습니다 — Greed Bound > 로비 씬 생성을 먼저 실행하세요");

        if (!string.IsNullOrEmpty(previousScenePath)) EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);

        Debug.Log($"{Title} 완료\n" + string.Join("\n", notes));
        EditorUtility.DisplayDialog(Title, string.Join("\n", notes), "확인");
    }
}
