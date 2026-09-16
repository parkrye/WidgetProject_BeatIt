using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace BeatIt.Core;

/// <summary>
/// GIF를 프레임 단위로 미리 합성해두고 재생한다.
/// GifBitmapDecoder 가 주는 프레임은 부분 갱신본이라, disposal 규칙에 맞춰 직접 겹쳐야 제대로 보인다.
/// </summary>
public sealed class AnimatedGifPlayer : IDisposable
{
    private const int DisposalRestoreBackground = 2;
    private const int DisposalRestorePrevious = 3;

    private readonly IReadOnlyList<BitmapSource> _frames;
    private readonly IReadOnlyList<TimeSpan> _delays;
    private readonly DispatcherTimer _timer;

    private int _index;
    private bool _disposed;

    private AnimatedGifPlayer(IReadOnlyList<BitmapSource> frames, IReadOnlyList<TimeSpan> delays)
    {
        _frames = frames;
        _delays = delays;
        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = delays[0] };
        _timer.Tick += OnTick;
    }

    public event EventHandler? FrameChanged;

    public ImageSource Current => _frames[_index];

    public static bool IsGif(string path) =>
        Path.GetExtension(path).Equals(".gif", StringComparison.OrdinalIgnoreCase);

    /// <summary>GIF 로드에 실패하거나 프레임이 한 장뿐이면 null 을 돌려준다(정지 이미지로 처리하면 된다).</summary>
    public static AnimatedGifPlayer? TryCreate(string path)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            GifBitmapDecoder decoder = new(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count <= 1)
            {
                return null;
            }

            (List<BitmapSource> frames, List<TimeSpan> delays) = Compose(decoder);
            return frames.Count <= 1 ? null : new AnimatedGifPlayer(frames, delays);
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or FileFormatException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>첫 프레임부터 다시 재생한다.</summary>
    public void Restart()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _timer.Stop();
        _index = 0;
        _timer.Interval = _delays[0];
        _timer.Start();
        FrameChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop() => _timer.Stop();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _index = (_index + 1) % _frames.Count;
        _timer.Interval = _delays[_index];
        FrameChanged?.Invoke(this, EventArgs.Empty);
    }

    private static (List<BitmapSource> Frames, List<TimeSpan> Delays) Compose(GifBitmapDecoder decoder)
    {
        (int width, int height) = ReadScreenSize(decoder);
        List<BitmapSource> frames = new(decoder.Frames.Count);
        List<TimeSpan> delays = new(decoder.Frames.Count);
        Rect canvas = new(0, 0, width, height);
        BitmapSource? carried = null;

        foreach (BitmapFrame frame in decoder.Frames)
        {
            BitmapMetadata? metadata = frame.Metadata as BitmapMetadata;
            Rect area = ReadFrameArea(metadata, frame);
            BitmapSource composed = Draw(canvas, visual =>
            {
                if (carried is not null)
                {
                    visual.DrawImage(carried, canvas);
                }

                visual.DrawImage(frame, area);
            });

            frames.Add(composed);
            delays.Add(ReadDelay(metadata));
            carried = NextCarried(metadata, canvas, area, carried, composed);
        }

        return (frames, delays);
    }

    private static BitmapSource? NextCarried(
        BitmapMetadata? metadata,
        Rect canvas,
        Rect area,
        BitmapSource? carried,
        BitmapSource composed)
    {
        int disposal = Read<byte>(metadata, "/grctlext/Disposal");
        if (disposal == DisposalRestorePrevious)
        {
            return carried;
        }

        if (disposal != DisposalRestoreBackground)
        {
            return composed;
        }

        // 이 프레임이 그린 영역만 투명으로 되돌린 상태를 다음 프레임의 바탕으로 쓴다.
        CombinedGeometry hole = new(
            GeometryCombineMode.Exclude,
            new RectangleGeometry(canvas),
            new RectangleGeometry(area));

        return Draw(canvas, visual =>
        {
            visual.PushClip(hole);
            visual.DrawImage(composed, canvas);
            visual.Pop();
        });
    }

    private static BitmapSource Draw(Rect canvas, Action<DrawingContext> paint)
    {
        DrawingVisual visual = new();
        using (DrawingContext context = visual.RenderOpen())
        {
            paint(context);
        }

        RenderTargetBitmap target = new(
            (int)canvas.Width,
            (int)canvas.Height,
            96,
            96,
            PixelFormats.Pbgra32);
        target.Render(visual);
        target.Freeze();
        return target;
    }

    private static (int Width, int Height) ReadScreenSize(GifBitmapDecoder decoder)
    {
        BitmapMetadata? metadata = decoder.Metadata;
        int width = Read<ushort>(metadata, "/logscrdesc/Width");
        int height = Read<ushort>(metadata, "/logscrdesc/Height");
        if (width > 0 && height > 0)
        {
            return (width, height);
        }

        BitmapFrame first = decoder.Frames[0];
        return (first.PixelWidth, first.PixelHeight);
    }

    private static Rect ReadFrameArea(BitmapMetadata? metadata, BitmapFrame frame)
    {
        int left = Read<ushort>(metadata, "/imgdesc/Left");
        int top = Read<ushort>(metadata, "/imgdesc/Top");
        int width = Read<ushort>(metadata, "/imgdesc/Width");
        int height = Read<ushort>(metadata, "/imgdesc/Height");
        if (width <= 0 || height <= 0)
        {
            (width, height) = (frame.PixelWidth, frame.PixelHeight);
        }

        return new Rect(left, top, width, height);
    }

    private static TimeSpan ReadDelay(BitmapMetadata? metadata)
    {
        int hundredths = Read<ushort>(metadata, "/grctlext/Delay");

        // 0 또는 1은 "가능한 한 빠르게"라는 뜻인데, 브라우저들처럼 100ms 로 맞춰준다.
        return TimeSpan.FromMilliseconds(hundredths <= 1 ? 100 : hundredths * 10);
    }

    private static int Read<T>(BitmapMetadata? metadata, string query)
        where T : struct, IConvertible
    {
        if (metadata is null || !metadata.ContainsQuery(query))
        {
            return 0;
        }

        return metadata.GetQuery(query) is T value ? Convert.ToInt32(value) : 0;
    }
}
