using System.Globalization;
using System.IO;
using DaikinDesktopController.App.Localization;
using DaikinDesktopController.App.Preferences;
using Xunit;
namespace DaikinDesktopController.App.Tests;
public sealed class PreferencesTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "DaikinPreferencesTests", Guid.NewGuid().ToString("N"));
    private string PreferencesPath => Path.Combine(directory, "nested", "preferences.json");
    [Fact]
    public void MissingFileReturnsDefaults()
    {
        var actual = new JsonPreferencesStore(PreferencesPath).Load();
        Assert.Null(actual.Language);
        Assert.Equal(ThemePreference.System, actual.Theme);
        Assert.True(actual.LaunchAtSignIn);
    }
    [Fact]
    public void SaveCreatesDirectoryAndRoundTripsReplacingExistingFile()
    {
        var store = new JsonPreferencesStore(PreferencesPath);
        store.Save(new AppPreferences());
        var expected = new AppPreferences("en", ThemePreference.Dark, false);
        store.Save(expected);
        Assert.Equal(expected, store.Load());
        Assert.Contains("\"Dark\"", File.ReadAllText(PreferencesPath));
        Assert.False(File.Exists(PreferencesPath + ".tmp"));
    }
    [Theory]
    [InlineData("{bad")]
    [InlineData("null")]
    [InlineData("{\"Theme\":\"Unknown\"}")]
    [InlineData("{\"Theme\":42}")]
    public void MalformedPreferencesReturnDefaults(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PreferencesPath)!);
        File.WriteAllText(PreferencesPath, json);
        Assert.Equal(new AppPreferences(), new JsonPreferencesStore(PreferencesPath).Load());
    }
    [Fact]
    public void UnreadableFileReturnsDefaults()
    {
        Directory.CreateDirectory(PreferencesPath);
        Assert.Equal(new AppPreferences(), new JsonPreferencesStore(PreferencesPath).Load());
    }
    [Fact]
    public void SessionLoadsSavesAndNotifiesOnlyForChanges()
    {
        var initial = new AppPreferences("en");
        var store = new Store { Value = initial };
        var session = new PreferencesSession(store);
        Assert.Equal(initial, session.Current);
        var notifications = 0;
        session.Changed += (old, now) =>
        {
            Assert.Equal(initial, old);
            Assert.Equal(initial with { Theme = ThemePreference.Light }, now);
            Assert.Equal(now, session.Current);
            Assert.Equal(now, store.Value);
            notifications++;
        };
        session.Update(p => p with { Theme = ThemePreference.Light });
        session.Update(p => p with { Theme = ThemePreference.Light });
        Assert.Equal(1, store.Saves);
        Assert.Equal(1, notifications);
    }
    [Fact]
    public void FailedSaveDoesNotChangeCurrentOrNotify()
    {
        var store = new Store { FailSave = true };
        var session = new PreferencesSession(store);
        var notifications = 0;
        session.Changed += (_, _) => notifications++;
        Assert.Throws<IOException>(() => session.Update(p => p with { Language = "en" }));
        Assert.Equal(new AppPreferences(), session.Current);
        Assert.Equal(0, notifications);
    }
    [Theory]
    [InlineData("en", "pt-BR", "en")]
    [InlineData("pt-PT", "en", "pt-BR")]
    [InlineData("fr-FR", "en-GB", "en")]
    [InlineData(null, "pt-PT", "pt-BR")]
    [InlineData(null, "fr-FR", "pt-BR")]
    [InlineData("", "en", "en")]
    [InlineData("invalid_!", "en", "en")]
    public void SavedSupportedLanguageWinsOtherwiseWindowsRulesApply(string? saved, string windows, string expected) =>
        Assert.Equal(expected, Localizer.ResolveCulture(saved, CultureInfo.GetCultureInfo(windows)).Name);
    [Fact]
    public void LanguagesHaveStableOrderAndDistinctNativeNames()
    {
        Assert.Equal(new[] { "pt-BR", "en" }, Localizer.SupportedLanguages.Select(c => c.Name));
        Assert.Equal("pt-BR", Localizer.DefaultLanguage.Name);
        var names = Localizer.SupportedLanguages.Select(Localizer.LanguageName).ToArray();
        Assert.All(names, name => Assert.False(string.IsNullOrWhiteSpace(name)));
        Assert.Equal(new[] { "Português (Brasil)", "English" }, names);
    }
    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
    private sealed class Store : IPreferencesStore
    {
        public AppPreferences Value { get; set; } = new();
        public int Saves { get; private set; }
        public bool FailSave { get; init; }
        public AppPreferences Load() => Value;
        public void Save(AppPreferences preferences)
        {
            if (FailSave) throw new IOException("Test save failure");
            Value = preferences;
            Saves++;
        }
    }
}
