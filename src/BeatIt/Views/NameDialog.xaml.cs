using System.Windows;

namespace BeatIt.Views;

/// <summary>이름 한 줄을 받아오는 작은 창. 캐릭터를 만들거나 이름을 바꿀 때 쓴다.</summary>
public partial class NameDialog : Window
{
    public NameDialog(string title, string prompt, string initial = "")
    {
        InitializeComponent();

        Title = title;
        Prompt.Text = prompt;
        Input.Text = initial;

        Loaded += (_, _) =>
        {
            Input.Focus();
            Input.SelectAll();
        };
    }

    /// <summary>확인을 눌렀을 때 적힌 이름. 앞뒤 공백은 떼고 준다.</summary>
    public string Value { get; private set; } = string.Empty;

    /// <summary>이름을 받아온다. 취소했으면 null.</summary>
    public static string? Ask(Window owner, string title, string prompt, string initial = "")
    {
        NameDialog dialog = new(title, prompt, initial) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.Value : null;
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        Value = Input.Text.Trim();
        DialogResult = true;
    }
}
