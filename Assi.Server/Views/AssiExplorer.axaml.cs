using Assi.Server.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Assi.Server;

public partial class AssiExplorer : Window
{
    public AssiExplorerViewModel AEVM { get; set; }
    public AssiExplorer()
    {
        InitializeComponent();
        AEVM = new AssiExplorerViewModel();
        this.DataContext = AEVM;
    }

}