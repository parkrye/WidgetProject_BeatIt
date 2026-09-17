using System.IO;

namespace BeatIt.Core;

/// <summary>테마 칸에 파일이 들어가는 방식.</summary>
public enum ThemeSlotKind
{
    /// <summary>이름 제한 없이 여러 장이 들어간다. 이펙트가 여기 해당한다.</summary>
    Gallery,

    /// <summary>
    /// 이름이 정해진 자리에 한 장씩 들어간다. 콤보 숫자(<c>0</c>~<c>9</c>)와 라벨(<c>label</c>)이 그렇다.
    /// 확장자는 자유라 <c>3.png</c> 든 <c>3.gif</c> 든 같은 자리로 본다.
    /// </summary>
    Fixed,

    /// <summary>글꼴 파일이 들어간다. 여러 장을 넣어도 첫 장만 쓴다.</summary>
    Font,
}

/// <summary>
/// 테마 폴더 안에서 그림이나 글꼴이 들어가는 칸 하나.
/// 캐릭터와 달리 콤보는 파일 이름이 정해져 있어서, 그 자리를 <see cref="Places"/> 로 들고 있는다.
/// </summary>
/// <param name="Group">편집기 목록에서 묶이는 머리.</param>
/// <param name="Label">칸 이름.</param>
/// <param name="RelativePath">테마 폴더 기준 상대 경로.</param>
/// <param name="Kind">파일이 들어가는 방식.</param>
/// <param name="Places"><see cref="ThemeSlotKind.Fixed"/> 일 때 자리 이름. 그 밖에는 비어 있다.</param>
/// <param name="Hint">커서를 올렸을 때 보여줄 설명.</param>
public sealed record ThemeSlot(
    string Group,
    string Label,
    string RelativePath,
    ThemeSlotKind Kind,
    IReadOnlyList<string> Places,
    string Hint)
{
    public override string ToString() => Label;

    /// <summary>이 칸이 테마 폴더 안에서 가리키는 실제 폴더.</summary>
    public string In(string themeFolder) => Path.Combine(themeFolder, RelativePath);

    /// <summary>이 칸에 들어갈 수 있는 확장자.</summary>
    public string[] Extensions => Kind == ThemeSlotKind.Font ? FontExtensions : CharacterFolder.Extensions;

    /// <summary>글꼴로 쓸 수 있는 확장자. WPF 가 폴더에서 읽어주는 건 이 둘이다.</summary>
    public static readonly string[] FontExtensions = [".ttf", ".otf"];
}

/// <summary>테마가 가질 수 있는 칸 전부. 편집기에 뜨는 순서이기도 하다.</summary>
public static class ThemeSlots
{
    private const string EffectGroup = "타격 이펙트";
    private const string ComboGroup = "콤보";

    public static IReadOnlyList<ThemeSlot> All { get; } =
    [
        new(EffectGroup, "이펙트", "effects", ThemeSlotKind.Gallery, [],
            "때릴 때마다 이 중 하나가 튄다. 장수 제한은 없고 이름도 자유다. 비워두면 이펙트가 안 뜬다. "
            + "여러 개가 동시에 뜰 수 있어 각자 재생 상태를 가질 수 없으니, GIF 를 넣으면 첫 프레임만 쓴다."),

        new(ComboGroup, "숫자 0~9", "combo", ThemeSlotKind.Fixed, ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9"],
            "콤보 수를 그릴 숫자 그림. 열 자리가 전부 차 있어야 그림으로 그리고, 하나라도 비면 폰트나 기본 글꼴로 떨어진다. "
            + "자리를 고르고 [이 자리에 넣기] 를 누르면 그 숫자 이름으로 베껴 들어간다."),

        new(ComboGroup, "라벨", "combo", ThemeSlotKind.Fixed, ["label"],
            "숫자 밑의 COMBO 자리에 들어갈 그림. 비워두면 글자로 그린다."),

        new(ComboGroup, "폰트", "combo", ThemeSlotKind.Font, [],
            "숫자 그림이 없을 때 콤보를 그릴 글꼴(.ttf/.otf). 여러 개를 넣어도 첫 장만 쓴다. "
            + "숫자 그림이 열 자리 다 차 있으면 글꼴은 안 쓴다."),
    ];

    /// <summary>콤보 숫자 자리. 열 자리가 다 차야 그림으로 그린다.</summary>
    public static ThemeSlot Digits => All[1];
}
