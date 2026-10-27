using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// SCI-FI UI Pack의 폰트(Play, Aldrich)에는 한글이 없어서, TMP 텍스트에 한글 폰트를 대체(fallback)로 붙여 준다.
/// 한글 폰트는 이 순서로 찾는다.
///  1) Assets/Resources/Fonts/KoreanFallback (TMP_FontAsset) — 직접 만든 한글 폰트 에셋이 있으면 최우선.
///  2) 실행 중인 PC에 설치된 한글 폰트(맑은 고딕 등)를 파일에서 읽어 동적으로 만든다. 폰트 파일을 게임에 넣지 않으므로 임시 방편이다.
/// 팩의 폰트 에셋 자체는 수정하지 않고, 복제본에 대체 폰트를 붙여서 쓴다.
/// </summary>
public static class GameFonts
{
    private const string BundledAssetPath = "Fonts/KoreanFallback";

    // 한글 글리프가 있는 대표적인 OS 폰트 파일 (위에서부터 먼저 찾은 것을 사용)
    private static readonly string[] OsFontFiles =
    {
        @"C:\Windows\Fonts\malgun.ttf",
        @"C:\Windows\Fonts\NanumGothic.ttf",
        "/System/Library/Fonts/AppleSDGothicNeo.ttc",
        "/Library/Fonts/AppleGothic.ttf",
        "/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc",
        "/usr/share/fonts/truetype/noto/NotoSansKR-Regular.ttf",
    };

    private static TMP_FontAsset _korean;
    private static bool _koreanSearched;
    private static readonly Dictionary<TMP_FontAsset, TMP_FontAsset> _withKorean = new Dictionary<TMP_FontAsset, TMP_FontAsset>();

    // 에디터에서 도메인 리로드를 꺼 둬도 플레이마다 깨끗이 시작하도록
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _korean = null;
        _koreanSearched = false;
        _withKorean.Clear();
    }

    /// <summary>텍스트의 폰트를 "원래 폰트 + 한글 대체 폰트"로 바꾼다.</summary>
    public static void ApplyKorean(TMP_Text text)
    {
        if (text != null && text.font != null)
            text.font = WithKorean(text.font);
    }

    public static TMP_FontAsset WithKorean(TMP_FontAsset font)
    {
        TMP_FontAsset korean = GetKorean();
        if (korean == null || font == null)
            return font;

        if (_withKorean.TryGetValue(font, out TMP_FontAsset cached) && cached != null)
            return cached;

        TMP_FontAsset clone = Object.Instantiate(font);
        clone.name = font.name + " (KR)";
        clone.fallbackFontAssetTable = new List<TMP_FontAsset>(font.fallbackFontAssetTable ?? new List<TMP_FontAsset>()) { korean };

        _withKorean[font] = clone;
        return clone;
    }

    private static TMP_FontAsset GetKorean()
    {
        if (_koreanSearched)
            return _korean;

        _koreanSearched = true;

        _korean = Resources.Load<TMP_FontAsset>(BundledAssetPath);
        if (_korean != null)
            return _korean;

        foreach (string path in OsFontFiles)
        {
            if (!File.Exists(path))
                continue;

            var font = new Font(path);
            _korean = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (_korean != null)
            {
                _korean.name = "Korean OS Fallback";
                return _korean;
            }
        }

        Debug.LogWarning("[GameFonts] 한글 폰트를 찾지 못해 한글이 표시되지 않을 수 있습니다. " +
                         $"한글 TMP 폰트 에셋을 Assets/Resources/{BundledAssetPath}에 두세요.");
        return null;
    }
}
