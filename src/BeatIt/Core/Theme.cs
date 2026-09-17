using System.IO;
using System.Windows.Media;

namespace BeatIt.Core;

/// <summary>
/// 타격 이펙트와 콤보 그림 묶음. 캐릭터와 따로 고른다.
/// 콤보는 숫자 이미지가 있으면 그걸, 없고 폰트가 있으면 그 폰트를, 둘 다 없으면 기본 글꼴로 그린다.
/// </summary>
public sealed class Theme
{
    private static readonly string[] FontExtensions = [".ttf", ".otf"];

    private Theme(string name, IReadOnlyList<ImageSource> effects, IReadOnlyList<ImageSource>? digits, ImageSource? label, FontFamily? font)
    {
        Name = name;
        Effects = effects;
        Digits = digits;
        Label = label;
        Font = font;
    }

    public string Name { get; }

    /// <summary>때릴 때마다 이 중 하나가 튄다. 비어 있으면 이펙트 없이 간다.</summary>
    public IReadOnlyList<ImageSource> Effects { get; }

    /// <summary>0~9 이미지. 열 장이 다 있을 때만 값이 들어온다.</summary>
    public IReadOnlyList<ImageSource>? Digits { get; }

    /// <summary>COMBO 라벨 그림. 없으면 글자로 그린다.</summary>
    public ImageSource? Label { get; }

    public FontFamily? Font { get; }

    /// <summary>아무것도 없는 테마. 이펙트를 안 띄우고 콤보는 기본 글꼴로 그린다.</summary>
    public static Theme Empty() => new("없음", [], null, null, null);

    /// <summary>폴더만 보고 알아낸 테마의 생김새. 그림은 안 읽는다.</summary>
    public sealed record Shape(string Name, int EffectCount, bool HasDigits, bool HasFont);

    /// <summary>
    /// 설정 창 목록에 줄 하나 적자고 그림을 전부 디코드할 일은 아니다.
    /// 파일이 있는지만 보고 어떤 테마인지 알아본다.
    /// </summary>
    public static Shape? Inspect(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return null;
        }

        string comboFolder = Path.Combine(folder, "combo");
        return new Shape(
            new DirectoryInfo(folder).Name,
            ImagePathsIn(Path.Combine(folder, "effects")).Count,
            HasAllDigits(comboFolder),
            HasFont(comboFolder));
    }

    public static Theme? Load(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return null;
        }

        string effectsFolder = Path.Combine(folder, "effects");
        string comboFolder = Path.Combine(folder, "combo");

        return new Theme(
            new DirectoryInfo(folder).Name,
            LoadEffects(effectsFolder),
            LoadDigits(comboFolder),
            LoadImage(Path.Combine(comboFolder, "label.png")),
            LoadFont(comboFolder));
    }

    private static IReadOnlyList<ImageSource> LoadEffects(string folder)
    {
        List<ImageSource> effects = [];
        foreach (string path in ImagePathsIn(folder))
        {
            // 이펙트는 동시에 여러 개가 떠서 각자 재생 상태를 가질 수 없다. GIF 는 첫 프레임만 쓴다.
            ImageSource? image = LoadImage(path);
            if (image is not null)
            {
                effects.Add(image);
            }
        }

        return effects;
    }

    private static IReadOnlyList<string> ImagePathsIn(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        return
        [
            .. Directory
                .EnumerateFiles(folder)
                .Where(path => CharacterFolder.Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
        ];
    }

    /// <summary>0~9 가 다 놓여 있는지만 본다. 읽히는지는 <see cref="LoadDigits"/> 가 따진다.</summary>
    private static bool HasAllDigits(string folder) =>
        Directory.Exists(folder)
        && Enumerable
            .Range(0, 10)
            .All(number => CharacterFolder.Extensions.Any(extension => File.Exists(Path.Combine(folder, number + extension))));

    private static bool HasFont(string folder) =>
        Directory.Exists(folder)
        && Directory
            .EnumerateFiles(folder)
            .Any(path => FontExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase));

    /// <summary>0~9 가 다 있어야 숫자 그림으로 그린다. 하나라도 빠지면 글자로 떨어진다.</summary>
    private static IReadOnlyList<ImageSource>? LoadDigits(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return null;
        }

        List<ImageSource> digits = [];
        for (int number = 0; number <= 9; number++)
        {
            ImageSource? image = FindDigit(folder, number);
            if (image is null)
            {
                return null;
            }

            digits.Add(image);
        }

        return digits;
    }

    private static ImageSource? FindDigit(string folder, int number)
    {
        foreach (string extension in CharacterFolder.Extensions)
        {
            ImageSource? image = LoadImage(Path.Combine(folder, number + extension));
            if (image is not null)
            {
                return image;
            }
        }

        return null;
    }

    private static ImageSource? LoadImage(string path) =>
        File.Exists(path) ? Sprite.Load(path)?.Current : null;

    private static FontFamily? LoadFont(string folder)
    {
        if (!HasFont(folder))
        {
            return null;
        }

        try
        {
            // 폴더 URI 를 주면 그 안의 글꼴 파일을 훑어준다. 끝의 구분자가 없으면 폴더로 안 본다.
            Uri baseUri = new(folder.EndsWith(Path.DirectorySeparatorChar) ? folder : folder + Path.DirectorySeparatorChar);
            return Fonts.GetFontFamilies(baseUri).FirstOrDefault();
        }
        catch (Exception ex) when (ex is UriFormatException or IOException)
        {
            return null;
        }
    }
}
