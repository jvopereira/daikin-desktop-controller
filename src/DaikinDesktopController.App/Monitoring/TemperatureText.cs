using System.Globalization;
namespace DaikinDesktopController.App.Monitoring;
public static class TemperatureText
{
    public static string Format(double value) => FormatValue(value) + " °C";
    public static string FormatValue(double value) => value.ToString("0.#", CultureInfo.CurrentUICulture);
}
