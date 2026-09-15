# Relay dispatch benchmark

Reproduces the packet-type dispatch comparison used for portfolio verification.

- JSON path: UTF-8 decode -> Jackson `readTree()` -> `type` extraction
- Magic-byte path: inspect the first byte only
- Metric: JMH AverageTime (`ns/op`)
- Warmup: 3 iterations
- Measurement: 5 iterations
- Forks: 3

The benchmark code and workflow are kept in the repository for reproducibility.
The GitHub Actions workflow is manual-only and is not part of the regular CI pipeline.
