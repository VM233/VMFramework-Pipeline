# Changelog

All notable changes to this package are documented here.

## [1.1.3] - 2026-08-20

### Fixed

- Require VM Unity Automation 0.2.4 after removal of the last retired
  transport-route metadata.

## [1.1.2] - 2026-08-20

### Fixed

- Require VM Unity Automation 0.2.3 so all framework contracts compile against
  the complete transport-neutral public API.

## [1.1.1] - 2026-08-20

### Fixed

- Require VM Unity Automation 0.2.1 with the corrected split-contract compile
  surface.

## [1.1.0] - 2026-08-20

### Changed

- Require VM Unity Automation 0.2.0 and its transport-neutral public API after
  removal of the retired MCP route and type surface.

## [1.0.3] - 2026-08-20

### Fixed

- Require VM Unity Automation 0.1.7 so the framework package owner survives
  adaptation into the bounded CLI catalog.

## [1.0.2] - 2026-08-20

### Fixed

- Declare the package owner once at assembly scope for thread-safe background
  catalog discovery through VM Unity Automation 0.1.6.

## [1.0.1] - 2026-08-20

### Changed

- Require VM Unity Automation 0.1.5 so CLI discovery reports this package as
  the owner of its 28 framework project tools.

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
