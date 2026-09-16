using System.IO;
using BeatIt.Core;

namespace BeatIt.Services;

/// <summary>테마 폴더 하나의 요약. 설정 창 목록에 쓴다.</summary>
public sealed record ThemeInfo(string Name, string Path, int EffectCount, string ComboKind)
{
    public string Summary => $"이펙트 {EffectCount}장 / 콤보 {ComboKind}";

    public override string ToString() => Name;
}

/// <summary>쓸 수 있는 테마 폴더를 찾아준다.</summary>
public static class ThemeLibrary
{
    /// <summary>테마가 모여 사는 곳. 기본 테마도 첫 실행 때 여기로 풀린다.</summary>
    public static string Root { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BeatIt",
        "themes");

    public static string? DefaultPath => Scan().FirstOrDefault()?.Path;

    public static IReadOnlyList<ThemeInfo> Scan()
    {
        if (!Directory.Exists(Root))
        {
            return [];
        }

        List<ThemeInfo> found = [];
        foreach (string folder in Directory.EnumerateDirectories(Root).OrderBy(System.IO.Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            ThemeInfo? info = Describe(folder);
            if (info is not null)
            {
                found.Add(info);
            }
        }

        return found;
    }

    public static ThemeInfo? Describe(string folder)
    {
        Theme? theme = Theme.Load(folder);
        if (theme is null)
        {
            return null;
        }

        string combo = theme.Digits is not null ? "숫자 이미지" : theme.Font is not null ? "폰트" : "기본 글꼴";
        return new ThemeInfo(theme.Name, System.IO.Path.GetFullPath(folder), theme.Effects.Count, combo);
    }

    public static void EnsureRoot() => Directory.CreateDirectory(Root);
}
