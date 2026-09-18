using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 에디터 메뉴 "Greed Bound > 플레이어 애니메이터 생성" (Spec 8장 "플레이어 애니메이션 상태").
// Assets/Art/Animations/Player/player_*.anim → Player.controller (상태 이름 = "player_" 뺀 이름, 전환선 없음 — PlayerAnimation이 Play)
// → Player 프리팹의 Visual에 Animator 연결, 기본 스프라이트를 대기 첫 프레임으로, 루트에 PlayerAnimation.
// 동작 클립을 새로 저장할 때마다 다시 실행 (기존 상태는 클립만 갱신).
public static class PlayerAnimatorBuilder
{
    const string ClipFolder = "Assets/Art/Animations/Player";
    const string ControllerPath = ClipFolder + "/Player.controller";

    [MenuItem("Greed Bound/플레이어 애니메이터 생성")]
    static void Build()
    {
        List<AnimationClip> clips = AssetDatabase.FindAssets("t:AnimationClip", new[] { ClipFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null && c.name.StartsWith("player_"))
            .OrderBy(c => c.name)
            .ToList();
        if (clips.Count == 0)
        {
            EditorUtility.DisplayDialog("플레이어 애니메이터 생성", $"{ClipFolder}에 player_로 시작하는 클립이 없습니다.\n도트 변환기에서 \"애니메이션 클립 만들기\"를 켜고 저장하세요.", "확인");
            return;
        }

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                                        ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        List<string> notes = new List<string>();
        foreach (AnimationClip clip in clips)
        {
            string stateName = clip.name.Substring("player_".Length);
            AnimatorState state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == stateName);
            if (state == null)
            {
                state = machine.AddState(stateName);
                notes.Add($"상태 추가: {stateName}");
            }
            state.motion = clip;
            state.writeDefaultValues = false;
        }
        AnimatorState idle = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == PlayerAnimation.Idle);
        if (idle != null) machine.defaultState = idle;
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        // 플레이어 프리팹에 연결
        string prefabPath = SideSceneBuilder.PlayerPrefabPath;
        if (!File.Exists(prefabPath))
        {
            EditorUtility.DisplayDialog("플레이어 애니메이터 생성", $"Controller는 만들었지만 플레이어 프리팹({prefabPath})이 없습니다.", "확인");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            CharacterVisual.Migrate(root, immediate: true);
            if (root.GetComponent<CharacterVisual>() == null) root.AddComponent<CharacterVisual>();
            if (root.GetComponent<PlayerAnimation>() == null) root.AddComponent<PlayerAnimation>();

            Transform visual = root.transform.Find(CharacterVisual.VisualName);
            Animator animator = visual.GetComponent<Animator>() ?? visual.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // 에디터·첫 프레임에도 도트가 보이게 기본 스프라이트를 대기 첫 프레임으로
            AnimationClip idleClip = clips.FirstOrDefault(c => c.name == "player_" + PlayerAnimation.Idle) ?? clips[0];
            Sprite firstSprite = FirstSprite(idleClip);
            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            if (firstSprite != null && renderer != null)
            {
                renderer.sprite = firstSprite;
                renderer.color = Color.white;
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        string message = $"상태 {clips.Count}개: {string.Join(", ", clips.Select(c => c.name.Substring(7)))}\n" +
                         (notes.Count > 0 ? string.Join("\n", notes) + "\n" : "") +
                         $"{ControllerPath}\n→ {prefabPath}의 Visual에 연결";
        Debug.Log("플레이어 애니메이터 생성\n" + message);
        EditorUtility.DisplayDialog("플레이어 애니메이터 생성", message, "확인");
    }

    static Sprite FirstSprite(AnimationClip clip)
    {
        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            if (keys.Length > 0 && keys[0].value is Sprite sprite) return sprite;
        }
        return null;
    }
}
