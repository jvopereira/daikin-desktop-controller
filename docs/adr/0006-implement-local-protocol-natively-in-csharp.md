# Implement the local protocol natively in C#

**Status:** Accepted

Implement discovery and status reads in a WPF-free `net10.0` device-control library. The protocol is small and uses BCL cryptography. Use the MIT-licensed Zeroconf NuGet package for mDNS/DNS-SD.

The community Go reference has no license, so neither bundling nor porting it is cleared. Bundling would also add a runtime, binary distribution and process IPC. The MIT-licensed vendor reference [pyiotdevice](https://github.com/daikin-br/pyiotdevice) documents the wire format.

Discovery uses `_iota._tcp.local.`; its advertised port is never a control endpoint. Local control always uses TCP port 15914. Raw TCP handles the unit's indented HTTP headers. CRC validation precedes AES-CFB128 decryption; successful JSON status parsing validates the Device Secret Key.

## Consequences

We own protocol maintenance and independent test vectors. CRC is corruption detection, not authentication. Keep all protocol code behind the device-control boundary, and keep secrets in Windows Credential Manager. Firmware changes may require updates.

Zeroconf does not expose the SRV target hostname. For advertisements without `apn`, use a bounded reverse lookup for the fallback hostname; when reverse DNS is unavailable, retain the IPv4 address as identity. The advertised port remains ignored.
