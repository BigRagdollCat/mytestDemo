using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;

namespace Assi.Student;

public partial class PlayerView : Window
{
    public static int Width { get; set; }
    public static int Height { get; set; }
    public PlayerView()
    {
        InitializeComponent();
        var primaryScreen = Screens.Primary;
        if (primaryScreen != null)
        {
            var bounds = primaryScreen.Bounds;
            Width = bounds.Width;
            Height = bounds.Height;
        }
    }
}