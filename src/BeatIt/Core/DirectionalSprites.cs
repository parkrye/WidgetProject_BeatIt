namespace BeatIt.Core;

/// <summary>
/// 방향마다 다른 그림 묶음. 요청한 방향이 비어 있으면 default 로, default 도 비어 있으면 대체 목록으로 떨어진다.
/// 폴백까지 같은 목록 인스턴스를 돌려주기 때문에, 부르는 쪽은 참조 비교만으로 "그림이 실제로 바뀌는지" 알 수 있다.
/// </summary>
public sealed class DirectionalSprites
{
    private readonly IReadOnlyDictionary<FacingDirection, IReadOnlyList<Sprite>> _byDirection;
    private readonly IReadOnlyList<Sprite> _fallback;

    public DirectionalSprites(
        IReadOnlyDictionary<FacingDirection, IReadOnlyList<Sprite>> byDirection,
        IReadOnlyList<Sprite> fallback)
    {
        _byDirection = byDirection;
        _fallback = fallback;
    }

    /// <summary>방향 폴더에서 읽은 게 하나도 없다. 전부 대체 목록으로 간다.</summary>
    public bool IsEmpty => _byDirection.Count == 0;

    /// <summary>이 묶음이 들고 있는 그림 전부. 대체 목록은 빼고 센다.</summary>
    public IEnumerable<Sprite> Own => _byDirection.Values.SelectMany(sprites => sprites);

    public static DirectionalSprites Fallback(IReadOnlyList<Sprite> sprites) =>
        new(new Dictionary<FacingDirection, IReadOnlyList<Sprite>>(), sprites);

    /// <summary>이 방향에 쓸 후보. 없으면 default, 그것도 없으면 대체 목록.</summary>
    public IReadOnlyList<Sprite> For(FacingDirection direction)
    {
        if (_byDirection.TryGetValue(direction, out IReadOnlyList<Sprite>? matched))
        {
            return matched;
        }

        if (_byDirection.TryGetValue(FacingDirection.Default, out IReadOnlyList<Sprite>? shared))
        {
            return shared;
        }

        return _fallback;
    }
}
