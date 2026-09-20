using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// 에디터 메뉴 "Greed Bound > 에셋 팩 시트 임포트".
// AI_Source/_sheets/*.png → Assets/Art/Sprites/... 시트 + Assets/Art/Animations/... 클립.
//
// AiSourceBatchImport 와 달리 그림을 전혀 건드리지 않는다. 팩 스프라이트는 이미 완성된
// 그림이라, 축소하거나 Endesga 32 로 팔레트를 맞추면 오히려 망가진다.
// 크기 맞춤·발 정렬·가로 한 줄 배치는 AI_Source/tools/pack_to_strip.py 가 미리 끝내 놓고,
// 여기서는 복사 → 슬라이스 → 클립 생성만 한다.
public static class PackSheetImport
{
    const string SourceFolder = "AI_Source/_sheets";      // 프로젝트 루트 기준 (Assets 밖)
    const string ManifestName = "sheets.json";

    [System.Serializable]
    class Sheet
    {
        public string name;
        public int frames;
        public float fps;
        public bool loop;
        public int cellWidth, cellHeight;
    }

    [System.Serializable]
    class Manifest { public Sheet[] sheets; }

    // 접두사로 들어갈 곳을 가른다. 목록에 없으면 적으로 본다.
    // NPC 는 실행 중에 Resources.Load 로 읽으므로 Resources 아래에 둔다.
    static (string sprites, string clips) TargetOf(string name)
    {
        if (name.StartsWith("npc_")) return ("Assets/Resources/Props", "Assets/Resources/NpcAnimators");
        if (name.StartsWith("player_")) return ("Assets/Art/Sprites/Player", "Assets/Art/Animations/Player");
        if (name.StartsWith("boss_")) return ("Assets/Art/Sprites/Bosses", "Assets/Art/Animations/Bosses");
        return ("Assets/Art/Sprites/Enemies", "Assets/Art/Animations/Enemies");
    }

    [MenuItem("Greed Bound/에셋 팩 시트 임포트")]
    public static void Run()
    {
        string root = Directory.GetParent(Application.dataPath).FullName;
        string srcDir = Path.Combine(root, SourceFolder.Replace('/', Path.DirectorySeparatorChar));
        string manifestPath = Path.Combine(srcDir, ManifestName);
        if (!File.Exists(manifestPath))
        {
            Debug.LogError($"에셋 팩 시트 임포트: 목록 파일이 없습니다 - {manifestPath}\n" +
                           "AI_Source/tools/pack_to_strip.py 를 먼저 돌리세요.");
            return;
        }

        Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
        if (manifest?.sheets == null || manifest.sheets.Length == 0)
        {
            Debug.LogError("에셋 팩 시트 임포트: 목록이 비어 있습니다.");
            return;
        }

        List<string> done = new List<string>();
        List<string> skipped = new List<string>();
        bool touchedPlayer = false;

        foreach (Sheet sheet in manifest.sheets)
        {
            string src = Path.Combine(srcDir, sheet.name + ".png");
            if (!File.Exists(src)) { skipped.Add(sheet.name + " (원본 없음)"); continue; }

            (string spriteDir, string clipDir) = TargetOf(sheet.name);
            Directory.CreateDirectory(spriteDir);
            Directory.CreateDirectory(clipDir);

            string spritePath = $"{spriteDir}/{sheet.name}.png";
            File.Copy(src, spritePath, true);
            AssetDatabase.ImportAsset(spritePath, ImportAssetOptions.ForceUpdate);

            AiSourceBatchImport.SliceSheet(spritePath, sheet.frames, sheet.cellWidth, sheet.cellHeight);
            AiSourceBatchImport.CreateClip(spritePath, $"{clipDir}/{sheet.name}.anim",
                                           sheet.frames, sheet.fps, sheet.loop);

            if (sheet.name.StartsWith("player_")) touchedPlayer = true;
            done.Add($"{sheet.name} ({sheet.frames}프레임, {sheet.cellWidth}x{sheet.cellHeight}, {sheet.fps}fps)");
        }

        List<string> removed = RemoveStale(manifest);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (touchedPlayer)
        {
            MethodInfo build = typeof(PlayerAnimatorBuilder).GetMethod("Build",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (build != null) build.Invoke(null, null);
            else Debug.LogWarning("PlayerAnimatorBuilder.Build를 찾지 못했습니다. 메뉴에서 직접 실행하세요.");
        }

        Debug.Log($"에셋 팩 시트 임포트 완료 — {done.Count}개\n" + string.Join("\n", done) +
                  (skipped.Count > 0 ? "\n\n건너뜀:\n" + string.Join("\n", skipped) : "") +
                  (removed.Count > 0 ? "\n\n지운 옛 파일: " + string.Join(", ", removed) : ""));
    }

    // 목록에서 빠진 옛 스프라이트·클립을 지운다.
    // 남겨 두면 애니메이터가 그걸 상태로 집어넣어, 이를테면 병사가 쓰지 않기로 한
    // 창·레이피어 공격을 계속 꺼내 쓴다. 같은 접두사를 쓰는 것만 건드린다.
    static List<string> RemoveStale(Manifest manifest)
    {
        HashSet<string> keep = new HashSet<string>();
        HashSet<string> prefixes = new HashSet<string>();
        foreach (Sheet sheet in manifest.sheets)
        {
            keep.Add(sheet.name);
            int cut = sheet.name.IndexOf('_');
            if (cut > 0) prefixes.Add(sheet.name.Substring(0, cut + 1));
        }

        List<string> removed = new List<string>();
        string[] folders =
        {
            "Assets/Art/Sprites/Player", "Assets/Art/Sprites/Enemies", "Assets/Art/Sprites/Bosses",
            "Assets/Art/Animations/Player", "Assets/Art/Animations/Enemies", "Assets/Art/Animations/Bosses",
            "Assets/Resources/Props", "Assets/Resources/NpcAnimators",
        };
        foreach (string folder in folders)
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;
            foreach (string guid in AssetDatabase.FindAssets("", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".png") && !path.EndsWith(".anim")) continue;

                string name = Path.GetFileNameWithoutExtension(path);
                if (keep.Contains(name)) continue;

                bool mine = false;
                foreach (string prefix in prefixes) if (name.StartsWith(prefix)) { mine = true; break; }
                if (!mine) continue;

                if (AssetDatabase.DeleteAsset(path)) removed.Add(name);
            }
        }
        return removed;
    }
}
