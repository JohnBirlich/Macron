namespace Macron.App;

using Macron.Core;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;

/// <summary>
/// Библиотека макросов пользователя в %APPDATA%\Macron\Macros.
/// Сами файлы .macron.json пользователь может хранить где угодно;
/// здесь приложение держит свою копию для списка макросов.
/// </summary>
public sealed class MacroRepository
{
    private static readonly string BaseFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Macron",
        "Macros");

    private readonly ObservableCollection<MacroSummary> _macros = new();
    private int _nextSeq;

    public MacroRepository()
    {
        Directory.CreateDirectory(BaseFolder);
        _nextSeq = 1;
        foreach (var file in Directory.GetFiles(BaseFolder, "*.macron.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var macro = MacroFileStorage.Load(file);
                _macros.Add(new SummaryFromMacro(macro, file));
                _nextSeq = Math.Max(_nextSeq, ExtractSeq(macro.Name) + 1);
            }
            catch
            {
                // Повреждённый файл не роняет приложение — пропускаем его.
            }
        }
    }

    public IReadOnlyList<MacroSummary> Macros => _macros;

    public void Add(Macro macro)
    {
        if (string.IsNullOrWhiteSpace(macro.Name))
        {
            macro.Name = $"Макрос {_nextSeq++}";
        }

        macro.Name = MakeUniqueName(macro.Name);
        var path = Path.Combine(BaseFolder, SafeFileName(macro.Name) + ".macron.json");

        MacroFileStorage.Save(path, macro);
        _macros.Add(new SummaryFromMacro(macro, path));
    }

    public void Remove(MacroSummary summary)
    {
        try { File.Delete(summary.Path); } catch { /* файл уже удалён — ничего страшного */ }
        _macros.Remove(summary);
        MacroRemoved?.Invoke(this, summary);
    }

    public event EventHandler<MacroSummary>? MacroRemoved;

    public void Rename(MacroSummary summary, string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        newName = MakeUniqueName(newName, summary);
        var newPath = Path.Combine(BaseFolder, SafeFileName(newName) + ".macron.json");

        var macro = LoadByPath(summary.Path);
        macro.Name = newName;
        if (newPath != summary.Path)
        {
            File.Delete(summary.Path);
            MacroFileStorage.Save(newPath, macro);
        }
        else
        {
            MacroFileStorage.Save(summary.Path, macro);
        }

        summary.Name = macro.Name;
        summary.Duration = macro.Duration;
        summary.EventCount = macro.EventCount;
        summary.Path = newPath == summary.Path ? summary.Path : newPath;
    }

    public Task<Macro> LoadBySummary(MacroSummary summary) => Task.Run(() => LoadByPath(summary.Path));

    public static Macro LoadByPath(string path) => MacroFileStorage.Load(path);

    private string MakeUniqueName(string name, MacroSummary? exclude = null)
    {
        var candidate = name;
        var i = 2;
        while (_macros.Any(m => m != exclude && string.Equals(m.Name, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{name} ({i++})";
        }

        return candidate;
    }

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "macro" : safe;
    }

    private static int ExtractSeq(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return 0;
        var match = System.Text.RegularExpressions.Regex.Match(name, @"\d+$");
        return match.Success ? int.Parse(match.Value) : 0;
    }

    private sealed class SummaryFromMacro : MacroSummary
    {
        public SummaryFromMacro(Macro macro, string path)
        {
            Macro = macro;
            Path = path;
            Update(macro, path);
        }

        public Macro Macro { get; private set; }

        public void Update(Macro macro, string path)
        {
            Macro = macro;
            Path = path;
            Name = macro.Name;
            Duration = macro.Duration;
            EventCount = macro.EventCount;
        }
    }
}

public class MacroSummary
{
    public string Name { get; set; } = "";
    public TimeSpan Duration { get; set; }
    public int EventCount { get; set; }
    public string DurationText => Duration.TotalSeconds < 60
        ? $"{Duration.TotalSeconds:F1} с"
        : $"{Duration.Minutes:D2}:{Duration.Seconds:D2}";
    internal string Path { get; set; } = "";
}
