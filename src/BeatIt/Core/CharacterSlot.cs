using System.IO;

namespace BeatIt.Core;

/// <summary>
/// 캐릭터 폴더 안에서 그림이나 소리가 들어가는 칸 하나.
/// 편집기는 이 칸 단위로 파일을 넣고 뺀다.
/// </summary>
/// <param name="Group">편집기 목록에서 묶이는 머리.</param>
/// <param name="Label">칸 이름.</param>
/// <param name="RelativePath">캐릭터 폴더 기준 상대 경로.</param>
/// <param name="IsSound">그림이 아니라 소리가 들어가는 칸.</param>
/// <param name="Hint">커서를 올렸을 때 보여줄 설명.</param>
public sealed record CharacterSlot(string Group, string Label, string RelativePath, bool IsSound, string Hint)
{
    public override string ToString() => Label;

    /// <summary>이 칸이 캐릭터 폴더 안에서 가리키는 실제 폴더.</summary>
    public string In(string characterFolder) => Path.Combine(characterFolder, RelativePath);
}

/// <summary>캐릭터가 가질 수 있는 칸 전부. 편집기에 뜨는 순서이기도 하다.</summary>
public static class CharacterSlots
{
    private const string MoveGroup = "움직일 때";
    private const string BeatGroup = "맞을 때";
    private const string SoundGroup = "소리";

    public static IReadOnlyList<CharacterSlot> All { get; } =
    [
        new("대기", "idle", "idle", false,
            "가만히 있을 때 이 중 하나가 설정한 간격 안에서 랜덤하게 갈아 끼워진다. 이 칸만 필수다."),

        new(MoveGroup, "기본", Path.Combine("move", "default"), false,
            "방향을 안 따질 때 쓴다. 방향 칸이 비어 있으면 여기로 떨어지고, 여기도 비면 idle 로 때운다."),
        new(MoveGroup, "왼쪽", Path.Combine("move", "left"), false, "왼쪽으로 갈 때."),
        new(MoveGroup, "오른쪽", Path.Combine("move", "right"), false, "오른쪽으로 갈 때."),
        new(MoveGroup, "위", Path.Combine("move", "up"), false, "위로 갈 때."),
        new(MoveGroup, "아래", Path.Combine("move", "down"), false, "아래로 갈 때."),

        new(BeatGroup, "기본", Path.Combine("beat", "default"), false,
            "가운데를 맞았을 때. 방향 칸이 비어 있으면 여기로 떨어지고, 여기도 비면 idle 로 때운다."),
        new(BeatGroup, "왼쪽", Path.Combine("beat", "left"), false, "왼쪽을 맞았을 때."),
        new(BeatGroup, "오른쪽", Path.Combine("beat", "right"), false, "오른쪽을 맞았을 때."),
        new(BeatGroup, "위", Path.Combine("beat", "up"), false, "위쪽을 맞았을 때."),
        new(BeatGroup, "아래", Path.Combine("beat", "down"), false, "아래쪽을 맞았을 때."),

        new(SoundGroup, "대기 소리", Path.Combine("sounds", "idle"), true,
            "설정한 긴 간격마다 하나씩 무작위로 운다."),
        new(SoundGroup, "이동 소리", Path.Combine("sounds", "move"), true,
            "움직이기 시작할 때 한 번 난다. 가는 방향이 꺾일 때는 다시 안 난다."),
        new(SoundGroup, "타격 소리", Path.Combine("sounds", "beat"), true,
            "맞을 때마다 난다. 같은 파일이 다시 걸리면 처음부터 다시 난다."),
    ];

    /// <summary>필수인 칸. 여기가 비면 캐릭터로 안 쳐준다.</summary>
    public static CharacterSlot Idle => All[0];
}
