using System.Windows;

namespace KDS.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(IServiceProvider serviceProvider) : this()
    {
        blazorWebView.Services = serviceProvider;
    }
}
