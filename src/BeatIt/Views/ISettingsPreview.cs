using BeatIt.Models;

namespace BeatIt.Views;

/// <summary>설정 창이 값을 바꿀 때마다 위젯에 바로 비춰보게 한다.</summary>
public interface ISettingsPreview
{
    void Preview(AppSettings settings);
}
