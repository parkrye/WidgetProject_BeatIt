using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BeatIt.Core;

/// <summary>이미지 한 장. 정지 이미지든 GIF든 같은 얼굴로 다룬다.</summary>
public sealed class Sprite : IDisposable
{
    private readonly AnimatedGifPlayer? _player;
    private readonly ImageSource _still;

    private Sprite(ImageSource still, AnimatedGifPlayer? player)
    {
        _still = still;
        _player = player;

        if (_player is not null)
        {
            _player.FrameChanged += OnFrameChanged;
        }
    }

    public event EventHandler? CurrentChanged;

    public ImageSource Current => _player?.Current ?? _still;

    public bool IsAnimated => _player is not null;

    /// <summary>읽을 수 없는 파일이면 null.</summary>
    public static Sprite? Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        AnimatedGifPlayer? player = AnimatedGifPlayer.IsGif(path) ? AnimatedGifPlayer.TryCreate(path) : null;
        ImageSource? still = player is null ? LoadStill(path) : player.Current;
        if (still is null)
        {
            return null;
        }

        return new Sprite(still, player);
    }

    public static Sprite FromImage(ImageSource image) => new(image, null);

    /// <summary>애니메이션이면 처음부터 다시 돌린다. 정지 이미지면 아무 일도 없다.</summary>
    public void Play() => _player?.Restart();

    public void Pause() => _player?.Stop();

    public void Dispose()
    {
        if (_player is null)
        {
            return;
        }

        _player.FrameChanged -= OnFrameChanged;
        _player.Dispose();
    }

    private static ImageSource? LoadStill(string path)
    {
        try
        {
            BitmapImage image = new();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or FileFormatException or UriFormatException)
        {
            return null;
        }
    }

    private void OnFrameChanged(object? sender, EventArgs e) => CurrentChanged?.Invoke(this, EventArgs.Empty);
}
