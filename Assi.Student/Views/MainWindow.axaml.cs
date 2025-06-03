using Assi.Student.ViewModels;
using Avalonia.Controls;

namespace Assi.Student.Views
{
    public partial class MainWindow : Window
    {
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
        }
    }
}