using System.Windows.Media;

namespace BeatIt.Core;

/// <summary>모드 2 - 맞을 때마다 다음 장으로 넘어간다. 장수 제한 없이 끝까지 가면 처음으로 돌아온다.</summary>
public sealed class SequenceSpriteSource : ISpriteSource
{
    private readonly IReadOnlyList<Sprite> _sprites;
    private int _index;

    public SequenceSpriteSource(IReadOnlyList<Sprite> sprites)
    {
        ArgumentOutOfRangeException.ThrowIfZero(sprites.Count);

        _sprites = sprites;
        foreach (Sprite sprite in _sprites)
        {
            sprite.CurrentChanged += OnSpriteChanged;
        }

        _sprites[0].Play();
    }

    public event EventHandler? CurrentChanged;

    public ImageSource Current => _sprites[_index].Current;

    public void OnHit()
    {
        if (_sprites.Count == 1)
        {
            _sprites[0].Play();
            CurrentChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        _sprites[_index].Pause();
        _index = (_index + 1) % _sprites.Count;
        _sprites[_index].Play();
        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        foreach (Sprite sprite in _sprites)
        {
            sprite.CurrentChanged -= OnSpriteChanged;
            sprite.Dispose();
        }
    }

    private void OnSpriteChanged(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, _sprites[_index]))
        {
            CurrentChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
