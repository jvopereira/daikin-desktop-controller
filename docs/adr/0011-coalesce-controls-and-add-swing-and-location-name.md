# 11. Coalesce controls and add Swing and Location Name

## Status

Accepted

## Decision

All air conditioner controls share a trailing 500 ms debounce. The panel
immediately shows Pending Command values, but only the latest unsent value for
each control is retained. Commands execute sequentially in order of their last
changes and are checked against current reported support before sending. Values
already satisfied at dispatch are skipped. An in-flight command finishes
normally; sequence checks protect newer intent. Enabling Turbo or Economy
immediately clears the other's pending value, matching the existing vendor
presets.

This supersedes the “each intent sends once” rule in ADRs 0007–0009:
intermediate unsent intents are discarded. Sent commands still are not retried
automatically, and existing status reconciliation remains in place. Hiding the
panel keeps its monitoring session alive until pending work finishes, then stops
it. Reopening reuses that session. Exiting, configuration changes, and
unavailable status cancel pending work.

Swing uses `port1.v_swing`, writes numeric 1 or 0, and follows ADR 0009's
optional numeric/boolean support detection and unavailable-control presentation.
The panel places Operating Mode and Fan Speed on one row, with Turbo, Airflow,
and Swing below, preserving its window dimensions and theme resources.

Location Name is a local preference, edited directly in the panel using a
keyboard-accessible button and inline field. Enter or lost focus saves; Escape
cancels. Leading/trailing whitespace is removed, the length is limited to 40
UTF-16 code units, and empty means the translated default. Existing preference
files need no migration. Persistence failures retain the previous name and show
the existing localized storage error. The name stays available when Room
Temperature is absent.

## Validation

Use a simulated clock for debounce, supersession, cancellation, and in-flight
tests. Test Swing serialization and optional-field parsing in the protocol
suite. Before release, validate Swing on the real unit and check inline editing,
hidden-panel draining, both languages, Light/Dark themes, keyboard access, and
Windows display scaling.

## Reference

- [Local state mapping, including
  v_swing](https://github.com/crossworth/daikin/blob/main/state.md)
