# Changelog

All notable changes to this package are documented here.

## [2.1.5] - 2026-08-31

### Fixed

- Resolve path-only Unity Object updates to the asset's authoritative main
  object, so Prefab `GameObject` fields cannot silently bind an arbitrary child.
- Accept the emitted `assetPath`/`guid`/`fileID` descriptor when callers need an
  exact subasset identity, and reject ambiguous path-only subasset references.

## [2.1.4] - 2026-08-29

### Fixed

- Require VM Unity Automation 0.3.62 so framework adoption uses exact assembly
  output identities and Unity 6-compatible CleanBuildCache terminal evidence.

## [2.1.3] - 2026-08-29

### Fixed

- Require VM Unity Automation 0.3.61 so framework package adoption accepts
  Unity's documented CleanBuildCache rebuild only after every expected assembly
  has both build-start and precise terminal callback evidence.

## [2.1.2] - 2026-08-29

### Fixed

- Require VM Unity Automation 0.3.60 so framework project-tool adoption uses
  durable clean-compilation jobs that prove every expected script assembly
  actually completed.
- Normalize the 2.1.1 GamePrefab validation sources and regression fixture
  through the package's deterministic Unity GUID owner.

## [2.1.1] - 2026-08-28

### Fixed

- Make `vmframework/validate-game-prefabs` compare every discoverable config
  with the runtime `GlobalSettingCollector` provider graph, report
  `unregistered_game_prefab`, and expose registered/unregistered aggregate
  counts so orphan wrappers can no longer pass the final audit.

## [2.1.0] - 2026-08-21

### Added

- Add the read-only `vmframework/validate-game-prefabs` project tool. It scans
  every discoverable GamePrefab Wrapper and reports null configs plus missing,
  destroyed, or unreadable `IPrefabProvider.Prefab` references with bounded
  structured issues and complete aggregate counts.

## [2.0.3] - 2026-08-21

### Fixed

- Serialize destroyed Unity object references as explicit diagnostic values
  instead of dereferencing them and aborting GamePrefab inspect/update tools.

## [2.0.2] - 2026-08-21

### Fixed

- Require VM Unity Automation 0.3.23 so framework project tools share the
  corrected VFX Block Activation Slot contract.

## [2.0.1] - 2026-08-20

### Fixed

- Require VM Unity Automation 0.3.1 so framework project tools consume the
  corrected request-metadata boundary.

## [2.0.0] - 2026-08-20

### Changed

- Require VM Unity Automation 0.3.0 and publish its renamed Automation-owned
  schema extensions through framework project-tool contracts.
- Remove the final retired-transport terminology from current package history.

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
  removal of the retired route and type surface.

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
