using System.Windows.Media;

namespace BeatIt.Core;

/// <summary>위젯이 지금 어떤 상태인지.</summary>
public enum SpriteState
{
    /// <summary>가만히 있다.</summary>
    Idle,

    /// <summary>끌려가거나 혼자 걸어가는 중.</summary>
    Moving,
}

/// <summary>위젯이 지금 무엇을 그리고 무슨 소리를 내야 하는지 알려준다.</summary>
public interface ISpriteSource : IDisposable
{
    ImageSource Current { get; }

    /// <summary>그릴 그림이 바뀌었을 때 발생한다. 이미지 교체와 GIF 프레임 진행 둘 다 해당한다.</summary>
    event EventHandler? CurrentChanged;

    /// <summary>한 대 맞았다. <paramref name="direction"/> 은 캐릭터 어느 쪽을 때렸는지.</summary>
    void OnHit(FacingDirection direction);

    /// <summary>프레임마다 호출한다. 시간이 흐르고 상태와 진행 방향이 바뀐 걸 알린다.</summary>
    void Update(double deltaSeconds, SpriteState state, FacingDirection direction);

    /// <summary>대기 중 그림을 갈아 끼우는 간격. 설정에서 바꾸면 다시 불러온다.</summary>
    void SetIdleInterval(double minSeconds, double maxSeconds);

    /// <summary>대기 중 소리를 내는 간격. 그림 교체와 따로 돈다.</summary>
    void SetIdleSoundInterval(double minSeconds, double maxSeconds);

    /// <summary>소리 크기(0~1)와 음소거.</summary>
    void SetVolume(double volume, bool muted);

    /// <summary>쥐고 있던 소리 파일을 놓는다. 캐릭터 폴더를 고치는 동안 잠겨 있으면 안 된다.</summary>
    void ReleaseAudio();
}
