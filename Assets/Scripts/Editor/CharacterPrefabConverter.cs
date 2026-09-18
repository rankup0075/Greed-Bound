using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 에디터 메뉴 "Greed Bound > 캐릭터 프리팹 구조 변환".
// Player·Enemy 프리팹을 "루트 = 판정, 자식 Visual = 그림" 구조로 바꿔 저장 (Spec 8장 "캐릭터 판정·그림 분리").
// 실행 중에도 CharacterVisual이 인스턴스마다 같은 변환을 하므로 안 돌려도 동작은 같음.
// 프리팹에 Visual이 있어야 스프라이트·Animator를 에디터에서 붙일 수 있음.
public static class CharacterPrefabConverter
{
    [MenuItem("Greed Bound/캐릭터 프리팹 구조 변환")]
    static void Convert()
    {
        List<string> notes = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || (asset.GetComponent<Enemy>() == null && asset.GetComponent<PlayerHealth>() == null)) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = CharacterVisual.Migrate(root, immediate: true);
                if (root.GetComponent<CharacterVisual>() == null)
                {
                    root.AddComponent<CharacterVisual>();
                    changed = true;
                }
                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    notes.Add($"변환: {path}");
                }
                else notes.Add($"이미 변환됨: {path}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        string message = notes.Count > 0 ? string.Join("\n", notes) : "Player·Enemy 프리팹을 찾지 못했습니다.";
        Debug.Log("캐릭터 프리팹 구조 변환\n" + message);
        EditorUtility.DisplayDialog("캐릭터 프리팹 구조 변환", message, "확인");
    }
}
