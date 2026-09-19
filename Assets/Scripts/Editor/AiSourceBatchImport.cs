using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// 에디터 메뉴 "Greed Bound > AI 원본 일괄 변환" (md/GreedBound_Art_Guide.md 2장 제작 흐름).
// AI_Source/Player/*.png(Assets 밖, 96x76 가로 한 줄) → 도트 변환기와 같은 처리로
// Assets/Art/Sprites/Player 시트 + Assets/Art/Animations/Player 클립을 만들고 애니메이터까지 재생성.
// 도트 변환기 창을 하나씩 여는 대신 한 번에 돌리는 용도. 사람이 보는 설정은 아래 Job 표에 있음.
public static class AiSourceBatchImport
{
    const string SourceFolder = "AI_Source/Player";          // 프로젝트 루트 기준 (Assets 밖)
    const string SpriteFolder = "Assets/Art/Sprites/Player";
    const string ClipFolder = "Assets/Art/Animations/Player";
    const int PixelsPerUnit = 36;                             // PixelConverterWindow와 같은 값

    // bodyHeight는 "기준 프레임의 실제 높이"를 그대로 적어 배율을 1.0으로 둔다.
    // 생성 단계에서 이미 몸 50px 기준으로 포즈를 잡았으므로 여기서 다시 키우면 프레임마다 크기가 달라진다.
    struct Job
    {
        public string name;
        public int frames, referenceFrame, bodyHeight;
        public bool lockFeet, loop;
        public float fps;
        public Job(string name, int frames, int referenceFrame, int bodyHeight, bool lockFeet, bool loop, float fps)
        {
            this.name = name; this.frames = frames; this.referenceFrame = referenceFrame;
            this.bodyHeight = bodyHeight; this.lockFeet = lockFeet; this.loop = loop; this.fps = fps;
        }
    }

    static readonly Job[] Jobs =
    {
        //       이름                프레임 기준칸 몸높이 발고정 루프   fps
        new Job("player_run",        8,    0,    53,   true,  true,  12f),
        new Job("player_attack2",    6,    5,    50,   false, false, 12f),
        new Job("player_jump",       3,    0,    53,   false, false, 10f),
        new Job("player_hurt",       3,    0,    47,   false, false, 12f),
        new Job("player_death",      3,    0,    50,   false, false,  8f),
    };

    // 에디터가 열려 있는 동안에는 배치 모드로 프로젝트를 열 수 없어서,
    // 프로젝트 루트에 이 이름의 표식 파일이 있으면 컴파일 직후 한 번만 자동 실행한다.
    // (Unity 창을 클릭 → 자동 컴파일 → 도메인 리로드 시점) 실행하면 표식을 지운다.
    const string AutoRunMarker = "AI_Source/.run_batch_import";

    [InitializeOnLoadMethod]
    static void AutoRunOnce()
    {
        string marker = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                                     AutoRunMarker.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(marker)) return;
        File.Delete(marker);
        EditorApplication.delayCall += () =>
        {
            Debug.Log("AI 원본 일괄 변환: 표식 파일을 보고 자동 실행합니다.");
            Run();
        };
    }

    [MenuItem("Greed Bound/AI 원본 일괄 변환")]
    public static void Run()
    {
        string root = Directory.GetParent(Application.dataPath).FullName;
        string srcDir = Path.Combine(root, SourceFolder.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(srcDir))
        {
            Debug.LogError($"AI 원본 일괄 변환: 원본 폴더가 없습니다 - {srcDir}");
            return;
        }

        Color32[] palette = PixelConverter.LoadPalette();
        Directory.CreateDirectory(SpriteFolder);
        Directory.CreateDirectory(ClipFolder);

        List<string> done = new List<string>();
        foreach (Job job in Jobs)
        {
            string src = Path.Combine(srcDir, job.name + "_src.png");
            if (!File.Exists(src)) { Debug.LogWarning($"건너뜀 (원본 없음): {src}"); continue; }

            Texture2D source = PixelConverter.LoadImage(src);
            if (source == null) { Debug.LogError($"읽기 실패: {src}"); continue; }

            PixelConverter.Settings settings = new PixelConverter.Settings
            {
                columns = job.frames,
                rows = 1,
                frameCount = job.frames,
                frameList = "",
                referenceFrame = job.referenceFrame,
                lockFeet = job.lockFeet,
                bodyHeightPx = job.bodyHeight,
                canvasWidth = 96,
                canvasHeight = 76,
                flipX = false,
                usePalette = true,
                outline = PixelConverter.Outline.None,
            };

            PixelConverter.Result result = PixelConverter.Convert(source, settings, palette);
            Object.DestroyImmediate(source);
            if (result.error != null) { Debug.LogError($"{job.name} 변환 실패: {result.error}"); continue; }

            string spritePath = $"{SpriteFolder}/{job.name}.png";
            File.WriteAllBytes(spritePath, result.sheet.EncodeToPNG());
            AssetDatabase.ImportAsset(spritePath, ImportAssetOptions.ForceUpdate);
            SliceSheet(spritePath, result.frameCount, settings.canvasWidth, settings.canvasHeight);
            CreateClip(spritePath, $"{ClipFolder}/{job.name}.anim", result.frameCount, job.fps, job.loop);

            done.Add($"{job.name} ({result.frameCount}프레임, 배율 {result.scale:F2}, 잘림 {result.clippedPixels}px)");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 클립이 생겼으니 애니메이터 상태·프리팹 연결을 다시 만든다
        MethodInfo build = typeof(PlayerAnimatorBuilder).GetMethod("Build", BindingFlags.NonPublic | BindingFlags.Static);
        if (build != null) build.Invoke(null, null);
        else Debug.LogWarning("PlayerAnimatorBuilder.Build를 찾지 못했습니다. 메뉴에서 직접 실행하세요.");

        Debug.Log("AI 원본 일괄 변환 완료\n" + string.Join("\n", done));
    }

    // PixelConverterWindow.SliceSheet와 같은 처리 (기존 스프라이트 ID 유지 = 애니메이션 참조 유지)
    internal static void SliceSheet(string path, int frames, int cw, int ch)
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();

        SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        Dictionary<string, SpriteRect> existing = provider.GetSpriteRects().ToDictionary(r => r.name, r => r);
        string baseName = Path.GetFileNameWithoutExtension(path);
        SpriteRect[] rects = new SpriteRect[frames];
        for (int f = 0; f < frames; f++)
        {
            string name = $"{baseName}_{f}";
            rects[f] = new SpriteRect
            {
                name = name,
                rect = new Rect(f * cw, 0, cw, ch),
                alignment = SpriteAlignment.BottomCenter,
                pivot = new Vector2(0.5f, 0f),
                border = Vector4.zero,
                spriteID = existing.TryGetValue(name, out SpriteRect old) ? old.spriteID : GUID.Generate(),
            };
        }
        provider.SetSpriteRects(rects);

        ISpriteNameFileIdDataProvider names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        names?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());

        provider.Apply();
        importer.SaveAndReimport();
    }

    // PixelConverterWindow.CreateClip과 같은 처리
    internal static void CreateClip(string spritePath, string clipPath, int frames, float fps, bool loop)
    {
        string baseName = Path.GetFileNameWithoutExtension(spritePath);
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(spritePath).OfType<Sprite>()
            .Where(s => s.name.StartsWith(baseName + "_"))
            .OrderBy(s => int.TryParse(s.name.Substring(baseName.Length + 1), out int n) ? n : int.MaxValue)
            .Take(frames)
            .ToArray();
        if (sprites.Length == 0) { Debug.LogError($"{spritePath}: 슬라이스된 스프라이트가 없습니다."); return; }

        float frameTime = 1f / fps;
        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i * frameTime, value = sprites[i] };
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length * frameTime, value = sprites[sprites.Length - 1] };

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        bool isNew = clip == null;
        if (isNew)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(clipPath));
            clip = new AnimationClip();
        }
        clip.frameRate = fps;

        EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
        clipSettings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, clipSettings);

        if (isNew) AssetDatabase.CreateAsset(clip, clipPath);
        else EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();
    }
}
