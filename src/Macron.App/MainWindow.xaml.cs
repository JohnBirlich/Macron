namespace Macron.App;

using Microsoft.Win32;
using System.ComponentModel;

public partial class MainWindow
{
    private readonly MainViewModel _viewModel;
    private readonly MacroSession _session;

    public MainWindow(MainViewModel viewModel, MacroSession session)
    {
        _session = session;
        _viewModel = viewModel;
        InitializeComponent();

        _viewModel.ShowSaveFileDialog = ShowSaveFileDialog;
        _viewModel.ShowOpenFileDialog = ShowOpenFileDialog;
        _viewModel.ShowSettingsWindow = ShowSettingsWindow;
        _viewModel.RenameRequested += OnRenameRequested;
    }

    private Task<string?> ShowSaveFileDialog(string initial) => Dispatcher.Invoke(() =>
    {
        var dlg = new SaveFileDialog
        {
            FileName = initial,
            Filter = "Макрос Macron (*.macron.json)|*.macron.json|Все файлы (*.*)|*.*",
            DefaultExt = ".macron.json"
        };
        return dlg.ShowDialog(this) == true ? dlg.FileName : null;
    });

    private Task<string?> ShowOpenFileDialog() => Dispatcher.Invoke(() =>
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Макрос Macron (*.macron.json)|*.macron.json|Все файлы (*.*)|*.*",
            DefaultExt = ".macron.json"
        };
        return dlg.ShowDialog(this) == true ? dlg.FileName : null;
    });

    private void ShowSettingsWindow()
    {
        var dlg = new SettingsWindow { Owner = this };
        dlg.ShowDialog();
    }

    private void OnRenameRequested(object? sender, MacroSummary summary)
    {
        var dlg = new RenameWindow { NewName = summary.Name, Owner = this };
        if (dlg.ShowDialog() == true)
        {
            _viewModel.ApplyRename(summary, dlg.NewName);
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // Остановить запись/воспроизведение перед выходом.
        _session.Stop();
    }
}
