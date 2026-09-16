using System.Windows.Media;

namespace BeatIt.Core;

/// <summary>위젯이 지금 어떤 상태인지.</summary>
public enum SpriteState
{
    /// <summary>가만히 있다.</summary>
    Idle,

    /// <summary>끌려가는 중.</summary>
    Moving,
}

/// <summary>위젯이 지금 무엇을 그려야 하는지 알려준다.</summary>
public interface ISpriteSource : IDisposable
{
    ImageSource Current { get; }

    /// <summary>그릴 그림이 바뀌었을 때 발생한다. 이미지 교체와 GIF 프레임 진행 둘 다 해당한다.</summary>
    event EventHandler? CurrentChanged;

    /// <summary>한 대 맞았다.</summary>
    void OnHit();

    /// <summary>프레임마다 호출한다. 시간이 흐르고 상태가 바뀐 걸 알린다.</summary>
    void Update(double deltaSeconds, SpriteState state);
}
