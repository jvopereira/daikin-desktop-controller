# Use WPF Fluent ThemeMode for application appearance

**Status:** Accepted

Use Application.ThemeMode to apply System, Light, and Dark to every open WPF window, including standard title bars. System follows Windows appearance changes automatically. Controls and the Compact Control Panel use Fluent dynamic resources rather than fixed light colours.

ThemeMode is experimental (WPF0001). Accept this API stability risk and suppress that diagnostic in the app project; recheck the API and appearance when upgrading .NET. This avoids maintaining hand-made Light/Dark resource dictionaries and operating-system theme notifications.
