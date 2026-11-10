using System.Windows;
using CoordinatesTinyApp.ViewModels;
using CoordinatesTinyApp.Views;

namespace CoordinatesTinyApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel(new WindowDialogService(this));
    }
}
