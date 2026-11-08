namespace Macron.App;

using System.Windows;

public partial class RenameWindow : Window
{
    public string NewName
    {
        get => NameBox.Text;
        set => NameBox.Text = value;
    }

    public RenameWindow()
    {
        InitializeComponent();
    }

    private void OnOk(object sender, System.Windows.RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void OnCancel(object sender, System.Windows.RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
