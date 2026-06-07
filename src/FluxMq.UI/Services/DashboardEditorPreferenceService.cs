using System.Text.Json;

namespace FluxMq.UI.Services;

public sealed class DashboardEditorPreferenceService
{
    private const string PreferencesFileName = "editor-preferences.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _path;
    private DashboardEditorPreferences _preferences;

    public DashboardEditorPreferenceService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FluxMQ",
            PreferencesFileName))
    {
    }

    public DashboardEditorPreferenceService(string path)
    {
        _path = path;
        _preferences = Load(path);
    }

    public bool ShowQueryBuilderHelp => _preferences.ShowQueryBuilderHelp;

    public event EventHandler? Changed;

    public void SetShowQueryBuilderHelp(bool value)
    {
        if (_preferences.ShowQueryBuilderHelp == value)
        {
            return;
        }

        _preferences = _preferences with { ShowQueryBuilderHelp = value };
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static DashboardEditorPreferences Load(string path)
    {
        if (!File.Exists(path))
        {
            return new DashboardEditorPreferences();
        }

        try
        {
            return JsonSerializer.Deserialize<DashboardEditorPreferences>(File.ReadAllText(path), JsonOptions)
                   ?? new DashboardEditorPreferences();
        }
        catch (JsonException)
        {
            return new DashboardEditorPreferences();
        }
        catch (IOException)
        {
            return new DashboardEditorPreferences();
        }
    }

    private void Save()
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_path, JsonSerializer.Serialize(_preferences, JsonOptions));
    }

    private sealed record DashboardEditorPreferences
    {
        public bool ShowQueryBuilderHelp { get; init; } = true;
    }
}
