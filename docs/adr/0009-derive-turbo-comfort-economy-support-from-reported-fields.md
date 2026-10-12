# 0009: Derive Turbo, Comfort and Economy support from reported fields

Accepted: 2026-10-11

The local status has no capability list, as described in ADRs 0007 and 0008. Turbo Mode uses `powerchill`, Comfort Mode uses `coanda`, and Economy Mode uses `econo` in `/acstatus` `port1`. Each feature is supported only when its field is numeric or boolean. Numeric zero means off; other numbers mean on. Missing or non-numeric, non-boolean fields are treated as absent without rejecting otherwise valid status. The Compact Control Panel shows a disabled toggle with a localized “not reported” explanation.

Each command writes its own field as 1 or 0. Following the vendor preset rule, enabling Turbo also writes `econo:0`, and enabling Economy also writes `powerchill:0`. Disabling either writes only its own field. Comfort is independent. Support and writes do not depend on power or Operating Mode, and Turbo does not lock Fan Speed or Target Temperature. If a command is refused, normal reconciliation shows it as not applied; effects on the other feature appear in refreshed status.

All three controls reuse Pending Command: immediate pending values, sequence protection for newer intent, confirmed values and errors on failure, manual retry, and one status refresh after timeout without resending. Labels and errors support English and pt-BR. The existing ScrollViewer accommodates the controls without changing window size.

Per ADR 0004, writes require manual Windows validation against the real unit before release. The README checklist covers each toggle, mutual exclusion, pending confirmation, missing fields, network interruption, and pt-BR.

## References

- [State mapping](https://github.com/crossworth/daikin/blob/main/state.md)
- [Vendor integration](https://github.com/daikin-br/ha-custom-integration)
