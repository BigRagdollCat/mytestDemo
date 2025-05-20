using Avalonia.Controls;

namespace Assi.Server.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
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