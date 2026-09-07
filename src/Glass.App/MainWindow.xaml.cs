using Glass.App.Configuration;
using Microsoft.UI.Xaml;

namespace Glass.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = ProductBranding.DevelopmentWindowTitle;
    }
}
