# Relay dispatch benchmark

This branch reproduces the packet-type dispatch comparison used for portfolio verification.

- JSON path: UTF-8 decode -> Jackson `readTree()` -> `type` extraction
- Magic-byte path: inspect the first byte only
- Metric: JMH AverageTime (`ns/op`)
- Warmup: 5 iterations
- Measurement: 10 iterations
- Forks: 3

This branch is benchmark-only and is not intended to be merged into `main`.
