using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using BeatIt.Models;
using Microsoft.Win32;

namespace BeatIt.Views;

/// <summary>이미지와 타격 감각을 고르는 설정 창.</summary>
public partial class SettingsWindow : Window
{
    private const string ImageFilter =
        "이미지 (*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|모든 파일 (*.*)|*.*";

    private readonly ObservableCollection<string> _sequence;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();

        Result = settings;
        _sequence = [.. settings.SequencePaths];
        SequenceList.ItemsSource = _sequence;

        SingleModeRadio.IsChecked = settings.Mode == SpriteMode.Single;
        SequenceModeRadio.IsChecked = settings.Mode == SpriteMode.Sequence;
        SinglePathBox.Text = settings.SinglePath ?? string.Empty;
        WidthSlider.Value = settings.WidgetWidth;
        ComboSlider.Value = settings.ComboTimeoutMs;
        TopmostCheck.IsChecked = settings.Topmost;

        UpdatePanels();
    }

    /// <summary>확인을 눌렀을 때 적용할 설정.</summary>
    public AppSettings Result { get; }

    private void OnModeChanged(object sender, RoutedEventArgs e) => UpdatePanels();

    private void OnBrowseSingle(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new() { Filter = ImageFilter, Title = "때릴 이미지 고르기" };
        if (dialog.ShowDialog(this) == true)
        {
            SinglePathBox.Text = dialog.FileName;
        }
    }

    private void OnClearSingle(object sender, RoutedEventArgs e) => SinglePathBox.Text = string.Empty;

    private void OnAddSequence(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Filter = ImageFilter,
            Title = "타격 이미지 추가 (여러 장 선택 가능)",
            Multiselect = true,
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        foreach (string path in dialog.FileNames.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            _sequence.Add(path);
        }
    }

    private void OnRemoveSequence(object sender, RoutedEventArgs e)
    {
        foreach (string path in SequenceList.SelectedItems.Cast<string>().ToList())
        {
            _sequence.Remove(path);
        }
    }

    private void OnClearSequence(object sender, RoutedEventArgs e) => _sequence.Clear();

    private void OnMoveUp(object sender, RoutedEventArgs e) => Move(-1);

    private void OnMoveDown(object sender, RoutedEventArgs e) => Move(1);

    private void Move(int offset)
    {
        int index = SequenceList.SelectedIndex;
        int target = index + offset;
        if (index < 0 || target < 0 || target >= _sequence.Count)
        {
            return;
        }

        _sequence.Move(index, target);
        SequenceList.SelectedIndex = target;
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        Result.Mode = SequenceModeRadio.IsChecked == true ? SpriteMode.Sequence : SpriteMode.Single;
        Result.SinglePath = string.IsNullOrWhiteSpace(SinglePathBox.Text) ? null : SinglePathBox.Text;
        Result.SequencePaths = [.. _sequence];
        Result.WidgetWidth = WidthSlider.Value;
        Result.ComboTimeoutMs = (int)ComboSlider.Value;
        Result.Topmost = TopmostCheck.IsChecked == true;
        DialogResult = true;
    }

    private void UpdatePanels()
    {
        bool sequence = SequenceModeRadio.IsChecked == true;
        SequencePanel.Visibility = sequence ? Visibility.Visible : Visibility.Collapsed;
        SinglePanel.Visibility = sequence ? Visibility.Collapsed : Visibility.Visible;
    }
}
