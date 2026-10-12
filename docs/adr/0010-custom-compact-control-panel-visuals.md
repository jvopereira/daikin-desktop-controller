# 10. Custom Compact Control Panel visuals

## Status
Accepted

## Context
The Compact Control Panel used stock Fluent controls stacked vertically, with a pending and an error line under each control. We want a denser, card-like panel with an air conditioner illustration, icon tiles and motion, without losing the theme behavior from ADR 0005.

## Decision
- The panel uses custom ControlTemplates (power button, switch, slider, tiles) and a vector illustration. They reference only Fluent dynamic brush keys, so `Application.ThemeMode` still switches Light, Dark and System.
- The window uses `AllowsTransparency` for rounded corners and a drop shadow.
- Pending state is shown as a pulsing indicator on the affected control. Errors and unsupported explanations go to the footer, the control's tooltip and `AutomationProperties.HelpText`. This refines the display wording from ADR 0009.
- Comfort Mode is shown as the "Airflow" tile (Comfort/Normal). Economy Mode is shown as the "Eco mode" card.
- Animations are skipped when Windows client area animations are off.

## Consequences
Template parts (`PART_Track`, `PART_Popup`) must be kept so keyboard and UI Automation behavior stays intact. Visual changes need manual checks in both themes.
