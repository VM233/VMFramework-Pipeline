# Changelog

All notable changes to this package are documented here.

## [1.0.0] - 2026-08-20

### Added

- Publish 28 VMFramework-aware project contracts through the bounded
  `VMUnityAutomation` catalog consumed by the official Unity CLI Pipeline.
- Cover GamePrefabs, GameTags, UI panels, properties, runtime sessions,
  procedures, Logic Ticks, configuration, and reference tracing.
- Preserve exact input/output schemas, side effects, lifecycle requirements,
  stable errors, data-product links, persistent jobs, and transactional
  GamePrefab evidence without registering one top-level CLI command per tool.

### Changed

- Replace the former server-bound package identity with the transport-neutral
  `com.vm233.vmframework-pipeline` package and `VMFramework.Pipeline.Editor`
  assembly.
- Require `com.vm233.unity-automation` 0.1.4 and use package-owned deterministic
  Unity GUIDs so migration can be resolved without asset identity collisions.
- Split independent lifecycle and command owners into one top-level type per
  source file.

### Removed

- Remove the companion server, host configuration, direct-route publication,
  and legacy transport terminology from the package contract.
