# 0008: Derive operating mode and fan speed options from vendor table

Accepted: 2026-10-11

The air conditioner reports current mode and fan codes but no supported-option list. Following [ADR 0007](0007-derive-target-temperature-range-from-operating-mode.md), derive options from the vendor tables in [pyiotdevice device_utils.py](https://github.com/daikin-br/pyiotdevice/blob/main/pyiotdevice/device_utils.py) and [ha-custom-integration climate.py](https://github.com/daikin-br/ha-custom-integration).

| Operating Mode | Code written |
| --- | --- |
| Auto | 1 |
| Dry | 2 |
| Cool | 3 |
| Heat | 4 |
| Fan | 6 |

For compatibility with ADR 0007 and this controller’s existing temperature behavior, reported 0 and 1 both mean Auto, even though the vendor mapper names 0 as off. Power remains the separate source of on/off state. Unknown codes have no selected option but users can select a known mode. The Compact Control Panel omits Heat from selectable modes at the user’s request. The device layer retains code 4 for interpreting reported status and protocol compatibility. Mode writes leave power alone. Dry writes also set fan to 17, matching the vendor rule.

| Fan Speed | Code |
| --- | --- |
| Low | 3 |
| MediumLow | 4 |
| Medium | 5 |
| MediumHigh | 6 |
| High | 7 |
| Auto | 17 |
| Quiet | 18 |

Fan speed changes require a known reported speed and Auto, Cool, Heat, or Fan mode. Dry, unknown modes, missing fan fields, and unknown fan codes disable the control with a localized explanation. Non-numeric fan fields are treated as absent without rejecting otherwise valid status.

Both controls reuse Pending Command reconciliation: each intent sends once, timeout refreshes status without resending, failures restore confirmed values, and sequence checks prevent older completions clearing newer intent. English and pt-BR labels refresh when App Language changes.

Per ADR 0004, manual Windows validation against the real unit remains required before release: switch through every mode and speed, verify Pending then confirmed, verify Dry disables fan speed, verify power stays unchanged, and repeat with pt-BR.
