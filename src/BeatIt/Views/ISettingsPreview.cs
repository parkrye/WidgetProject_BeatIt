using BeatIt.Models;

namespace BeatIt.Views;

/// <summary>설정 창이 값을 바꿀 때마다 위젯에 바로 비춰보게 한다.</summary>
public interface ISettingsPreview
{
    void Preview(AppSettings settings);

    /// <summary>
    /// 쥐고 있던 소리 파일을 놓는다. 캐릭터를 고치는 창을 열기 전에 부른다.
    /// 소리는 첫 소리가 늦지 않게 미리 열어두는 탓에, 놓아주지 않으면 지금 쓰는 캐릭터의
    /// 소리를 빼지도 갈아 끼우지도 못한다. 그림은 읽을 때 통째로 받아두니 잠기지 않는다.
    /// </summary>
    void ReleaseAudio();

    /// <summary>
    /// 쥐고 있던 테마 글꼴을 놓는다. 테마를 고치는 창을 열기 전에 부른다.
    /// 글꼴은 폴더째 훑어 읽는 탓에 그리는 동안 파일이 잠길 수 있다.
    /// 놓으면 그동안 이펙트가 안 뜨고 콤보가 기본 글꼴로 그려지지만, 닫으면 다시 읽는다.
    /// </summary>
    void ReleaseTheme();

    /// <summary>
    /// 캐릭터와 테마를 경로가 그대로여도 다시 읽는다. 관리 창에서 파일을 고치고 나면 부른다.
    /// 평소의 <see cref="Preview"/> 는 경로가 같으면 다시 안 읽기 때문에, 방금 넣은 그림과
    /// 소리가 앱을 껐다 켜야 나오는 일이 생긴다.
    /// </summary>
    void ReloadAssets();
}
