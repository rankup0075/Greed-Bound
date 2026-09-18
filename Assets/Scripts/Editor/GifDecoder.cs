using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 애니메이션 GIF → 프레임 목록 (도트 변환기용, PixelLab이 GIF로 내보냄).
// Unity는 GIF를 읽지 못해서 직접 해석: 전역·지역 팔레트, 투명색, 프레임 합성(disposal 0~3), 인터레이스.
public static class GifDecoder
{
    public class Result
    {
        public int width, height;
        public readonly List<Color32[]> frames = new List<Color32[]>();  // Unity 텍스처 순서 (아래 줄이 0)
        public readonly List<float> delays = new List<float>();          // 초
    }

    public static Result Decode(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        int pos = 0;
        if (data.Length < 13 || data[0] != 'G' || data[1] != 'I' || data[2] != 'F') return null;
        pos = 6;

        Result result = new Result
        {
            width = data[pos] | (data[pos + 1] << 8),
            height = data[pos + 2] | (data[pos + 3] << 8),
        };
        byte packed = data[pos + 4];
        pos += 7;

        Color32[] globalTable = null;
        if ((packed & 0x80) != 0)
        {
            int size = 2 << (packed & 7);
            globalTable = ReadTable(data, ref pos, size);
        }

        int w = result.width, h = result.height;
        Color32[] canvas = new Color32[w * h];   // GIF 순서 (위 줄이 0)
        int disposal = 0, transparentIndex = -1;
        float delay = 0.1f;

        while (pos < data.Length)
        {
            byte block = data[pos++];
            if (block == 0x3B) break;  // 끝

            if (block == 0x21)  // 확장
            {
                byte label = data[pos++];
                if (label == 0xF9 && data[pos] >= 4)
                {
                    byte flags = data[pos + 1];
                    disposal = (flags >> 2) & 7;
                    int delayCs = data[pos + 2] | (data[pos + 3] << 8);
                    delay = delayCs > 0 ? delayCs / 100f : 0.1f;
                    transparentIndex = (flags & 1) != 0 ? data[pos + 4] : -1;
                }
                SkipSubBlocks(data, ref pos);
                continue;
            }

            if (block != 0x2C) return result.frames.Count > 0 ? result : null;  // 알 수 없는 블록

            int fx = data[pos] | (data[pos + 1] << 8);
            int fy = data[pos + 2] | (data[pos + 3] << 8);
            int fw = data[pos + 4] | (data[pos + 5] << 8);
            int fh = data[pos + 6] | (data[pos + 7] << 8);
            byte framePacked = data[pos + 8];
            pos += 9;

            Color32[] table = globalTable;
            if ((framePacked & 0x80) != 0) table = ReadTable(data, ref pos, 2 << (framePacked & 7));
            bool interlaced = (framePacked & 0x40) != 0;

            int minCodeSize = data[pos++];
            byte[] indices = DecodeLzw(ReadSubBlocks(data, ref pos), minCodeSize, fw * fh);

            Color32[] previous = disposal == 3 ? (Color32[])canvas.Clone() : null;

            // 그리기
            int[] rowOrder = RowOrder(fh, interlaced);
            for (int i = 0; i < fh; i++)
            {
                int row = rowOrder[i];
                int cy = fy + row;
                if (cy < 0 || cy >= h) continue;
                for (int col = 0; col < fw; col++)
                {
                    int cx = fx + col;
                    if (cx < 0 || cx >= w) continue;
                    int index = indices[i * fw + col];
                    if (index == transparentIndex || table == null || index >= table.Length) continue;
                    canvas[cy * w + cx] = table[index];
                }
            }

            // 저장 (Unity는 아래 줄이 0이라 뒤집음)
            Color32[] frame = new Color32[w * h];
            for (int y = 0; y < h; y++) System.Array.Copy(canvas, y * w, frame, (h - 1 - y) * w, w);
            result.frames.Add(frame);
            result.delays.Add(delay);

            // 다음 프레임 전 처리
            if (disposal == 2)
            {
                for (int y = Mathf.Max(0, fy); y < Mathf.Min(h, fy + fh); y++)
                for (int x = Mathf.Max(0, fx); x < Mathf.Min(w, fx + fw); x++)
                    canvas[y * w + x] = default;
            }
            else if (disposal == 3 && previous != null) canvas = previous;

            disposal = 0;
            transparentIndex = -1;
            delay = 0.1f;
        }
        return result.frames.Count > 0 ? result : null;
    }

    // 프레임을 가로 한 줄 시트로
    public static Texture2D ToSheet(Result gif)
    {
        int w = gif.width, h = gif.height, n = gif.frames.Count;
        Color32[] sheet = new Color32[w * n * h];
        for (int f = 0; f < n; f++)
        for (int y = 0; y < h; y++)
            System.Array.Copy(gif.frames[f], y * w, sheet, y * w * n + f * w, w);

        Texture2D texture = new Texture2D(w * n, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        texture.SetPixels32(sheet);
        texture.Apply();
        return texture;
    }

    static Color32[] ReadTable(byte[] data, ref int pos, int size)
    {
        Color32[] table = new Color32[size];
        for (int i = 0; i < size && pos + 2 < data.Length; i++, pos += 3)
            table[i] = new Color32(data[pos], data[pos + 1], data[pos + 2], 255);
        return table;
    }

    static void SkipSubBlocks(byte[] data, ref int pos)
    {
        while (pos < data.Length)
        {
            int size = data[pos++];
            if (size == 0) return;
            pos += size;
        }
    }

    static byte[] ReadSubBlocks(byte[] data, ref int pos)
    {
        using (MemoryStream stream = new MemoryStream())
        {
            while (pos < data.Length)
            {
                int size = data[pos++];
                if (size == 0) break;
                stream.Write(data, pos, Mathf.Min(size, data.Length - pos));
                pos += size;
            }
            return stream.ToArray();
        }
    }

    static int[] RowOrder(int height, bool interlaced)
    {
        int[] order = new int[height];
        if (!interlaced)
        {
            for (int i = 0; i < height; i++) order[i] = i;
            return order;
        }
        int n = 0;
        int[][] passes = { new[] { 0, 8 }, new[] { 4, 8 }, new[] { 2, 4 }, new[] { 1, 2 } };
        foreach (int[] pass in passes)
            for (int row = pass[0]; row < height; row += pass[1]) order[n++] = row;
        return order;
    }

    static byte[] DecodeLzw(byte[] input, int minCodeSize, int pixelCount)
    {
        byte[] output = new byte[pixelCount];
        int clear = 1 << minCodeSize, end = clear + 1;
        int codeSize = minCodeSize + 1, next = end + 1;
        int[] prefix = new int[4096];
        byte[] suffix = new byte[4096];
        byte[] stack = new byte[4097];
        for (int i = 0; i < clear; i++) suffix[i] = (byte)i;

        int bitBuffer = 0, bitCount = 0, bytePos = 0, outPos = 0;
        int prev = -1;
        byte first = 0;

        while (outPos < pixelCount)
        {
            while (bitCount < codeSize)
            {
                if (bytePos >= input.Length) return output;
                bitBuffer |= input[bytePos++] << bitCount;
                bitCount += 8;
            }
            int code = bitBuffer & ((1 << codeSize) - 1);
            bitBuffer >>= codeSize;
            bitCount -= codeSize;

            if (code == clear)
            {
                codeSize = minCodeSize + 1;
                next = end + 1;
                prev = -1;
                continue;
            }
            if (code == end) break;

            if (prev == -1)
            {
                if (code >= clear) break;  // 잘못된 데이터
                output[outPos++] = suffix[code];
                first = suffix[code];
                prev = code;
                continue;
            }

            int inCode = code;
            int top = 0;
            if (code >= next)
            {
                stack[top++] = first;
                code = prev;
            }
            while (code >= clear)
            {
                if (top >= stack.Length - 1 || code >= 4096) return output;
                stack[top++] = suffix[code];
                code = prefix[code];
            }
            first = suffix[code];
            stack[top++] = first;

            while (top > 0 && outPos < pixelCount) output[outPos++] = stack[--top];

            if (next < 4096)
            {
                prefix[next] = prev;
                suffix[next] = first;
                next++;
                if (next == (1 << codeSize) && codeSize < 12) codeSize++;
            }
            prev = inCode;
        }
        return output;
    }
}
