# Use the verified local Daikin Smart AC protocol

**Status:** Accepted

The application will communicate with the target FTKP09S5VL through the reverse-engineered Daikin Smart AC local protocol, using the device secret key and local endpoint on port `15914`; it will not use Daikin cloud control as a fallback. This protocol was validated by successfully reading the target unit's status with the community Go implementation, while the discovery output's advertised port 80 was not the endpoint used for that read. The protocol is unofficial and firmware-sensitive, and control writes remain unverified on this unit. The integration mechanism is resolved by [ADR 0006](0006-implement-local-protocol-natively-in-csharp.md): native C# in a WPF-free device-control library.

## Consequences

- The app must communicate only with the unit on the local network and must securely store and use its device secret key.
- Device Discovery and manual IP configuration remain necessary because the unit's local IP may change.
- The protocol implementation must be isolated behind the device-control boundary so its implementation can be changed without redesigning the WPF UI.
- Read status is verified on the target model; command writes and individual control capabilities must be validated before release.
- Daikin does not provide this as a public local API; compatibility may change with firmware or service updates.

## References

- [crossworth/daikin](https://github.com/crossworth/daikin)
- [Verified target model state mapping](https://github.com/crossworth/daikin/blob/main/state.md)
