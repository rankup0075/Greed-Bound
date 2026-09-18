using System.Collections.Generic;
using System.IO;
using UnityEngine;

// AI 이미지 → 게임용 도트 스프라이트 시트 변환 (md/GreedBound_Art_Guide.md 4장, Spec 8장).
// 배경 제거 → 기준 프레임 키를 몸 높이에 맞춰 축소(블록 다수결) → 팔레트 맞춤 → 발 기준 캔버스 배치 → 가로 한 줄 시트.
// 모든 프레임에 같은 배율·같은 원점을 써서 프레임 사이 움직임(점프·앞으로 내딛기)은 그대로 남음.
public static class PixelConverter
{
    public const string PalettePath = "Assets/Art/Palette.hex";

    public enum Outline { None, Inner, Outer }

    public class Settings
    {
        public int columns = 1, rows = 1, frameCount = 1;
        public string frameList = "";          // 쓸 프레임·순서 (예: "0-3,5,7"). 비우면 0 ~ frameCount-1. 이상한 프레임 빼기용
        public int referenceFrame;             // 원본 시트의 칸 번호
        public bool lockFeet;                  // 프레임마다 발 위치 고정 (AI 애니메이션의 위아래·좌우 흔들림 제거)
        public int bodyHeightPx = 36;          // 기준 프레임의 불투명 영역 키가 이 px가 되게 축소
        public int canvasWidth = 72, canvasHeight = 56;
        public bool flipX;                     // 원본이 왼쪽을 보고 있을 때
        public bool usePalette = true;
        public float backgroundTolerance = 40f; // 0~255 RGB 거리
        public float coverage = 0.4f;           // 블록 안 불투명 비율이 이 이상이어야 픽셀로 남김
        public Outline outline = Outline.None;
    }

    public class Result
    {
        public Texture2D sheet;                // 가로 한 줄, 프레임당 canvasWidth × canvasHeight
        public int frameCount;
        public float scale;                    // 원본 px / 출력 px
        public int clippedPixels;              // 캔버스 밖으로 잘린 출력 픽셀 수
        public string error;
    }

    public static Texture2D LoadImage(string path)
    {
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(File.ReadAllBytes(path)))
        {
            Object.DestroyImmediate(texture);
            return null;
        }
        return texture;
    }

    public static Color32[] LoadPalette()
    {
        List<Color32> colors = new List<Color32>();
        if (File.Exists(PalettePath))
        {
            foreach (string raw in File.ReadAllLines(PalettePath))
            {
                string line = raw.Trim().TrimStart('#');
                if (line.Length == 6 && ColorUtility.TryParseHtmlString("#" + line, out Color c)) colors.Add(c);
            }
        }
        return colors.ToArray();
    }

    public static Result Convert(Texture2D source, Settings s, Color32[] palette)
    {
        Result result = new Result();
        int w = source.width, h = source.height;
        int cols = Mathf.Max(1, s.columns), rows = Mathf.Max(1, s.rows);
        int cellW = w / cols, cellH = h / rows;
        if (cellW < 1 || cellH < 1) return Fail(result, "시트 칸 수가 이미지보다 큽니다.");
        List<int> order = ParseFrameList(s.frameList, Mathf.Clamp(s.frameCount, 1, cols * rows), cols * rows, out string listError);
        if (listError != null) return Fail(result, listError);
        int frames = order.Count;

        Color32[] pixels = source.GetPixels32();
        bool[] opaque = OpaqueMask(pixels, w, h, s.backgroundTolerance);

        // 기준 프레임의 불투명 영역 → 배율·원점
        int refFrame = Mathf.Clamp(s.referenceFrame, 0, cols * rows - 1);
        if (!Bounds(opaque, w, CellRect(refFrame, cols, cellW, cellH, h), out RectInt refBounds))
            return Fail(result, $"기준 프레임 {refFrame}에 캐릭터가 없습니다. (배경 허용 오차 확인)");

        float scale = refBounds.height / (float)Mathf.Max(1, s.bodyHeightPx);
        if (scale >= 1f && Mathf.Abs(scale - Mathf.Round(scale)) < 0.08f) scale = Mathf.Round(scale);  // 이미 도트인 원본은 정수 배율로
        scale = Mathf.Max(scale, 0.01f);
        result.scale = scale;

        RectInt refCell = CellRect(refFrame, cols, cellW, cellH, h);
        Vector2 refOrigin = s.lockFeet ? FeetOrigin(opaque, w, refCell, refBounds)
                                       : new Vector2(refBounds.center.x - refCell.x, refBounds.yMin - refCell.y);  // 셀 기준 몸 가운데·발 (텍스처 y는 아래가 0)

        int cw = s.canvasWidth, ch = s.canvasHeight;
        Color32[] sheet = new Color32[cw * frames * ch];
        Dictionary<int, Color32> nearest = new Dictionary<int, Color32>();
        bool paletteOn = s.usePalette && palette != null && palette.Length > 0;

        for (int f = 0; f < frames; f++)
        {
            RectInt cell = CellRect(order[f], cols, cellW, cellH, h);
            Color32[] frame = new Color32[cw * ch];

            // 발 고정: 프레임마다 자기 발 위치를 원점으로 (비어 있는 프레임은 기준 원점)
            Vector2 origin = s.lockFeet && Bounds(opaque, w, cell, out RectInt fb) ? FeetOrigin(opaque, w, cell, fb) : refOrigin;
            float originX = origin.x, originY = origin.y;

            for (int oy = 0; oy < ch; oy++)
            for (int ox = 0; ox < cw; ox++)
            {
                // 출력 픽셀 하나가 덮는 원본 블록. 좌우 반전은 캔버스 가운데 기준으로 거울 위치에 씀
                float lx0 = originX + (ox - cw * 0.5f) * scale;
                float ly0 = originY + oy * scale;
                if (SampleBlock(pixels, opaque, w, cell, lx0, ly0, scale, s.coverage, paletteOn, palette, nearest, out Color32 color))
                    frame[oy * cw + (s.flipX ? cw - 1 - ox : ox)] = color;
            }

            result.clippedPixels += CountClipped(opaque, w, cell, originX, originY, scale, cw, ch);
            ApplyOutline(frame, cw, ch, s.outline, paletteOn ? Darkest(palette) : new Color32(24, 20, 37, 255));

            for (int y = 0; y < ch; y++)
                System.Array.Copy(frame, y * cw, sheet, y * cw * frames + f * cw, cw);
        }

        Texture2D output = new Texture2D(cw * frames, ch, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        output.SetPixels32(sheet);
        output.Apply();
        result.sheet = output;
        result.frameCount = frames;
        return result;
    }

    static Result Fail(Result result, string message)
    {
        result.error = message;
        return result;
    }

    // "0-3,5,7" → [0,1,2,3,5,7]. 비어 있으면 0 ~ defaultCount-1. 거꾸로 범위("5-2")·반복도 허용
    static List<int> ParseFrameList(string text, int defaultCount, int cellCount, out string error)
    {
        error = null;
        List<int> order = new List<int>();
        if (string.IsNullOrWhiteSpace(text))
        {
            for (int i = 0; i < defaultCount; i++) order.Add(i);
            return order;
        }
        foreach (string raw in text.Split(','))
        {
            string token = raw.Trim();
            if (token.Length == 0) continue;
            string[] ends = token.Split('-');
            if (ends.Length > 2 || !int.TryParse(ends[0], out int a) || !int.TryParse(ends[ends.Length - 1], out int b))
            {
                error = $"프레임 목록을 읽지 못했습니다: \"{token}\" (예: 0-3,5,7)";
                return order;
            }
            int step = b >= a ? 1 : -1;
            for (int i = a; ; i += step)
            {
                if (i < 0 || i >= cellCount)
                {
                    error = $"프레임 {i}은 시트 칸 범위(0~{cellCount - 1}) 밖입니다.";
                    return order;
                }
                order.Add(i);
                if (i == b) break;
            }
        }
        if (order.Count == 0) error = "프레임 목록이 비어 있습니다.";
        return order;
    }

    // 발 원점: 불투명 영역 맨 아래를 세로 기준, 아래쪽 25% 줄(발 부근)의 가로 가운데를 가로 기준.
    // 칼·팔이 앞으로 뻗어 전체 폭이 바뀌어도 발은 제자리에 있게
    static Vector2 FeetOrigin(bool[] opaque, int w, RectInt cell, RectInt bounds)
    {
        int bandTop = bounds.yMin + Mathf.Max(1, Mathf.RoundToInt(bounds.height * 0.25f));
        RectInt band = new RectInt(bounds.xMin, bounds.yMin, bounds.width, bandTop - bounds.yMin);
        float centerX = Bounds(opaque, w, band, out RectInt feet) ? feet.center.x : bounds.center.x;
        return new Vector2(centerX - cell.x, bounds.yMin - cell.y);
    }

    // 프레임 f의 원본 영역. 시트는 왼쪽 위 칸부터 가로로 읽음 (텍스처 y는 아래가 0이라 뒤집어 계산)
    static RectInt CellRect(int f, int cols, int cellW, int cellH, int texHeight)
    {
        int c = f % cols, r = f / cols;
        return new RectInt(c * cellW, texHeight - (r + 1) * cellH, cellW, cellH);
    }

    // 원본에 투명도가 있으면 그대로 쓰고, 없으면 테두리에서 이어진 배경색 영역을 지움 (안쪽의 같은 색은 남김)
    static bool[] OpaqueMask(Color32[] pixels, int w, int h, float tolerance)
    {
        bool[] opaque = new bool[pixels.Length];
        bool hasAlpha = false;
        for (int i = 0; i < pixels.Length; i++)
        {
            opaque[i] = pixels[i].a >= 128;
            if (pixels[i].a < 250) hasAlpha = true;
        }
        if (hasAlpha) return opaque;

        Color32 key = pixels[(h - 1) * w];  // 왼쪽 위 모서리
        float tol2 = tolerance * tolerance;
        Queue<int> queue = new Queue<int>();
        void Seed(int i)
        {
            if (opaque[i] && Distance2(pixels[i], key) <= tol2)
            {
                opaque[i] = false;
                queue.Enqueue(i);
            }
        }
        for (int x = 0; x < w; x++) { Seed(x); Seed((h - 1) * w + x); }
        for (int y = 0; y < h; y++) { Seed(y * w); Seed(y * w + w - 1); }

        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            int x = i % w, y = i / w;
            if (x > 0) Seed(i - 1);
            if (x < w - 1) Seed(i + 1);
            if (y > 0) Seed(i - w);
            if (y < h - 1) Seed(i + w);
        }
        return opaque;
    }

    static bool Bounds(bool[] opaque, int w, RectInt area, out RectInt bounds)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (int y = area.yMin; y < area.yMax; y++)
        for (int x = area.xMin; x < area.xMax; x++)
        {
            if (!opaque[y * w + x]) continue;
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }
        bounds = maxX < 0 ? default : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        return maxX >= 0;
    }

    // 블록 다수결: 뭉개진 평균색 대신 가장 많이 나온 색을 골라 도트 경계를 또렷하게
    static bool SampleBlock(Color32[] pixels, bool[] opaque, int w, RectInt cell, float lx0, float ly0, float size, float coverage,
                            bool paletteOn, Color32[] palette, Dictionary<int, Color32> nearest, out Color32 color)
    {
        color = default;
        int x0 = Mathf.FloorToInt(lx0), x1 = Mathf.CeilToInt(lx0 + size);
        int y0 = Mathf.FloorToInt(ly0), y1 = Mathf.CeilToInt(ly0 + size);
        if (x1 <= x0) x1 = x0 + 1;
        if (y1 <= y0) y1 = y0 + 1;

        int total = 0, filled = 0;
        Dictionary<int, int> votes = new Dictionary<int, int>();
        Dictionary<int, Vector3> sums = new Dictionary<int, Vector3>();
        for (int ly = y0; ly < y1; ly++)
        for (int lx = x0; lx < x1; lx++)
        {
            total++;
            if (lx < 0 || ly < 0 || lx >= cell.width || ly >= cell.height) continue;
            int i = (cell.y + ly) * w + cell.x + lx;
            if (!opaque[i]) continue;
            filled++;

            Color32 p = pixels[i];
            int bucket;
            if (paletteOn)
            {
                Color32 q = Nearest(p, palette, nearest);
                bucket = (q.r << 16) | (q.g << 8) | q.b;
            }
            else bucket = (p.r >> 3 << 10) | (p.g >> 3 << 5) | (p.b >> 3);

            votes.TryGetValue(bucket, out int count);
            votes[bucket] = count + 1;
            sums.TryGetValue(bucket, out Vector3 sum);
            sums[bucket] = sum + new Vector3(p.r, p.g, p.b);
        }
        if (total == 0 || filled < total * coverage) return false;

        int best = 0, bestCount = -1;
        foreach (KeyValuePair<int, int> v in votes)
            if (v.Value > bestCount) { best = v.Key; bestCount = v.Value; }

        if (paletteOn) color = new Color32((byte)(best >> 16), (byte)(best >> 8), (byte)best, 255);
        else
        {
            Vector3 avg = sums[best] / bestCount;
            color = new Color32((byte)avg.x, (byte)avg.y, (byte)avg.z, 255);
        }
        return true;
    }

    static Color32 Nearest(Color32 c, Color32[] palette, Dictionary<int, Color32> cache)
    {
        int key = (c.r << 16) | (c.g << 8) | c.b;
        if (cache.TryGetValue(key, out Color32 hit)) return hit;

        Color32 best = palette[0];
        float bestD = float.MaxValue;
        foreach (Color32 p in palette)
        {
            // redmean: 사람 눈 기준 색 거리 근사
            float rm = (c.r + p.r) * 0.5f;
            float dr = c.r - p.r, dg = c.g - p.g, db = c.b - p.b;
            float d = (2f + rm / 256f) * dr * dr + 4f * dg * dg + (2f + (255f - rm) / 256f) * db * db;
            if (d < bestD) { bestD = d; best = p; }
        }
        cache[key] = best;
        return best;
    }

    static float Distance2(Color32 a, Color32 b)
    {
        float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
        return dr * dr + dg * dg + db * db;
    }

    static Color32 Darkest(Color32[] palette)
    {
        Color32 best = palette[0];
        foreach (Color32 p in palette)
            if (p.r + p.g + p.b < best.r + best.g + best.b) best = p;
        return best;
    }

    // 캐릭터 불투명 영역 중 캔버스 밖으로 나가는 부분 (출력 px 단위 근사, 좌우 반전과 무관)
    static int CountClipped(bool[] opaque, int w, RectInt cell, float originX, float originY, float scale, int cw, int ch)
    {
        if (!Bounds(opaque, w, cell, out RectInt b)) return 0;
        float left = (b.xMin - cell.x - originX) / scale + cw * 0.5f;
        float right = (b.xMax - cell.x - originX) / scale + cw * 0.5f;
        float bottom = (b.yMin - cell.y - originY) / scale;
        float top = (b.yMax - cell.y - originY) / scale;
        int clipped = 0;
        if (left < 0f) clipped += Mathf.CeilToInt(-left);
        if (right > cw) clipped += Mathf.CeilToInt(right - cw);
        if (bottom < 0f) clipped += Mathf.CeilToInt(-bottom);
        if (top > ch) clipped += Mathf.CeilToInt(top - ch);
        return clipped;
    }

    // Inner: 가장자리 픽셀을 외곽선 색으로 (크기 유지). Outer: 바깥 1px 추가 (몸이 1px씩 커짐)
    static void ApplyOutline(Color32[] frame, int cw, int ch, Outline mode, Color32 lineColor)
    {
        if (mode == Outline.None) return;
        bool[] solid = new bool[frame.Length];
        for (int i = 0; i < frame.Length; i++) solid[i] = frame[i].a > 0;

        for (int y = 0; y < ch; y++)
        for (int x = 0; x < cw; x++)
        {
            int i = y * cw + x;
            bool edge = false, near = false;
            for (int k = 0; k < 4; k++)
            {
                int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                bool inside = nx >= 0 && ny >= 0 && nx < cw && ny < ch;
                bool neighborSolid = inside && solid[ny * cw + nx];
                if (solid[i] && !neighborSolid && (inside || ny >= 0)) edge = true;  // 발 아래(캔버스 밖 바닥)는 외곽선 없음
                if (!solid[i] && neighborSolid) near = true;
            }
            if (mode == Outline.Inner && solid[i] && edge) frame[i] = lineColor;
            if (mode == Outline.Outer && !solid[i] && near) frame[i] = lineColor;
        }
    }
}
