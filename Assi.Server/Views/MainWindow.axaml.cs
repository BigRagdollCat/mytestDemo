using Avalonia.Controls;

namespace Assi.Server.Views
{
    public partial class MainWindow : Window
    {
        public static int Width { get; set; }
        public static int Height { get; set; }
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var primaryScreen = Screens.All[0];
            if (primaryScreen != null)
            {
                var bounds = primaryScreen.Bounds;
                Width = bounds.Width;
                Height = bounds.Height;
            }
        }

        private void StackPanel_PointerEntered(object? sender, Avalonia.Input.PointerEventArgs e)
        {
            this.GroupBor.Width = 200;
        }

        private void Button_PointerExited_1(object? sender, Avalonia.Input.PointerEventArgs e)
        {
            this.GroupBor.Width = 0;
        }

        private void ListBox_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
        {
            if (this.ItemList.SelectedItems?.Count > 1) 
            {
                this.AddGroupBtn.IsVisible = true;
            }
            else
            {
                this.AddGroupBtn.IsVisible = false;
            }
        }
    }
}