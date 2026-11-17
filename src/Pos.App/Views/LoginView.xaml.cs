using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Pos.App.ViewModels;

namespace Pos.App.Views;

public partial class LoginView : UserControl
{
    private Control? _activeInput;
    private bool _shift;

    public LoginView()
    {
        InitializeComponent();
        _activeInput = UsernameBox;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LoginViewModel viewModel)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(viewModel.Username))
        {
            PasswordBox.Focus();
            _activeInput = PasswordBox;
        }
        else
        {
            UsernameBox.Focus();
            _activeInput = UsernameBox;
        }

        UpdatePasswordHint();
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel viewModel)
        {
            viewModel.Password = PasswordBox.Password;
        }

        UpdatePasswordHint();
    }

    private void UpdatePasswordHint()
    {
        PasswordHint.Visibility = PasswordBox.Password.Length == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OnFieldGotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is Control control)
        {
            _activeInput = control;
        }
    }

    private void OnLoginFieldVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && sender is Control control)
        {
            _activeInput = control;
        }
    }

    private void OnUsernameArrowClick(object sender, RoutedEventArgs e)
    {
        PasswordBox.Focus();
        _activeInput = PasswordBox;
    }

    private void OnLoginArrowClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel viewModel)
        {
            viewModel.SignInCommand.Execute(null);
        }
    }

    private void OnToggleLetterKeyboard(object sender, RoutedEventArgs e)
    {
        KeyboardPanel.Visibility = Visibility.Visible;
        SymbolKeyboard.Visibility = Visibility.Collapsed;
        LetterKeyboard.Visibility = Visibility.Visible;
        FocusActiveInput();
    }

    private void OnToggleSymbolKeyboard(object sender, RoutedEventArgs e)
    {
        KeyboardPanel.Visibility = Visibility.Visible;
        LetterKeyboard.Visibility = Visibility.Collapsed;
        SymbolKeyboard.Visibility = Visibility.Visible;
        FocusActiveInput();
    }

    private void OnKeypadKeyClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        var tag = button.Tag as string ?? string.Empty;
        var content = button.Content?.ToString() ?? string.Empty;

        switch (tag)
        {
            case "char":
                Append(content);
                break;
            case "letter":
                Append(_shift ? content.ToUpperInvariant() : content.ToLowerInvariant());
                break;
            case "space":
                Append(" ");
                break;
            case "back":
                Backspace();
                break;
            case "enter":
                if (_activeInput == UsernameBox && !string.IsNullOrWhiteSpace(UsernameBox.Text))
                {
                    PasswordBox.Focus();
                    _activeInput = PasswordBox;
                }
                else if (DataContext is LoginViewModel viewModel)
                {
                    viewModel.SignInCommand.Execute(null);
                }
                break;
            case "next":
                PasswordBox.Focus();
                _activeInput = PasswordBox;
                break;
            case "left":
                MoveCaret(-1);
                break;
            case "right":
                MoveCaret(1);
                break;
            case "shift":
                ToggleShift();
                break;
            case "hide":
                KeyboardPanel.Visibility = Visibility.Collapsed;
                FocusActiveInput();
                break;
        }
    }

    private void Append(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        switch (_activeInput)
        {
            case TextBox textBox:
                textBox.Text += text;
                textBox.CaretIndex = textBox.Text.Length;
                break;
            case PasswordBox passwordBox:
                passwordBox.Password += text;
                break;
        }

        FocusActiveInput();
    }

    private void Backspace()
    {
        switch (_activeInput)
        {
            case TextBox textBox when textBox.Text.Length > 0:
                textBox.Text = textBox.Text[..^1];
                textBox.CaretIndex = textBox.Text.Length;
                break;
            case PasswordBox passwordBox when passwordBox.Password.Length > 0:
                passwordBox.Password = passwordBox.Password[..^1];
                break;
        }

        FocusActiveInput();
    }

    private void MoveCaret(int delta)
    {
        if (_activeInput is TextBox textBox)
        {
            textBox.CaretIndex = Math.Clamp(
                textBox.CaretIndex + delta,
                0,
                textBox.Text.Length);
        }

        FocusActiveInput();
    }

    private void FocusActiveInput()
    {
        _activeInput?.Focus();
    }

    private void ToggleShift()
    {
        _shift = !_shift;
        foreach (var key in FindVisualChildren<Button>(LetterKeyboard))
        {
            if (key.Tag as string != "letter")
            {
                continue;
            }

            var letter = key.Content?.ToString() ?? string.Empty;
            key.Content = _shift ? letter.ToUpperInvariant() : letter.ToLowerInvariant();
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
