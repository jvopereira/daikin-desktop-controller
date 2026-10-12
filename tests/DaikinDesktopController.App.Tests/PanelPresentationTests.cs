using System.Globalization;
using DaikinDesktopController.App.Monitoring;
using DaikinDesktopController.App.Panel;
using DaikinDesktopController.Device;
using Xunit;
namespace DaikinDesktopController.App.Tests;
public class PanelPresentationTests
{
    [Theory]
    [InlineData(OperatingMode.Cool, "Icon.Snowflake")]
    [InlineData(OperatingMode.Heat, "Icon.Sun")]
    [InlineData(OperatingMode.Dry, "Icon.Droplet")]
    [InlineData(OperatingMode.Fan, "Icon.Fan")]
    [InlineData(OperatingMode.Auto, "Icon.AutoMode")]
    [InlineData(null, "Icon.AutoMode")]
    public void ModeIconMatchesOperatingMode(OperatingMode? mode, string expected) =>
        Assert.Equal(expected, PanelPresentation.ModeIconKey(mode));
    [Theory]
    [InlineData(true, true, true, "CommandNotAppliedMessage", FooterSeverity.Error)]
    [InlineData(false, true, true, "OperatingModeUnknownMessage", FooterSeverity.Error)]
    [InlineData(false, false, true, "Updating", FooterSeverity.Pending)]
    [InlineData(false, false, false, "Connected", FooterSeverity.Normal)]
    public void FooterPrefersErrorsOverPendingOverConnected(bool notApplied, bool modeUnknown, bool pending, string key, FooterSeverity severity) =>
        Assert.Equal(new FooterStatus(key, severity), PanelPresentation.Footer(notApplied, modeUnknown, pending));
    [Fact]
    public void TemperatureValueUsesCurrentUiCultureWithoutUnit()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("pt-BR");
            Assert.Equal("23,5", TemperatureText.FormatValue(23.5));
            Assert.Equal("23", TemperatureText.FormatValue(23));
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }
}
