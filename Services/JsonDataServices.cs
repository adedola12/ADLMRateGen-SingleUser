// Services/JsonDataServices.cs
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using ADLMRateGen.Helpers;   // <= add

public sealed class JsonDataServices<T> where T : class
{
    private readonly string _file;          // absolute, in AppData
    private readonly string? _defaultAbs;   // absolute, next to EXE

    public JsonDataServices(string fileNameOrPath, string? defaultRelativeToExe = null)
    {
        // Working file: under %APPDATA%\ADLMRateGen
        _file = Path.IsPathRooted(fileNameOrPath)
              ? fileNameOrPath
              : Path.Combine(AppPaths.UserDataDir, Path.GetFileName(fileNameOrPath));

        // Where the installer placed your seed JSON (read-only)
        if (!string.IsNullOrWhiteSpace(defaultRelativeToExe))
        {
            var exeDir = AppContext.BaseDirectory;
            var candidate = Path.Combine(exeDir, defaultRelativeToExe);
            _defaultAbs = File.Exists(candidate) ? candidate : null;
        }

        // First-run bootstrap: copy seed → AppData
        try
        {
            if (!File.Exists(_file) && !string.IsNullOrEmpty(_defaultAbs))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
                File.Copy(_defaultAbs!, _file, overwrite: false);
            }
        }
        catch { /* swallow – we’ll create an empty file on first save */ }
    }

    public ObservableCollection<T> LoadData()
    {
        try
        {
            if (!File.Exists(_file)) return new ObservableCollection<T>();
            var json = File.ReadAllText(_file);
            return JsonSerializer.Deserialize<ObservableCollection<T>>(json)
                   ?? new ObservableCollection<T>();
        }
        catch
        {
            // If the file is corrupt or locked, don’t crash the app
            return new ObservableCollection<T>();
        }
    }

    public void SaveData(IEnumerable<T> list)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_file, json);   // AppData is writable for the user
    }
}
