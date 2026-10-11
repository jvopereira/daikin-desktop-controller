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
    public static CultureInfo SelectCulture(CultureInfo displayLanguage) =>
        CultureInfo.GetCultureInfo(displayLanguage.TwoLetterISOLanguageName == "en" ? "en" : "pt-BR");
    public void SetCulture(CultureInfo culture)
    {
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
}
