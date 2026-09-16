using System.Windows.Media;

namespace BeatIt.Core;

/// <summary>모드 1 - 이미지 한 장을 계속 꾸겨서 쓴다. GIF면 알아서 계속 돌아간다.</summary>
public sealed class SingleSpriteSource : ISpriteSource
{
    private readonly Sprite _sprite;

    public SingleSpriteSource(Sprite sprite)
    {
        _sprite = sprite;
        _sprite.CurrentChanged += OnSpriteChanged;
        _sprite.Play();
    }

    public event EventHandler? CurrentChanged;

    public ImageSource Current => _sprite.Current;

    public void OnHit()
    {
        // 그림은 그대로 두고 꾸기기만 한다. 교체할 게 없다.
    }

    public void Dispose()
    {
        _sprite.CurrentChanged -= OnSpriteChanged;
        _sprite.Dispose();
    }

    private void OnSpriteChanged(object? sender, EventArgs e) => CurrentChanged?.Invoke(this, EventArgs.Empty);
}
