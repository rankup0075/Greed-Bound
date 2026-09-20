using UnityEditor;
using UnityEngine;

// 픽셀아트 스프라이트 임포트 규칙 (Spec 8장 "픽셀아트 — Unity 구현 규칙").
// Assets/Art/Sprites/ 아래에 새로 들어온 이미지에만 적용: Sprite, PPU 36, Point 필터, 압축 없음, Mipmap 끔, Pivot 발 가운데.
// 이미 가져온 이미지는 건드리지 않으므로, 슬라이스·Pivot을 Inspector에서 고친 값이 덮어써지지 않음.
public class PixelArtImportRules : AssetPostprocessor
{
    const string SpriteFolder = "Assets/Art/Sprites/";
    // 상점·로비 소품은 실행 중에 Resources.Load 로 읽어서 여기 있다 (같은 규칙 적용)
    const string PropFolder = "Assets/Resources/Props/";
    const int PixelsPerUnit = 36;

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(SpriteFolder) && !assetPath.StartsWith(PropFolder)) return;
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
        // 캐릭터는 발이 기준점(그림 크기가 달라도 발이 땅에 닿게).
        // 배경·지형은 가운데 기준 — SpriteRenderer 의 Tiled 모드로 늘릴 때
        // 발 기준이면 늘어나는 방향이 한쪽으로 쏠린다
        settings.spriteAlignment = assetPath.StartsWith(SpriteFolder + "Environment/")
            ? (int)SpriteAlignment.Center : (int)SpriteAlignment.BottomCenter;
        settings.spriteMeshType = SpriteMeshType.FullRect;   // Tiled 모드에 필요
        importer.SetTextureSettings(settings);
    }
}
