using Assi.Student.ViewModels;
using Avalonia.Controls;

namespace Assi.Student.Views
{
    public partial class MainWindow : Window
    {
        public static int Width { get; set; }
        public static int Height { get; set; }
        public static PlayerView PlayerView { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            if (PlayerView == null)
            {
                PlayerView = new PlayerView()
                {
                    DataContext = PlayerViewModel.Instance
                };
            }
            var primaryScreen = Screens.Primary;
            if (primaryScreen != null)
            {
                var bounds = primaryScreen.Bounds;
                Width = bounds.Width;
                Height = bounds.Height;
            }
        }
    }
}