using System.ComponentModel;
using System.Globalization;
using System.Resources;
namespace DaikinDesktopController.App.Localization;
public sealed class Localizer : INotifyPropertyChanged
{
    public static Localizer Instance { get; } = new();
    private readonly ResourceManager resources = new("DaikinDesktopController.App.Localization.Strings", typeof(Localizer).Assembly);
    public event PropertyChangedEventHandler? PropertyChanged;
    public string this[string key] => resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    public static IReadOnlyList<CultureInfo> SupportedLanguages { get; } = Array.AsReadOnly(new[]
    { CultureInfo.GetCultureInfo("pt-BR"), CultureInfo.GetCultureInfo("en") });
    public static CultureInfo DefaultLanguage { get; } = CultureInfo.GetCultureInfo("pt-BR");
    private static CultureInfo? SupportedCulture(CultureInfo culture) =>
        SupportedLanguages.FirstOrDefault(c => c.Name.Equals(culture.Name, StringComparison.OrdinalIgnoreCase)) ??
        SupportedLanguages.FirstOrDefault(c => c.TwoLetterISOLanguageName == culture.TwoLetterISOLanguageName);
    public static CultureInfo SelectCulture(CultureInfo displayLanguage) => SupportedCulture(displayLanguage) ?? DefaultLanguage;
    public static CultureInfo ResolveCulture(string? saved, CultureInfo windows)
    {
        if (!string.IsNullOrWhiteSpace(saved))
        {
            try
            {
                if (SupportedCulture(CultureInfo.GetCultureInfo(saved)) is { } culture) return culture;
            }
            catch (CultureNotFoundException) { }
        }
        return SelectCulture(windows);
    }
    public static string LanguageName(CultureInfo culture) => Instance.resources.GetString("LanguageName", culture) ?? culture.NativeName;
    public void SetCulture(CultureInfo culture)
    {
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
}
