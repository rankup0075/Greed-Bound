using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// 에디터 메뉴 "Greed Bound > 도트 변환기" (md/GreedBound_Art_Guide.md 4장).
// AI가 뽑은 이미지를 골라 캐릭터 프리셋에 맞춰 변환 → 미리보기 → Assets/Art/Sprites에 시트 저장(프레임별 슬라이스, 발 기준점)
// → 선택하면 Assets/Art/Animations에 애니메이션 클립까지 생성.
public class PixelConverterWindow : EditorWindow
{
    const int PixelsPerUnit = 36;

    // Spec 8장 "캐릭터 몸 크기" 표
    struct Preset
    {
        public string label, prefix, folder;
        public int bodyW, bodyH, canvasW, canvasH;
        public Preset(string label, string prefix, string folder, int bodyW, int bodyH, int canvasW, int canvasH)
        {
            this.label = label; this.prefix = prefix; this.folder = folder;
            this.bodyW = bodyW; this.bodyH = bodyH; this.canvasW = canvasW; this.canvasH = canvasH;
        }
    }

    static readonly Preset[] Presets =
    {
        new Preset("플레이어", "player", "Player", 50, 50, 96, 76),
        new Preset("일반 적", "enemy_normal", "Enemies", 36, 50, 76, 64),
        new Preset("소형 적", "enemy_small", "Enemies", 27, 35, 54, 48),
        new Preset("정예", "enemy_elite", "Enemies", 49, 68, 96, 88),
        new Preset("원거리", "enemy_ranged", "Enemies", 34, 53, 64, 64),
        new Preset("방패병", "enemy_shield", "Enemies", 41, 56, 76, 72),
        new Preset("돌진병", "enemy_charger", "Enemies", 39, 46, 76, 60),
        new Preset("보스", "boss_knight", "Bosses", 57, 79, 128, 104),
        new Preset("직접 입력 (이펙트·UI·환경)", "fx", "Effects", 32, 32, 32, 32),
    };

    static readonly string[] Folders = { "Player", "Enemies", "Bosses", "Effects", "Environment", "UI" };

    string sourcePath = "";
    Texture2D source;
    int presetIndex;
    string folder = "Player";
    string assetName = "player_idle";
    int bodyW = 36;
    readonly PixelConverter.Settings settings = new PixelConverter.Settings();

    bool makeClip = true;
    bool loop = true;
    float fps = 10f;
    float totalDuration;                      // 0보다 크면 fps 대신 이 길이에 맞춤 (예고 동작)

    PixelConverter.Result preview;
    bool playPreview = true;
    Vector2 scroll;
    int zoom = 4;

    [MenuItem("Greed Bound/도트 변환기")]
    static void Open() => GetWindow<PixelConverterWindow>("도트 변환기");

    void OnEnable() => ApplyPreset(presetIndex, keepName: true);

    void OnDisable()
    {
        ClearPreview();
        if (source != null) DestroyImmediate(source);
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("1. 원본 이미지", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(sourcePath) ? "(선택 안 함)" : sourcePath, GUILayout.Height(18));
            if (GUILayout.Button("파일 선택", GUILayout.Width(80))) PickSource();
        }
        if (source != null) EditorGUILayout.LabelField($"크기 {source.width} × {source.height}");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2. 캐릭터 프리셋", EditorStyles.boldLabel);
        int newPreset = EditorGUILayout.Popup("프리셋", presetIndex, Presets.Select(p => p.label).ToArray());
        if (newPreset != presetIndex) ApplyPreset(newPreset, keepName: false);
        using (new EditorGUILayout.HorizontalScope())
        {
            bodyW = EditorGUILayout.IntField("몸 폭 px (표시용)", bodyW);
            settings.bodyHeightPx = Mathf.Max(1, EditorGUILayout.IntField("몸 높이 px", settings.bodyHeightPx));
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            settings.canvasWidth = Mathf.Max(1, EditorGUILayout.IntField("캔버스 폭", settings.canvasWidth));
            settings.canvasHeight = Mathf.Max(1, EditorGUILayout.IntField("캔버스 높이", settings.canvasHeight));
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("3. 시트 칸", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            settings.columns = Mathf.Max(1, EditorGUILayout.IntField("가로 칸", settings.columns));
            settings.rows = Mathf.Max(1, EditorGUILayout.IntField("세로 칸", settings.rows));
        }
        settings.frameCount = Mathf.Clamp(EditorGUILayout.IntField("프레임 수", settings.frameCount), 1, settings.columns * settings.rows);
        settings.frameList = EditorGUILayout.TextField(new GUIContent("쓸 프레임 (선택)", "비우면 전부. 예: 0-3,5,7 — 이상한 프레임을 빼거나 순서를 바꿈. 번호는 시트 칸 번호(왼쪽 위 0부터)"), settings.frameList);
        settings.referenceFrame = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("기준 프레임", "무기를 들어 올리지 않은 프레임(시트 칸 번호). 이 프레임 키 = 몸 높이"), settings.referenceFrame), 0, settings.columns * settings.rows - 1);
        settings.lockFeet = EditorGUILayout.Toggle(new GUIContent("프레임마다 발 고정", "AI 애니메이션의 흔들림 제거. 달리기·걷기·공격·대기는 켬 / 점프·대쉬처럼 몸이 실제로 이동하는 동작은 끔"), settings.lockFeet);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("4. 변환 옵션", EditorStyles.boldLabel);
        settings.flipX = EditorGUILayout.Toggle(new GUIContent("좌우 뒤집기", "원본이 왼쪽을 보고 있으면 켬 — 게임 기준은 오른쪽"), settings.flipX);
        settings.usePalette = EditorGUILayout.Toggle(new GUIContent("팔레트 맞춤", PixelConverter.PalettePath), settings.usePalette);
        settings.backgroundTolerance = EditorGUILayout.Slider(new GUIContent("배경 허용 오차", "투명도 없는 이미지: 테두리와 이어진 비슷한 색을 배경으로 지움"), settings.backgroundTolerance, 0f, 160f);
        settings.coverage = EditorGUILayout.Slider(new GUIContent("픽셀 채움 기준", "낮추면 얇은 부분(칼끝·머리카락)이 더 남음"), settings.coverage, 0.1f, 0.9f);
        settings.outline = (PixelConverter.Outline)EditorGUILayout.Popup("외곽선", (int)settings.outline, new[] { "없음", "안쪽 1px (크기 유지)", "바깥 1px" });

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("5. 저장", EditorStyles.boldLabel);
        int folderIndex = Mathf.Max(0, System.Array.IndexOf(Folders, folder));
        folder = Folders[EditorGUILayout.Popup("폴더", folderIndex, Folders)];
        assetName = EditorGUILayout.TextField(new GUIContent("이름", "캐릭터_동작 (예: player_run)"), assetName);
        makeClip = EditorGUILayout.Toggle("애니메이션 클립 만들기", makeClip);
        if (makeClip)
        {
            EditorGUI.indentLevel++;
            loop = EditorGUILayout.Toggle("반복", loop);
            fps = Mathf.Max(1f, EditorGUILayout.FloatField("초당 프레임", fps));
            totalDuration = Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent("총 길이(초)", "0보다 크면 초당 프레임 대신 사용 — 예고 동작은 예고 시간(0.4·0.6 등)"), totalDuration));
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.LabelField("→ " + SpritePath + (makeClip ? "   /   " + ClipPath : ""), EditorStyles.miniLabel);

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(source == null))
            {
                if (GUILayout.Button("미리보기", GUILayout.Height(28))) RunPreview();
                if (GUILayout.Button("저장", GUILayout.Height(28))) Save();
            }
        }

        DrawPreview();
        EditorGUILayout.EndScrollView();
    }

    string SpritePath => $"Assets/Art/Sprites/{folder}/{assetName}.png";
    string ClipPath => $"Assets/Art/Animations/{folder}/{assetName}.anim";

    void ApplyPreset(int index, bool keepName)
    {
        presetIndex = Mathf.Clamp(index, 0, Presets.Length - 1);
        Preset p = Presets[presetIndex];
        bodyW = p.bodyW;
        settings.bodyHeightPx = p.bodyH;
        settings.canvasWidth = p.canvasW;
        settings.canvasHeight = p.canvasH;
        folder = p.folder;
        if (!keepName) assetName = p.prefix + "_idle";
        ClearPreview();
    }

    void PickSource()
    {
        string start = string.IsNullOrEmpty(sourcePath) ? System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile) : Path.GetDirectoryName(sourcePath);
        string path = EditorUtility.OpenFilePanelWithFilters("원본 이미지", start, new[] { "이미지", "png,jpg,jpeg,gif" });
        if (string.IsNullOrEmpty(path)) return;

        Texture2D loaded;
        if (Path.GetExtension(path).ToLowerInvariant() == ".gif")
        {
            // GIF: 프레임을 가로 한 줄 시트로 펼치고 칸 수·재생 속도를 GIF 값으로 채움
            GifDecoder.Result gif = GifDecoder.Decode(path);
            loaded = gif != null ? GifDecoder.ToSheet(gif) : null;
            if (gif != null)
            {
                settings.columns = gif.frames.Count;
                settings.rows = 1;
                settings.frameCount = gif.frames.Count;
                settings.referenceFrame = 0;
                float averageDelay = 0f;
                foreach (float d in gif.delays) averageDelay += d;
                averageDelay /= gif.delays.Count;
                if (averageDelay > 0.001f) fps = Mathf.Round(10f / averageDelay) / 10f;
            }
        }
        else loaded = PixelConverter.LoadImage(path);

        if (loaded == null)
        {
            EditorUtility.DisplayDialog("도트 변환기", "이미지를 읽지 못했습니다. PNG·JPG·GIF만 지원합니다.\n(WEBP는 그림판 등에서 PNG로 저장 후 사용)", "확인");
            return;
        }
        if (source != null) DestroyImmediate(source);
        source = loaded;
        sourcePath = path;
        ClearPreview();
    }

    void RunPreview()
    {
        ClearPreview();
        preview = PixelConverter.Convert(source, settings, PixelConverter.LoadPalette());
        if (preview.error != null) EditorUtility.DisplayDialog("도트 변환기", preview.error, "확인");
    }

    void ClearPreview()
    {
        if (preview?.sheet != null) DestroyImmediate(preview.sheet);
        preview = null;
    }

    void DrawPreview()
    {
        if (preview == null || preview.sheet == null) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("미리보기", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"배율: 원본 {preview.scale:F2}px → 1px   ·   프레임 {preview.frameCount}   ·   초록 = 판정(몸) {bodyW}×{settings.bodyHeightPx}px, 파랑 = 캔버스");
        if (preview.clippedPixels > 0)
            EditorGUILayout.HelpBox($"캐릭터 일부가 캔버스 밖으로 잘렸습니다 (약 {preview.clippedPixels}px). 캔버스를 키우거나 몸 높이를 확인하세요.", MessageType.Warning);
        zoom = EditorGUILayout.IntSlider("확대", zoom, 1, 12);

        int cw = settings.canvasWidth, ch = settings.canvasHeight;
        Rect area = GUILayoutUtility.GetRect(preview.sheet.width * zoom, ch * zoom, GUILayout.ExpandWidth(false));
        DrawChecker(area);
        GUI.DrawTexture(area, preview.sheet, ScaleMode.StretchToFill, true);

        Color canvasLine = new Color(0.3f, 0.6f, 1f, 0.8f);
        Color bodyLine = new Color(0.3f, 1f, 0.4f, 0.9f);
        for (int f = 0; f < preview.frameCount; f++)
        {
            Rect frame = new Rect(area.x + f * cw * zoom, area.y, cw * zoom, ch * zoom);
            DrawBox(frame, canvasLine);
            float bodyX = frame.x + (cw - bodyW) * 0.5f * zoom;
            DrawBox(new Rect(bodyX, frame.yMax - settings.bodyHeightPx * zoom, bodyW * zoom, settings.bodyHeightPx * zoom), bodyLine);
        }

        // 재생 미리보기: 저장할 클립과 같은 속도로 반복 — 루프 끝↔처음 이음새 확인용
        EditorGUILayout.Space();
        playPreview = EditorGUILayout.ToggleLeft($"재생 미리보기 (초당 {PlaybackFps:F1}프레임 — 5. 저장의 초당 프레임·총 길이 사용)", playPreview);
        if (!playPreview) return;

        int current = Mathf.FloorToInt((float)(EditorApplication.timeSinceStartup * PlaybackFps)) % preview.frameCount;
        EditorGUILayout.LabelField($"프레임 {current + 1} / {preview.frameCount}");
        int playZoom = Mathf.Max(zoom, 4);
        Rect playArea = GUILayoutUtility.GetRect(cw * playZoom, ch * playZoom, GUILayout.ExpandWidth(false));
        DrawChecker(playArea);
        Rect uv = new Rect((float)current / preview.frameCount, 0f, 1f / preview.frameCount, 1f);
        GUI.DrawTextureWithTexCoords(playArea, preview.sheet, uv, true);
        DrawBox(new Rect(playArea.x + (cw - bodyW) * 0.5f * playZoom, playArea.yMax - settings.bodyHeightPx * playZoom, bodyW * playZoom, settings.bodyHeightPx * playZoom), bodyLine);
    }

    float PlaybackFps => makeClip && totalDuration > 0f && preview != null ? preview.frameCount / totalDuration : fps;

    void Update()
    {
        if (playPreview && preview?.sheet != null) Repaint();
    }

    static void DrawChecker(Rect area)
    {
        EditorGUI.DrawRect(area, new Color(0.22f, 0.22f, 0.24f));
        const float cell = 8f;
        for (float y = area.y; y < area.yMax; y += cell)
        for (float x = area.x + ((int)((y - area.y) / cell) % 2) * cell; x < area.xMax; x += cell * 2f)
            EditorGUI.DrawRect(new Rect(x, y, Mathf.Min(cell, area.xMax - x), Mathf.Min(cell, area.yMax - y)), new Color(0.28f, 0.28f, 0.3f));
    }

    static void DrawBox(Rect r, Color color)
    {
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1f), color);
        EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), color);
        EditorGUI.DrawRect(new Rect(r.x, r.y, 1f, r.height), color);
        EditorGUI.DrawRect(new Rect(r.xMax - 1f, r.y, 1f, r.height), color);
    }

    void Save()
    {
        if (string.IsNullOrWhiteSpace(assetName) || assetName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            EditorUtility.DisplayDialog("도트 변환기", "이름에 쓸 수 없는 문자가 있습니다.", "확인");
            return;
        }
        RunPreview();
        if (preview == null || preview.error != null) return;
        if (File.Exists(SpritePath) && !EditorUtility.DisplayDialog("도트 변환기", $"{SpritePath}\n이미 있습니다. 덮어쓸까요? (연결된 애니메이션 참조는 유지)", "덮어쓰기", "취소")) return;

        Directory.CreateDirectory(Path.GetDirectoryName(SpritePath));
        File.WriteAllBytes(SpritePath, preview.sheet.EncodeToPNG());
        AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceUpdate);
        SliceSheet(SpritePath, preview.frameCount, settings.canvasWidth, settings.canvasHeight);

        string clipNote = "";
        if (makeClip)
        {
            CreateClip(SpritePath, ClipPath, preview.frameCount);
            clipNote = "\n" + ClipPath;
        }

        Object saved = AssetDatabase.LoadAssetAtPath<Texture2D>(SpritePath);
        EditorGUIUtility.PingObject(saved);
        Debug.Log($"도트 변환 저장: {SpritePath} ({preview.frameCount}프레임, 배율 {preview.scale:F2}){clipNote}");
    }

    // 프레임별 슬라이스 + 발 가운데 기준점. 같은 이름의 기존 스프라이트 ID는 유지 (애니메이션 참조가 끊기지 않게)
    static void SliceSheet(string path, int frames, int cw, int ch)
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

    void CreateClip(string spritePath, string clipPath, int frames)
    {
        string baseName = Path.GetFileNameWithoutExtension(spritePath);
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(spritePath).OfType<Sprite>()
            .Where(s => s.name.StartsWith(baseName + "_"))
            .OrderBy(s => int.TryParse(s.name.Substring(baseName.Length + 1), out int n) ? n : int.MaxValue)
            .Take(frames)
            .ToArray();
        if (sprites.Length == 0) return;

        float frameTime = totalDuration > 0f ? totalDuration / sprites.Length : 1f / fps;
        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i * frameTime, value = sprites[i] };
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length * frameTime, value = sprites[sprites.Length - 1] };  // 마지막 프레임도 한 칸 유지

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        bool isNew = clip == null;
        if (isNew)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(clipPath));
            clip = new AnimationClip();
        }
        clip.frameRate = totalDuration > 0f ? Mathf.Max(1f, sprites.Length / totalDuration) : fps;

        // SpriteRenderer와 Animator는 같은 Visual 오브젝트에 있으므로 경로 ""
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
