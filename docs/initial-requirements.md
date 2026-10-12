# Daikin Desktop Controller — Initial Requirements

## 1. Product overview

Daikin Desktop Controller (working title) is a Windows desktop application for monitoring and controlling a Daikin EcoSwing Smart Gold R-32 air conditioner.

The initial target unit is the FTKP09S5VL indoor unit paired with the RKP09S5VL outdoor unit (9,000 BTU/h, cooling-only variant).

The app will communicate with the air conditioner over the local home network and provide a fast, focused desktop experience for the everyday controls available in Daikin Smart AC. When away from home, the user will continue to use the official Daikin Smart AC app.

## 2. Product goals

- Provide a simple Windows-native way to control the air conditioner.
- Show the appliance's current state without requiring the mobile application.
- Prioritize local operation, low latency, and privacy.
- Keep the interface clear enough for frequent use from the desktop or system tray.

## 3. Target user

The primary user is the owner of one or more compatible Daikin EcoSwing Smart / Smart Gold R-32 units connected to the same local network as the Windows computer.

## 4. MVP scope

The MVP must support one configured air conditioner and the following functions:

- Automatically discover compatible Daikin units on the local network; allow manual entry of the device IP address if discovery does not find the unit.
- If discovery finds more than one compatible unit, show the candidates and let the user select one; the MVP supports one configured Air Conditioner at a time.
- If discovery finds exactly one compatible unit, select it automatically and start Pairing; if several are found, wait for the user to select one.
- Pair the application with a configured unit using its device secret key.
- After a unit is selected or its IP address is entered manually, validate the Device Secret Key by reading its status; consider the unit configured only after this succeeds.
- If the Air Conditioner rejects the Device Secret Key, show a clear error and provide a shortcut to the Secrets section to replace and save the key.
- Read and display the current device state.
- Display the current room temperature when the Air Conditioner reports it.
- Display all temperatures in degrees Celsius; the MVP will not offer a Fahrenheit option.
- Turn the unit on and off.
- Change the target temperature within the range reported by the device.
- While the target temperature slider is being dragged, update its displayed value without sending commands; send one command when the user releases the slider. Each plus or minus button click must send one command.
- Change the operating mode (for example cooling, heating, dry, fan, and auto) when supported by the unit.
- Change fan speed when supported by the unit.
- Provide on/off controls for Turbo Mode, Comfort Mode, and Economy Mode when supported by the unit.
- Refresh the status every 10 seconds by default while the compact control panel is visible; allow the user to choose 5, 10, 30, or 60 seconds in Settings.
- Refresh the device status immediately whenever the compact control panel is opened.
- Refresh the device status immediately after every control command sent by the user.
- If a status read fails, retry it once after a short delay; if the retry also fails, show the Air Conditioner as unavailable while keeping its configuration.
- While the Air Conditioner is unavailable, disable its controls and do not queue commands; re-enable them after a successful status read.
- If a control command times out or receives no response, refresh the device status to determine the actual state; do not resend the command automatically, and let the user retry it manually if needed.
- Display a useful error state when the device is offline, unreachable, or rejects a command.

## 5. User experience requirements

- The main screen must make the power state and target temperature immediately recognizable, and display the current room temperature as a secondary value when available.
- The target temperature must be adjustable using both a slider and plus/minus buttons, respecting the range and step size reported by the Air Conditioner.
- The interface must use familiar Windows visual conventions and controls.
- Commands must provide visible feedback: pending, succeeded, or failed.
- When a user changes a control, the interface must immediately display the requested value as pending, then reconcile it with the device-reported state after the command completes or a status refresh determines the actual outcome.
- Controls must remain interactive while a command is pending; pending commands must not disable the controls. This does not override the rule to disable controls while the Air Conditioner is unavailable.
- When multiple commands for a control are pending, a response to an older command must not overwrite a newer user-requested value; reconcile the displayed state using the response associated with the latest intent or a subsequent status refresh.
- If a command fails while the Air Conditioner remains reachable, show the device-confirmed state and an error beside the affected control, and allow the user to retry manually.
- Unsupported controls must be hidden or disabled with an explanation; the app must not imply that a command was applied when it was not.
- The app must expose a system-tray icon as its primary Windows entry point.
- The app must start automatically when the user signs in to Windows and remain available from the system tray while its compact control panel is dismissed.
- On the first launch, when no Air Conditioner is configured, automatically show the Compact Control Panel to guide setup; on later launches, start in the tray without opening the panel.
- Left-clicking the tray icon must open the compact control panel; clicking outside the panel must dismiss it.
- The compact control panel must not show standard close, maximize, or minimize buttons.
- The compact control panel must include a Settings button that opens a separate settings window.
- The Settings Window must include a language option that lets the user switch between Brazilian Portuguese and English.
- The Settings Window must include a Launch at Sign-in option that is enabled by default and can disable automatic startup with Windows.
- The Settings Window must use a standard Windows title bar and window controls, including a normal close action.
- The Settings Window must include a theme option with System, Light, and Dark choices; System is the default, while Light and Dark override the Windows theme. The selected preference must persist across launches.
- The Settings Window must provide device status refresh interval options of 5, 10, 30, and 60 seconds, with 10 seconds as the default.
- Changing the Theme Preference must update all open application windows immediately without requiring a restart.
- Changing the App Language must update all open application windows immediately without requiring a restart.
- Non-secret settings must be saved automatically when changed; no global Save button is required.
- On first launch, the app must use the Windows display language when it is Brazilian Portuguese or English; otherwise, it must default to Brazilian Portuguese. A language selected in Settings must be saved and used on later launches.
- The settings window must organize options into named sections in this order: Secrets, Device, and General.
- The Secrets section must be first and include an input for the device secret key.
- The Settings Window must include a Device section with an input for the device IP address as a manual connection fallback.
- The Device section must include the device status refresh interval option.
- The General section must include the App Language, Theme Preference, and Launch at Sign-in options.
- A manually entered device IP address must be saved and reused on later launches; if that address becomes unreachable, the app must retry Device Discovery.
- If Device Discovery does not find the unit, the compact control panel must offer a shortcut to the Device section in Settings.
- The device secret key input must be masked by default and provide a control to reveal or hide its value.
- The Secrets section must provide an explicit Save button; the app must confirm whether the key was saved successfully or report why saving failed.
- After the device secret key is saved, the app must automatically start Device Discovery and show its progress and result in the compact control panel.
- Right-clicking the tray icon must open a traditional context menu with only one action: exit the application completely.
- The app must not show Windows notifications in the MVP; status and errors are presented inside the compact control panel.

## 6. Security and privacy requirements

- Device secret keys must be stored using Windows Credential Manager or another operating-system-backed secure storage mechanism.
- Secrets, account credentials, device identifiers, and local network addresses must never be written to application logs by default.
- Local communication must remain on the local network for the MVP.
- The app must request only the information required to find and control the configured device.

## 7. Non-functional requirements

- The app must support Windows 10 and Windows 11.
- The interface must support Brazilian Portuguese and English from the first release, using a localization structure that can accommodate additional languages later.
- A normal local command should complete quickly enough to feel immediate; the app must show progress if a response is delayed.
- The application must tolerate the device becoming unavailable without crashing or losing its configuration.
- The implementation must separate the device-control layer from the user interface so that additional Daikin models and remote control can be added later.

## 8. Explicitly out of scope for the MVP

- Remote control through Daikin's cloud service. The user will use the official Daikin Smart AC app for this.
- Daikin account login inside the desktop application.
- Scheduling and automation.
- Energy-consumption reporting.
- Voice-assistant integration.
- Windows notifications.
- Multiple-device dashboards.
- Mobile, macOS, or web versions.

## 9. Assumptions and risks

- The target unit is already configured in the official Daikin Smart AC mobile application and connected to the home Wi-Fi network.
- The local protocol is based on community reverse engineering rather than a public Daikin API. Firmware or service changes may affect compatibility.
- Local status reading has been verified on the target FTKP09S5VL using the community Go implementation and the device secret key. Control writes have not yet been tested on this unit.
- The discovery result announced port 80, but the successful status read used the local control endpoint on port 15914; discovery metadata must not be treated as proof of the control endpoint.
- Available controls vary by unit model and configuration; the application must use the capabilities reported by the device.
- The user should not share device keys or Daikin account passwords with the application developer or through support channels.

## 10. Open questions for future ADRs

1. Resolved by ADR 0006: native C# with Zeroconf discovery; we own protocol maintenance and test vectors.

## 11. Technical validation notes

- On 2026-10-11, Device Discovery found the target unit at `192.168.1.157` on the home network.
- A read-only `get` using the community Go implementation, the device secret key, and `http://192.168.1.157:15914/` succeeded. This validates local connectivity and key acceptance for status reads on the target unit.
- The returned state reported power on, cooling mode, a 22 °C target, automatic fan, and a 22 °C room-temperature reading. The unit reported firmware `p1.0.3.33`.
- No `set` command has been issued to this unit yet. The MVP's control-write behavior remains unverified and must be tested explicitly before claiming end-to-end control support.
- The discovery output's `Port=80` did not identify the endpoint used by the successful status read. The application must use the validated protocol endpoint rather than infer it from that field.
