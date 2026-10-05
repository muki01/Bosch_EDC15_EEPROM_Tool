using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Edc15EepromTool.Behaviors;

/// <summary>Attached properties for text boxes: an error flag for the template and a digits-only filter.</summary>
public static class InputAssist
{
    public static readonly DependencyProperty HasErrorProperty = DependencyProperty.RegisterAttached(
        "HasError", typeof(bool), typeof(InputAssist), new FrameworkPropertyMetadata(false));

    public static bool GetHasError(DependencyObject element) => (bool)element.GetValue(HasErrorProperty);

    public static void SetHasError(DependencyObject element, bool value) => element.SetValue(HasErrorProperty, value);

    public static readonly DependencyProperty DigitsOnlyProperty = DependencyProperty.RegisterAttached(
        "DigitsOnly", typeof(bool), typeof(InputAssist), new PropertyMetadata(false, OnDigitsOnlyChanged));

    public static bool GetDigitsOnly(DependencyObject element) => (bool)element.GetValue(DigitsOnlyProperty);

    public static void SetDigitsOnly(DependencyObject element, bool value) => element.SetValue(DigitsOnlyProperty, value);

    private static void OnDigitsOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox box)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            box.PreviewTextInput += OnPreviewTextInput;
            DataObject.AddPastingHandler(box, OnPaste);
            InputMethod.SetIsInputMethodEnabled(box, false);
        }
        else
        {
            box.PreviewTextInput -= OnPreviewTextInput;
            DataObject.RemovePastingHandler(box, OnPaste);
        }
    }

    /// <summary>
    /// Keeps a "0" in front of what the user types, as the login code is always written 0XXXX.
    /// Only typing is corrected; values set by the program are shown unchanged.
    /// </summary>
    public static readonly DependencyProperty LeadingZeroProperty = DependencyProperty.RegisterAttached(
        "LeadingZero", typeof(bool), typeof(InputAssist), new PropertyMetadata(false, OnLeadingZeroChanged));

    public static bool GetLeadingZero(DependencyObject element) => (bool)element.GetValue(LeadingZeroProperty);

    public static void SetLeadingZero(DependencyObject element, bool value) => element.SetValue(LeadingZeroProperty, value);

    private static void OnLeadingZeroChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox box)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            box.TextChanged += OnLeadingZeroTextChanged;
        }
        else
        {
            box.TextChanged -= OnLeadingZeroTextChanged;
        }
    }

    private static void OnLeadingZeroTextChanged(object sender, TextChangedEventArgs e)
    {
        var box = (TextBox)sender;
        if (!box.IsKeyboardFocused || box.Text.StartsWith("0", StringComparison.Ordinal))
        {
            return;
        }

        var caret = box.CaretIndex;
        var text = "0" + box.Text.TrimStart('0');
        if (box.MaxLength > 0 && text.Length > box.MaxLength)
        {
            text = text.Substring(0, box.MaxLength);
        }
        box.Text = text;
        box.CaretIndex = Math.Min(text.Length, caret + 1);
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e) => e.Handled = !e.Text.All(IsAsciiDigit);

    private static void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetData(DataFormats.UnicodeText) is not string text)
        {
            e.CancelCommand();
            return;
        }

        // keep the digits of a pasted value such as "169,341 km"
        var digits = new string(text.Where(IsAsciiDigit).ToArray());
        if (digits.Length == 0)
        {
            e.CancelCommand();
            return;
        }

        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, digits);
        e.DataObject = data;
    }

    private static bool IsAsciiDigit(char c) => c is >= '0' and <= '9';
}
