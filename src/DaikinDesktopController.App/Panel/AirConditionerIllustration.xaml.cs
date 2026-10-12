using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Path = System.Windows.Shapes.Path;
using DaikinDesktopController.Device;
namespace DaikinDesktopController.App.Panel;
public partial class AirConditionerIllustration : UserControl
{
    public static readonly DependencyProperty IsOnProperty = DependencyProperty.Register(nameof(IsOn), typeof(bool),
        typeof(AirConditionerIllustration), new PropertyMetadata(false, (d, _) => ((AirConditionerIllustration)d).Update()));
    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(nameof(Mode), typeof(OperatingMode?),
        typeof(AirConditionerIllustration), new PropertyMetadata(null, (d, _) => ((AirConditionerIllustration)d).Update()));
    private bool? shownOn;
    private OperatingMode? shownMode;
    public bool IsOn { get => (bool)GetValue(IsOnProperty); set => SetValue(IsOnProperty, value); }
    public OperatingMode? Mode { get => (OperatingMode?)GetValue(ModeProperty); set => SetValue(ModeProperty, value); }
    private static bool ReduceMotion => !SystemParameters.ClientAreaAnimation;
    private Path[] Wisps => [Wisp1, Wisp2, Wisp3, Wisp4, Wisp5];
    public AirConditionerIllustration()
    {
        InitializeComponent();
        Update();
    }
    public static Color ModeColor(OperatingMode? mode) => mode switch
    {
        OperatingMode.Cool => Color.FromRgb(0x3F, 0xA9, 0xF5),
        OperatingMode.Heat => Color.FromRgb(0xF5, 0x8A, 0x3F),
        OperatingMode.Dry => Color.FromRgb(0x2E, 0xC4, 0xB6),
        OperatingMode.Fan => Color.FromRgb(0xA8, 0xB0, 0xBA),
        _ => Color.FromRgb(0x9B, 0x8C, 0xF5)
    };
    private void Update()
    {
        if (shownOn == IsOn && shownMode == Mode) return;
        var first = shownOn is null;
        shownOn = IsOn; shownMode = Mode;
        var color = ModeColor(Mode);
        var stroke = new SolidColorBrush(color); stroke.Freeze();
        Led.Fill = stroke;
        LedGlow.Color = color;
        foreach (var wisp in Wisps) wisp.Stroke = stroke;
        var fast = first || ReduceMotion ? TimeSpan.Zero : TimeSpan.FromMilliseconds(420);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Animate(Led, OpacityProperty, IsOn ? 1 : 0, fast, ease);
        Animate(FlapScale, ScaleTransform.ScaleYProperty, IsOn ? 0.6 : 1, fast, ease);
        Animate(FlapOffset, TranslateTransform.YProperty, IsOn ? 9 : 0, fast, ease);
        if (IsOn && !ReduceMotion) StartLoops(); else StopLoops();
    }
    private static void Animate(IAnimatable target, DependencyProperty property, double to, TimeSpan duration, IEasingFunction ease) =>
        target.BeginAnimation(property, new DoubleAnimation(to, duration) { EasingFunction = ease });
    private void StartLoops()
    {
        var period = TimeSpan.FromSeconds(2.4);
        for (var i = 0; i < Wisps.Length; i++)
        {
            var begin = TimeSpan.FromSeconds(i * 0.45);
            var fade = new DoubleAnimationUsingKeyFrames { Duration = period, BeginTime = begin, RepeatBehavior = RepeatBehavior.Forever };
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.75, KeyTime.FromPercent(0.3)));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
            Wisps[i].BeginAnimation(OpacityProperty, fade);
            Wisps[i].RenderTransform.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(0, 22, period) { BeginTime = begin, RepeatBehavior = RepeatBehavior.Forever });
        }
        Float.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -1.5, TimeSpan.FromSeconds(2))
        { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
    }
    private void StopLoops()
    {
        foreach (var wisp in Wisps)
        {
            wisp.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(250)));
            wisp.RenderTransform.BeginAnimation(TranslateTransform.YProperty, null);
        }
        Float.BeginAnimation(TranslateTransform.YProperty, null);
    }
}
