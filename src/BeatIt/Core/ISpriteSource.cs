using System.Windows.Media;

namespace BeatIt.Core;

/// <summary>위젯이 지금 무엇을 그려야 하는지 알려준다. 모드별 구현이 갈리는 지점.</summary>
public interface ISpriteSource : IDisposable
{
    ImageSource Current { get; }

    /// <summary>GIF 프레임 진행이나 이미지 교체로 <see cref="Current"/> 가 바뀌었을 때 발생한다.</summary>
    event EventHandler? CurrentChanged;

    /// <summary>한 대 맞았다.</summary>
    void OnHit();
}
