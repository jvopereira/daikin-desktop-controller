# Daikin Desktop Controller

This context defines the product language for controlling Daikin EcoSwing Smart Gold air conditioners from Windows.

## Product concepts

**Local Control**:
Monitoring and sending commands to an air conditioner directly over the same home network as the Windows computer.
_Avoid_: LAN mode, local API (when referring to the user capability)

**Remote Control**:
Monitoring and sending commands while the user is away from the air conditioner's home network through the official Daikin Smart AC app.
_Avoid_: Cloud mode (when referring to the user's chosen workflow)

**Air Conditioner**:
A compatible Daikin EcoSwing Smart or Smart Gold R-32 indoor unit paired with the Daikin Smart AC service.
_Avoid_: Device (when clarity about the appliance matters), AC (in user-facing copy)

## Windows interaction

**Tray Icon**:
The persistent Windows notification-area entry point for opening the app's compact control panel or exiting the app.
_Avoid_: Taskbar icon

**Compact Control Panel**:
The small, focused window that displays the air conditioner's controls and appears when the user left-clicks the Tray Icon.
_Avoid_: Main window, popup (unless referring to its transient window behavior)

**Tray Menu**:
The context menu opened by right-clicking the Tray Icon; it contains only the command to exit the application.
_Avoid_: Context menu (when referring specifically to this product menu)

**Running in the Tray**:
The app remains active and available from its Tray Icon while the Compact Control Panel is not visible.
_Avoid_: Minimized (when the panel has been dismissed)

**First-Run Setup**:
The one-time setup guidance shown in the Compact Control Panel when the app launches without a configured Air Conditioner.
_Avoid_: Onboarding wizard (unless the setup flow later becomes multi-step)

**Settings Window**:
A separate, standard Windows window opened from the Compact Control Panel to configure the app, with a title bar and normal window controls.
_Avoid_: Preferences dialog

**App Language**:
The language selected for all user-facing text in the Windows application, initially Brazilian Portuguese or English.
_Avoid_: Documentation language, device language

**Theme Preference**:
The user's choice of System, Light, or Dark appearance, with System following the Windows theme.
_Avoid_: Color mode (when referring to the saved app preference)

**Launch at Sign-in**:
The saved preference that controls whether the app starts automatically when the user signs in to Windows; enabled by default.
_Avoid_: Startup (when the distinction from launching the app manually matters)

**Secrets Section**:
The first section in the Settings Window, where the user enters the Air Conditioner's device secret key.
_Avoid_: Credentials page (the key is device-specific, not a Daikin account login)

**Device Section**:
A section in the Settings Window where the user can enter the Air Conditioner's local IP address if Device Discovery does not find it.
_Avoid_: Network section (unless it later includes more than device connection details)

**Device Secret Key**:
A device-specific secret generated during setup in the official Daikin Smart AC app and required by the desktop app to communicate with the Air Conditioner locally.
_Avoid_: Password, Daikin password

**Device Discovery**:
The process of finding a compatible Air Conditioner on the same local network as the Windows computer.
_Avoid_: Pairing (which also requires validating the Device Secret Key)

**Pairing**:
Validating the Device Secret Key against a discovered Air Conditioner so the app can configure it for control.
_Avoid_: Device Discovery

**Status Refresh Interval**:
The time between automatic device status reads while the Compact Control Panel is visible; it can be set to 5, 10, 30, or 60 seconds, with 10 seconds as the default.
_Avoid_: Polling interval (when writing user-facing copy)

**Air Conditioner Unavailable**:
The app cannot read the Air Conditioner's current state after its status retry, but retains the saved configuration.
_Avoid_: Unpaired (unless pairing data itself is invalid)

**Device IP Address**:
The Air Conditioner's address on the local home network, used to communicate with it directly.
_Avoid_: Public IP address

## Air conditioner controls

**Operating Mode**:
The primary climate operation selected for the Air Conditioner, such as cooling, heating, dry, fan, or auto.
_Avoid_: Comfort Mode, Economy Mode (these are separate features)

**Target Temperature**:
The temperature the user asks the Air Conditioner to maintain.
_Avoid_: Room Temperature, current temperature

**Room Temperature**:
The current temperature measured and reported by the Air Conditioner, when available.
_Avoid_: Target Temperature, set temperature

**Temperature Unit**:
The unit used to display Room Temperature and Target Temperature; the app uses degrees Celsius.
_Avoid_: Locale-dependent temperature unit

**Turbo Mode**:
A temporary high-output feature that prioritizes reaching the target temperature quickly.
_Avoid_: Operating Mode, Powerful Mode (unless matching the device's reported label)

**Comfort Mode**:
An airflow feature that directs discharge air to reduce direct airflow onto occupants.
_Avoid_: Operating Mode, Comfort setting

**Economy Mode**:
A feature that limits power consumption during operation.
_Avoid_: Operating Mode, Energy-saving schedule

### Target Temperature Range
The supported minimum, maximum, and step derived from the reported Operating Mode using the vendor table. Dry, Fan, and unknown modes have no supported range.

### Pending Command
A requested value shown immediately until confirmed or reconciled against refreshed device status. Each intent is sent once; older results cannot overwrite newer intent.
