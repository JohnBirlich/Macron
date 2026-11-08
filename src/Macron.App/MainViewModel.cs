namespace Macron.App;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Macron.Core;
using System.Threading;

/// <summary>
/// Модель представления главного окна. Не знает ничего о WinAPI и окнах диалогов:
/// диалоги подключаются через колбэки из кода основного окна.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly MacroSession _session;
    private readonly Settings _settings;
    private readonly Timer _statusTimer;
    private DateTime _recordStartUtc;
    private MacroSummary? _selectedMacro;

    public MainViewModel(MacroSession session)
    {
        _session = session;
        _settings = SettingsService.Load();

        RecordCommand = new RelayCommand(OnRecord, CanRecord);
        StopCommand = new RelayCommand(OnStop, CanStop);
        PlayCommand = new RelayCommand(OnPlay, CanPlay);
        SaveCommand = new RelayCommand(OnSave, CanSave);
        OpenCommand = new RelayCommand(OnOpen, CanOpen);
        DeleteCommand = new RelayCommand(OnDelete, CanDelete);
        RenameCommand = new RelayCommand(OnRename, CanRename);
        SettingsCommand = new RelayCommand(OnSettings);

        _session.StateChanged += OnSessionStateChanged;
        _session.MacroRecorded += (_, _) => OnMacrosChanged();
        _session.MacroRemoved += (_, _) => OnMacrosChanged();
        _session.PlaybackCompleted += OnPlaybackCompleted;

        _statusTimer = new Timer(_ => UpdateStatusText(), null, Timeout.Infinite, Timeout.Infinite);
    }

    // ---------- колбэки диалогов, подключаются из MainWindow.xaml.cs ----------

    public Func<string, string?>? ShowSaveFileDialog { get; set; }
    public Func<string?>? ShowOpenFileDialog { get; set; }
    public Action? ShowSettingsWindow { get; set; }
    public event EventHandler<MacroSummary>? RenameRequested;

    // ---------- состояние ----------

    public MacroState State => _session.State;

    public bool IsRecording => State == MacroState.Recording;

    public bool IsPlaying => State == MacroState.Playing;

    public ObservableCollection<MacroSummary> Macros { get; } =
        new(_session.Macros.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase));

    public MacroSummary? SelectedMacro
    {
        get => _selectedMacro;
        set
        {
            if (SetProperty(ref _selectedMacro, value))
            {
                RequeryCommands();
                OnPropertyChanged(nameof(HasSelectedMacro));
            }
        }
    }

    public bool HasSelectedMacro => SelectedMacro is not null;

    public double Speed
    {
        get => _settings.SpeedMultiplier;
        set
        {
            if (SetProperty(ref _settings.SpeedMultiplier, Math.Clamp(value, 0.1, 10.0)))
            {
                SettingsService.Save(_settings);
            }
        }
    }

    public RepeatMode RepeatMode
    {
        get => _settings.RepeatMode;
        set
        {
            if (SetProperty(ref _settings.RepeatMode, value))
            {
                SettingsService.Save(_settings);
            }
        }
    }

    public int RepeatCount
    {
        get => _settings.RepeatCount;
        set
        {
            if (SetProperty(ref _settings.RepeatCount, Math.Clamp(value, 1, 10_000_000)))
            {
                SettingsService.Save(_settings);
            }
        }
    }

    public int DelaySeconds
    {
        get => _settings.DelaySeconds;
        set
        {
            if (SetProperty(ref _settings.DelaySeconds, Math.Clamp(value, 0, 3600)))
            {
                SettingsService.Save(_settings);
            }
        }
    }

    public bool RecordMouseMoves
    {
        get => _settings.RecordMouseMoves;
        set
        {
            if (SetProperty(ref _settings.RecordMouseMoves, value))
            {
                SettingsService.Save(_settings);
            }
        }
    }

    public string StatusText { get; private set; } = "Готов";

    public int CurrentEventCount => State == MacroState.Recording
        ? _session.RecordedEventCount
        : _session.CurrentMacro?.EventCount ?? SelectedMacro?.EventCount ?? 0;

    public TimeSpan CurrentDuration => _session.CurrentMacro?.Duration ??
                                       SelectedMacro?.Duration ?? TimeSpan.Zero;

    // ---------- команды ----------

    public RelayCommand RecordCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand PlayCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand OpenCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand RenameCommand { get; }
    public RelayCommand SettingsCommand { get; }

    private bool CanRecord() => State == MacroState.Idle;

    private bool CanStop() => State is MacroState.Recording or MacroState.Playing;

    private bool CanPlay() => State == MacroState.Idle && SelectedMacro is not null;

    private bool CanSave() => _session.CurrentMacro is not null;

    private bool CanOpen() => State == MacroState.Idle;

    private bool CanDelete() => SelectedMacro is not null;

    private bool CanRename() => SelectedMacro is not null;

    private void RequeryCommands()
    {
        RecordCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        PlayCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        OpenCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        RenameCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CurrentEventCount));
        OnPropertyChanged(nameof(CurrentDuration));
    }

    // ---------- действия ----------

    private void OnRecord()
    {
        _session.TryRecord(RecordMouseMoves);
        _recordStartUtc = DateTime.UtcNow;
        _statusTimer.Change(0, 500);
        UpdateStatusText();
        RequeryCommands();
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(IsRecording));
        OnPropertyChanged(nameof(IsPlaying));
    }

    private void OnStop()
    {
        _session.Stop();
        _statusTimer.Change(Timeout.Infinite, Timeout.Infinite);
        UpdateStatusText();
        RequeryCommands();
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(IsRecording));
        OnPropertyChanged(nameof(IsPlaying));
    }

    private async void OnPlay()
    {
        if (SelectedMacro is null)
        {
            return;
        }

        Macro macro;
        try
        {
            macro = await _session.LoadMacro(SelectedMacro);
        }
        catch (Exception ex)
        {
            StatusText = "Ошибка загрузки: " + ex.Message;
            RequeryCommands();
            return;
        }

        var options = new PlayOptions
        {
            SpeedMultiplier = Speed,
            RepeatMode = RepeatMode,
            RepeatCount = RepeatCount,
            DelayBeforeStart = TimeSpan.FromSeconds(DelaySeconds)
        };

        StatusText = "Воспроизведение…";
        RequeryCommands();
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(IsRecording));
        OnPropertyChanged(nameof(IsPlaying));

        await _session.Play(macro, options);
        // Статус обновится через PlaybackCompleted.
    }

    private void OnSave()
    {
        var macro = _session.CurrentMacro;
        if (macro is null || ShowSaveFileDialog is null)
        {
            return;
        }

        var initial = string.IsNullOrWhiteSpace(macro.Name) ? "Макрос" : macro.Name;
        var path = ShowSaveFileDialog(initial);
        if (path is null)
        {
            return;
        }

        try
        {
            MacroFileStorage.Save(path, macro);
            StatusText = $"Сохранено: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusText = "Ошибка сохранения: " + ex.Message;
        }

        _statusTimer.Change(0, 500);
        UpdateStatusText();
    }

    private void OnOpen()
    {
        if (ShowOpenFileDialog is null)
        {
            return;
        }

        var path = ShowOpenFileDialog();
        if (path is null)
        {
            return;
        }

        try
        {
            var macro = _session.LoadMacro(new MacroSummary { Path = path, Name = Path.GetFileNameWithoutExtension(path) }).GetAwaiter().GetResult();
            _session.Repository.Add(macro);
            OnMacrosChanged();
            StatusText = $"Открыт: {macro.Name}";
        }
        catch (Exception ex)
        {
            StatusText = "Ошибка открытия: " + ex.Message;
        }

        _statusTimer.Change(0, 500);
        UpdateStatusText();
    }

    private void OnDelete()
    {
        if (SelectedMacro is null)
        {
            return;
        }

        _session.Repository.Remove(SelectedMacro);
        OnMacrosChanged();
        StatusText = "Макрос удалён";
        _statusTimer.Change(0, 500);
        UpdateStatusText();
    }

    private void OnRename()
    {
        if (SelectedMacro is null)
        {
            return;
        }

        RenameRequested?.Invoke(this, SelectedMacro);
    }

    private void OnSettings()
    {
        ShowSettingsWindow?.Invoke();
    }

    public void ApplyRename(MacroSummary summary, string newName)
    {
        if (summary is null || string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        try
        {
            _session.Repository.Rename(summary, newName);
            OnMacrosChanged();
            StatusText = "Переименовано";
        }
        catch (Exception ex)
        {
            StatusText = "Ошибка переименования: " + ex.Message;
        }

        _statusTimer.Change(0, 500);
        UpdateStatusText();
    }

    // ---------- внутреннее ----------

    private void OnSessionStateChanged(object? sender, MacroState state)
    {
        RequeryCommands();
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(IsRecording));
        OnPropertyChanged(nameof(IsPlaying));
        UpdateStatusText();
    }

    private void OnPlaybackCompleted(object? sender, PlaybackCompletedEventArgs e)
    {
        StatusText = e.Cancelled
            ? $"Воспроизведение остановлено ({e.IterationsCompleted} проходов)"
            : $"Готово ({e.IterationsCompleted} проходов)";
        _statusTimer.Change(0, 500);
        UpdateStatusText();
    }

    private void OnMacrosChanged()
    {
        // Пересобираем коллекцию, чтобы отразить добавление/удаление и сортировку.
        var ordered = _session.Macros.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
        Macros.Clear();
        foreach (var m in ordered)
        {
            Macros.Add(m);
        }

        RequeryCommands();
    }

    private void UpdateStatusText()
    {
        StatusText = State switch
        {
            MacroState.Idle => "Готов",
            MacroState.Recording =>
                $"Запись… {DateTime.UtcNow - _recordStartUtc:mm\\:ss} ({CurrentEventCount} событий)",
            MacroState.Playing => $"Воспроизведение… ({CurrentEventCount} событий)",
            _ => "Готов"
        };
        OnPropertyChanged(nameof(StatusText));
    }
}
