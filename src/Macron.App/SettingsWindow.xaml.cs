namespace Macron.App;

using System.IO;
using System.Windows;

public partial class SettingsWindow : Window
{
    public string AppDataFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Macron");

    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void OnClose(object sender, System.Windows.RoutedEventArgs e)
    {
        Close();
    }
}
