using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Assi.Server;

public partial class InputMsgWindow : Window
{
    public bool result { get; set; } = false;
    public string resultStr { get; set; } = string.Empty;   
    public InputMsgWindow()
    {
        InitializeComponent();
    }

    private void Button_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(this.natb.Text)) 
        {
            return;
        }
        result = true;
        resultStr = this.natb.Text;
        this.Close();
    }
}