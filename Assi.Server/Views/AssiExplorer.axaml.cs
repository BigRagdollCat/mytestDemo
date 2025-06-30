using Assi.Server.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Assi.Server;

public partial class AssiExplorer : Window
{
    public AssiExplorerViewModel AEVM { get; set; }
    public AssiExplorer(StudentCard student)
    {
        InitializeComponent();
        AEVM = new AssiExplorerViewModel(student);
        this.DataContext = AEVM;
    }

}