using System.IO;
using System.Globalization;
using System.Windows;
using System.Xml.Linq;
using DaikinDesktopController.App.Configuration;
using DaikinDesktopController.App.Localization;
using DaikinDesktopController.App.Panel;
using DaikinDesktopController.App.Settings;
using DaikinDesktopController.App.Startup;
using DaikinDesktopController.App.Tray;
using Xunit;
namespace DaikinDesktopController.App.Tests;
public class ShellTests
{
    [Theory]
    [InlineData(0, 0, 1920, 1040, 1552, 552)] // bottom
    [InlineData(0, 40, 1920, 1040, 1552, 48)] // top
    [InlineData(40, 0, 1880, 1080, 48, 592)] // left
    [InlineData(0, 0, 1880, 1080, 1512, 592)] // right
    [InlineData(0, 0, 1920, 1080, 1552, 592)] // auto-hide
    public void PlacementAnchorsNextToTray(double x, double y, double width, double height, double expectedX, double expectedY)
    {
        Assert.Equal(new Point(expectedX, expectedY), PanelPlacement.Compute(new Rect(x,y,width,height),
            new Rect(0,0,1920,1080), new Size(360,480)));
    }
    [Fact]
    public void PlacementHandlesNegativeMonitorCoordinatesAndScaling()
    {
        Assert.Equal(new Point(-548, 312), PanelPlacement.Compute(new Rect(-1920,0,1920,1040),
            new Rect(-1920,0,1920,1080), new Size(540,720)));
    }
    [Fact]
    public void PlacementClampsToSmallWorkArea()
    {
        Assert.Equal(new Point(10,20), PanelPlacement.Compute(new Rect(10,20,200,300),
            new Rect(10,20,200,300), new Size(360,480)));
    }
    [Fact]
    public void ClickGateSuppressesOnlyRecentDismissals()
    {
        var clock = new Clock();
        var gate = new TrayClickGate(clock);
        Assert.False(gate.ShouldSuppressClick());
        gate.RecordDismissal();
        clock.Timestamp = 299;
        Assert.True(gate.ShouldSuppressClick());
        clock.Timestamp = 300;
        Assert.False(gate.ShouldSuppressClick());
    }
    [Fact]
    public void LaunchRegistrationQuotesPathAndDoesNotRewriteIdenticalValue()
    {
        var key = new RunKey();
        var launch = new LaunchAtSignIn(key);
        launch.Apply(true, @"C:\Program Files\Daikin\Daikin.exe");
        launch.Apply(true, @"C:\Program Files\Daikin\Daikin.exe");
        Assert.Equal(LaunchAtSignIn.ValueName, key.Name);
        Assert.Equal("\"C:\\Program Files\\Daikin\\Daikin.exe\"", key.Value);
        Assert.Equal(1,key.Writes);
        launch.Apply(true, @"D:\Daikin.exe");
        Assert.Equal(2,key.Writes);
    }
    [Fact]
    public void DisablingLaunchDeletesOnlyAnExistingValue()
    {
        var key = new RunKey();
        var launch = new LaunchAtSignIn(key);
        launch.Apply(false, "");
        Assert.Equal(0, key.Deletes);
        launch.Apply(true, @"C:\Daikin.exe");
        launch.Apply(false, "");
        launch.Apply(false, "");
        Assert.Equal(1, key.Deletes);
        Assert.Null(key.Value);
        Assert.Equal(LaunchAtSignIn.ValueName, key.Name);
    }
    [Theory]
    [InlineData("")]
    [InlineData("bad\"path")]
    public void LaunchRegistrationRejectsInvalidPaths(string path) =>
        Assert.Throws<ArgumentException>(() => new LaunchAtSignIn(new RunKey()).Apply(true, path));
    [Theory]
    [InlineData("pt-PT", "pt-BR")]
    [InlineData("pt-BR", "pt-BR")]
    [InlineData("en-GB", "en")]
    [InlineData("fr-FR", "pt-BR")]
    public void CultureFollowsSupportedDisplayLanguage(string input, string expected) =>
        Assert.Equal(expected, Localizer.SelectCulture(CultureInfo.GetCultureInfo(input)).Name);
    [Fact]
    public void SettingsSectionsAreInProductOrder() =>
        Assert.Equal(new[] { SettingsSection.Secrets, SettingsSection.Device, SettingsSection.General }, Enum.GetValues<SettingsSection>());
    [Fact]
    public void BothLanguagesHaveExactlyTheSameNonEmptyKeys()
    {
        Dictionary<string,string> Read(string name) => XDocument.Load(Path.Combine(AppContext.BaseDirectory,"Resources",name))
            .Root!.Elements("data").ToDictionary(e => e.Attribute("name")!.Value, e => e.Element("value")!.Value);
        var en = Read("Strings.resx");
        var pt = Read("Strings.pt-BR.resx");
        Assert.Equal(en.Keys.Order(),pt.Keys.Order());
        Assert.All(en.Values.Concat(pt.Values), value => Assert.False(string.IsNullOrWhiteSpace(value)));
    }
    [Theory]
    [InlineData(false,true)]
    [InlineData(true,false)]
    public void StartupShowsPanelOnlyWithoutConfiguration(bool configured, bool expected) =>
        Assert.Equal(expected,StartupDecision.ShouldOpenPanel(new ConfigurationStore(configured)));
    private sealed class ConfigurationStore(bool configured) : IAirConditionerConfigurationStore
    { public bool HasConfiguredAirConditioner => configured;
      public AirConditionerConfiguration? Load() => null;
      public void Save(AirConditionerConfiguration value) { }
      public void Clear() { } }
    private sealed class Clock : TimeProvider
    {
        public long Timestamp { get; set; }
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => Timestamp;
    }
    private sealed class RunKey : IRunKey
    {
        public string? Name { get; private set; }
        public string? Value { get; private set; }
        public int Writes { get; private set; }
        public int Deletes { get; private set; }
        public void Delete(string name) { Name = name; Value = null; Deletes++; }
        public string? Read(string name) => Value;
        public void Write(string name,string value) { Name=name; Value=value; Writes++; }
    }
}
