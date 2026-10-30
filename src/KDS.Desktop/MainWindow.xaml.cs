using System.Windows;

namespace KDS.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        this.KeyDown += (s, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                this.Close();
            }
        };
    }

    public MainWindow(IServiceProvider serviceProvider) : this()
    {
        blazorWebView.Services = serviceProvider;
    }
}
