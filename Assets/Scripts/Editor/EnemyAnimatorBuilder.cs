using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 에디터 메뉴 "Greed Bound > 적 애니메이터 생성".
// Assets/Art/Animations/{Enemies,Bosses}/<접두사>_*.anim → Resources/EnemyAnimators/<접두사>.controller
//
// 적은 종류가 7가지인데 프리팹은 하나뿐이라(RoundManager.enemyPrefab), 프리팹에 컨트롤러를
// 미리 박아 둘 수 없다. 그래서 Resources 에 넣고 EnemyAnimation 이 종류를 보고 실행 중에 고른다.
// 상태 이름은 접두사를 뺀 이름(idle·run·attack1…), 전환선은 없다 — EnemyAnimation 이 Play 로 직접 넘긴다.
public static class EnemyAnimatorBuilder
{
    static readonly string[] ClipFolders = { "Assets/Art/Animations/Enemies", "Assets/Art/Animations/Bosses" };
    const string OutputFolder = "Assets/Resources/EnemyAnimators";

    [MenuItem("Greed Bound/적 애니메이터 생성")]
    static void Build()
    {
        string[] folders = ClipFolders.Where(AssetDatabase.IsValidFolder).ToArray();
        if (folders.Length == 0)
        {
            EditorUtility.DisplayDialog("적 애니메이터 생성",
                string.Join("\n", ClipFolders) + "\n위 폴더가 없습니다.\n먼저 \"에셋 팩 시트 임포트\"를 실행하세요.", "확인");
            return;
        }

        List<AnimationClip> clips = AssetDatabase.FindAssets("t:AnimationClip", folders)
            .Select(g => AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null && c.name.Contains("_"))
            .ToList();
        if (clips.Count == 0)
        {
            EditorUtility.DisplayDialog("적 애니메이터 생성", "적 클립이 없습니다.", "확인");
            return;
        }

        // 종류별로 실제 쓰이는 접두사만 만든다 (아직 그림이 없는 종류는 임시 사각형으로 남음)
        HashSet<string> wanted = new HashSet<string>();
        foreach (EnemyType type in System.Enum.GetValues(typeof(EnemyType)))
            wanted.Add(EnemyAnimation.PrefixOf(type));

        Directory.CreateDirectory(OutputFolder);
        List<string> report = new List<string>();
        List<string> missing = new List<string>();

        foreach (string prefix in wanted.OrderBy(p => p))
        {
            List<AnimationClip> mine = clips
                .Where(c => c.name.StartsWith(prefix + "_"))
                .OrderBy(c => c.name)
                .ToList();
            if (mine.Count == 0) { missing.Add(prefix); continue; }

            string path = $"{OutputFolder}/{prefix}.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path)
                                            ?? AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            HashSet<string> wantedStates = new HashSet<string>();
            foreach (AnimationClip clip in mine)
            {
                string stateName = clip.name.Substring(prefix.Length + 1);
                wantedStates.Add(stateName);
                AnimatorState state = machine.states.Select(s => s.state)
                    .FirstOrDefault(s => s.name == stateName) ?? machine.AddState(stateName);
                state.motion = clip;
                state.writeDefaultValues = false;
            }

            // 클립이 사라진 옛 상태를 지운다. 남겨 두면 상태는 있는데 재생할 그림이 없어서
            // 그 번호가 뽑혔을 때 **아무 동작도 나오지 않는다**
            // (병사 기본 공격·마법사 공격이 사라진 원인)
            foreach (AnimatorState dead in machine.states.Select(s => s.state)
                         .Where(s => s != null && !wantedStates.Contains(s.name)).ToList())
            {
                string deadName = dead.name;   // RemoveState 가 객체를 파괴하므로 이름을 먼저 받아 둔다
                machine.RemoveState(dead);
                report.Add($"{prefix}: 옛 상태 제거 — {deadName}");
            }

            AnimatorState idle = machine.states.Select(s => s.state)
                .FirstOrDefault(s => s != null && s.name == "idle");
            if (idle != null) machine.defaultState = idle;
            EditorUtility.SetDirty(controller);

            report.Add($"{prefix}: {mine.Count}개 ({string.Join(", ", mine.Select(c => c.name.Substring(prefix.Length + 1)))})");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string message = string.Join("\n", report) +
                         (missing.Count > 0 ? $"\n\n클립 없음(임시 사각형 유지): {string.Join(", ", missing)}" : "") +
                         $"\n\n→ {OutputFolder}";
        Debug.Log("적 애니메이터 생성\n" + message);
        EditorUtility.DisplayDialog("적 애니메이터 생성", message, "확인");
    }
}
