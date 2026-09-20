using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 에디터 메뉴 "Greed Bound > NPC 애니메이터 생성".
// Assets/Resources/NpcAnimators/npc_<이름>_<동작>.anim → 같은 폴더의 npc_<이름>.controller
//
// 상점 주인·모험가는 씬에 미리 놓인 오브젝트가 아니라 ShopManager 가 실행 중에 만든다.
// 그래서 컨트롤러도 Resources 에 두고 WorldGui.CreateSprite 가 실행 중에 붙인다.
// 대기 동작 하나뿐이라 상태 하나에 전환선 없이 끝난다.
public static class NpcAnimatorBuilder
{
    const string Folder = "Assets/Resources/NpcAnimators";

    [MenuItem("Greed Bound/NPC 애니메이터 생성")]
    static void Build()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            EditorUtility.DisplayDialog("NPC 애니메이터 생성",
                $"{Folder} 폴더가 없습니다.\n먼저 \"에셋 팩 시트 임포트\"를 실행하세요.", "확인");
            return;
        }

        List<AnimationClip> clips = AssetDatabase.FindAssets("t:AnimationClip", new[] { Folder })
            .Select(g => AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null && c.name.StartsWith("npc_"))
            .ToList();
        if (clips.Count == 0)
        {
            EditorUtility.DisplayDialog("NPC 애니메이터 생성", "NPC 클립이 없습니다.", "확인");
            return;
        }

        // npc_keeper_idle → npc_keeper 로 묶는다
        Dictionary<string, List<AnimationClip>> byNpc = new Dictionary<string, List<AnimationClip>>();
        foreach (AnimationClip clip in clips)
        {
            int cut = clip.name.LastIndexOf('_');
            if (cut <= 0) continue;
            string npc = clip.name.Substring(0, cut);
            if (!byNpc.TryGetValue(npc, out List<AnimationClip> list))
                byNpc[npc] = list = new List<AnimationClip>();
            list.Add(clip);
        }

        List<string> report = new List<string>();
        foreach (KeyValuePair<string, List<AnimationClip>> pair in byNpc.OrderBy(p => p.Key))
        {
            string path = $"{Folder}/{pair.Key}.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path)
                                            ?? AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            HashSet<string> wanted = new HashSet<string>();
            foreach (AnimationClip clip in pair.Value)
            {
                string stateName = clip.name.Substring(pair.Key.Length + 1);
                wanted.Add(stateName);
                AnimatorState state = machine.states.Select(s => s.state)
                    .FirstOrDefault(s => s != null && s.name == stateName) ?? machine.AddState(stateName);
                state.motion = clip;
                state.writeDefaultValues = false;
            }

            // 클립이 사라진 옛 상태는 지운다 (적 애니메이터와 같은 이유 — 빈 상태는 아무것도 안 나온다)
            foreach (AnimatorState dead in machine.states.Select(s => s.state)
                         .Where(s => s != null && !wanted.Contains(s.name)).ToList())
            {
                string deadName = dead.name;
                machine.RemoveState(dead);
                report.Add($"{pair.Key}: 옛 상태 제거 — {deadName}");
            }

            AnimatorState idle = machine.states.Select(s => s.state)
                .FirstOrDefault(s => s != null && s.name == "idle");
            if (idle != null) machine.defaultState = idle;
            EditorUtility.SetDirty(controller);
            report.Add($"{pair.Key}: {pair.Value.Count}개 ({string.Join(", ", wanted)})");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string message = string.Join("\n", report) + $"\n\n→ {Folder}";
        Debug.Log("NPC 애니메이터 생성\n" + message);
        EditorUtility.DisplayDialog("NPC 애니메이터 생성", message, "확인");
    }
}
