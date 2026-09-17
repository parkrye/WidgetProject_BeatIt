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

    /// <summary>기본 테마. 첫 폴더 하나만 읽어보면 되니 목록을 다 훑지 않는다.</summary>
    public static string? DefaultPath => Enumerate().FirstOrDefault()?.Path;

    public static IReadOnlyList<ThemeInfo> Scan() => [.. Enumerate()];

    public static ThemeInfo? Describe(string folder)
    {
        Theme.Shape? shape = Theme.Inspect(folder);
        if (shape is null)
        {
            return null;
        }

        string combo = shape.HasDigits ? "숫자 이미지" : shape.HasFont ? "폰트" : "기본 글꼴";
        return new ThemeInfo(shape.Name, System.IO.Path.GetFullPath(folder), shape.EffectCount, combo);
    }

    /// <summary>이름순으로 하나씩 읽어본다. 게으르게 돌기 때문에 첫 하나만 필요하면 거기서 멈춘다.</summary>
    private static IEnumerable<ThemeInfo> Enumerate()
    {
        if (!Directory.Exists(Root))
        {
            yield break;
        }

        foreach (string folder in Directory.EnumerateDirectories(Root).OrderBy(System.IO.Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            ThemeInfo? info = Describe(folder);
            if (info is not null)
            {
                yield return info;
            }
        }
    }

    public static void EnsureRoot() => Directory.CreateDirectory(Root);
}
