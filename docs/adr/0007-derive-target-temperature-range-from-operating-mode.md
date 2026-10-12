# 0007: Derive target temperature range from operating mode

Accepted: 2026-10-11

The unit reports its operating mode but no temperature range or step. Issue #5's wording about limits “reported by the device” cannot be implemented literally. Use the vendor capability table from [daikin-br/ha-custom-integration](https://github.com/daikin-br/ha-custom-integration): Cool (3) 18–32 °C, Auto (0/1) 18–30 °C, Heat (4) 10–30 °C, all with a 1 °C step. Dry (2), Fan (6), and unknown modes disable temperature writes with an explanation.

The [vendor protocol reference](https://github.com/daikin-br/pyiotdevice) sends compact command JSON with a literal `BZ` suffix. Requests contain a random 16-byte IV, AES-CFB128 ciphertext, and a little-endian CRC16 over IV and ciphertext; unlike responses they have no skipped byte. Raw POST requests use Host and Content-Length headers. Replies contain full post-apply status without error fields, so compare status against the requested value.

Pending values represent user intent until reconciliation. Each intent sends once. Timeout triggers a status refresh without resending. Manual retry means making the change again; no retry button is added.

Per [ADR 0004](0004-use-verified-local-daikin-protocol.md), writes remain unverified against the real unit until manual Windows testing.
