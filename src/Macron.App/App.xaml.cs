namespace Macron.App;

using Microsoft.Win32;

public partial class App : Application
{
    private MacroSession? _session;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _session = new MacroSession();
        var viewModel = new MainViewModel(_session);
        var mainWindow = new MainWindow(viewModel, _session);

        viewModel.ShowSaveFileDialog = initial =>
        {
            var dlg = new SaveFileDialog
            {
                FileName = initial,
                Filter = "Макрос Macron (*.macron.json)|*.macron.json|Все файлы (*.*)|*.*",
                DefaultExt = ".macron.json"
            };
            return dlg.ShowDialog(mainWindow) == true ? dlg.FileName : null;
        };

        viewModel.ShowOpenFileDialog = () =>
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Макрос Macron (*.macron.json)|*.macron.json|Все файлы (*.*)|*.*",
                DefaultExt = ".macron.json"
            };
            return dlg.ShowDialog(mainWindow) == true ? dlg.FileName : null;
        };

        viewModel.ShowSettingsWindow = () =>
        {
            var dlg = new SettingsWindow { Owner = mainWindow };
            dlg.ShowDialog();
        };

        MainWindow = mainWindow;
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _session?.Dispose();
        base.OnExit(e);
    }
}
