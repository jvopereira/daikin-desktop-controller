using System.IO;
using DaikinDesktopController.App.Preferences;
using Xunit;

namespace DaikinDesktopController.App.Tests;

public sealed class LocationNameTests
{
    [Theory]
    [InlineData(null, null)] [InlineData("   ", null)] [InlineData("  Escritório  ", "Escritório")]
    public void NamesAreNormalized(string? value, string? expected) =>
        Assert.Equal(expected, AppPreferences.NormalizeLocationName(value));

    [Fact] public void SavedNameSurvivesRestartAndOldFilesKeepTheirPreferences()
    {
        var directory = Directory.CreateTempSubdirectory("daikin-location-tests-");
        try
        {
            var path = Path.Combine(directory.FullName, "preferences.json");
            File.WriteAllText(path, "{\"Language\":\"en\",\"LaunchAtSignIn\":false}");
            var store = new JsonPreferencesStore(path);
            Assert.Equal(new AppPreferences("en", LaunchAtSignIn: false), store.Load());
            var session = new PreferencesSession(store);
            session.Update(p => p with { LocationName = "  Escritório  " });
            Assert.Equal("Escritório", new PreferencesSession(store).Current.LocationName);
            Assert.Equal("en", store.Load().Language);
            session.Update(p => p with { LocationName = new string('x', 60) });
            Assert.Equal(new string('x', 40), store.Load().LocationName);
            session.Update(p => p with { LocationName = " " });
            Assert.Null(store.Load().LocationName);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact] public void FailedSaveKeepsPreviousNameAndDoesNotNotify()
    {
        var session = new PreferencesSession(new FailingStore());
        var notified = false;
        session.Changed += (_, _) => notified = true;
        Assert.Throws<IOException>(() => session.Update(p => p with { LocationName = "Novo" }));
        Assert.Equal("Original", session.Current.LocationName); Assert.False(notified);
    }

    private sealed class FailingStore : IPreferencesStore
    {
        public AppPreferences Load() => new(LocationName: "Original");
        public void Save(AppPreferences preferences) => throw new IOException("Test storage failure");
    }
}
