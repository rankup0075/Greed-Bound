using UnityEditor;
using UnityEngine;

// 픽셀아트 스프라이트 임포트 규칙 (Spec 8장 "픽셀아트 — Unity 구현 규칙").
// Assets/Art/Sprites/ 아래에 새로 들어온 이미지에만 적용: Sprite, PPU 36, Point 필터, 압축 없음, Mipmap 끔, Pivot 발 가운데.
// 이미 가져온 이미지는 건드리지 않으므로, 슬라이스·Pivot을 Inspector에서 고친 값이 덮어써지지 않음.
public class PixelArtImportRules : AssetPostprocessor
{
    const string SpriteFolder = "Assets/Art/Sprites/";
    const int PixelsPerUnit = 36;

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(SpriteFolder)) return;
        if (!assetImporter.importSettingsMissing) return;  // 처음 가져올 때만

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
    }
}
