# VMFramework Pipeline

VMFramework Pipeline is an Editor-only extension for the official Unity CLI
Pipeline automation catalog. It contributes VMFramework-aware commands for
GamePrefabs, GeneralSettings, GameTags, UI panels, properties, runtime sessions,
procedures, Logic Ticks, and reference tracing without registering another
transport or a large top-level CLI command list.

## Architecture

The official Unity CLI remains the only process boundary. `VMUnityPipeline`
publishes five stable facade commands, `VMUnityAutomation` owns the bounded rich
catalog and command lifecycle, and this package owns only the `vmframework/...`
domain contracts and handlers.

Discovery is deliberately progressive:

1. `vm_catalog_list` searches a small page of contracts.
2. `vm_catalog_get` returns one exact input/output schema.
3. `vm_automation_call` executes that discovered command with one JSON object.

The framework tools therefore do not appear as hundreds of separate official
CLI commands. Their schemas remain fully available only when requested.

## Requirements

- Unity 6000.4 or newer.
- The official Unity CLI and `com.unity.pipeline` versions supported by
  `VMUnityPipeline`.
- `com.vm233.unity-automation` 0.2.3.
- The VMFramework, VMCore, VM Odin Extensions, and Unity Localization
  dependencies declared by `package.json`.

## Installation

Pin both this package and its Automation owner directly in the consuming
project with immutable remote Git revisions:

```json
"com.vm233.unity-automation": "https://github.com/VM233/VMUnityAutomation.git#bca4f6e94d30d27afb56ec28bd1042d0683f0f64",
"com.vm233.vmframework-pipeline": "https://github.com/VM233/VMFramework-Pipeline.git#eed220c114dda4e5181a833d3df3ea185b4547ec"
```

Let Unity update `Packages/packages-lock.json`. Do not use a local package,
embedded override, symlink, junction, or mutable branch pin.

## Discovery and execution

Set the exact checkout path before invoking the official CLI. Project names are
display metadata and never participate in binding.

```powershell
$env:UNITY_PROJECT_PATH = 'D:\UnityProjects\YourProject'
$env:UNITY_NO_CONSENT_PROMPT = '1'
unity command vm_catalog_list --query vmframework --limit 10 --format json
```

Inspect one returned identifier with `vm_catalog_get`, then pass that same
identifier to `vm_automation_call`. Mutating contracts require the exact
absolute `expected_project_path`; dangerous contracts additionally require the
published confirmation argument. Do not infer arguments from a command name:
the exact catalog schema is authoritative.

For repeated calls, use one `unity shell --protocol ndjson` process. Each input
line is one correlated official CLI request, so catalog and invocation output
stay bounded without paying process startup and discovery cost each time.

## Capability families

The package publishes 28 canonical `vmframework/...` contracts covering:

- effective settings and GeneralSettings discovery;
- GamePrefab type discovery, search, config query, inspection, creation, and
  transactional update;
- GameTag listing, localized upsert, and validation;
- UI panel, bind-object, container-panel, and VisualElementPath inspection;
- PropertyManager reads, runtime writes, and bounded traces;
- owner-scoped runtime GameItem sessions and one-shot inspection;
- runtime UI panel lifecycle, binding, visibility, and persistent waits;
- Procedure state and Logic Tick query/control;
- wrapper, Prefab, component, tag, localization, dependency, and reverse
  reference tracing.

The catalog is the sole command inventory authority. It publishes operation
kind, side effects, lifecycle requirements, error codes, data-product links,
and complete strict JSON schemas. This README intentionally does not duplicate
the full generated command table.

## Domain ownership

The runtime GameItem domain adapter is the only extension point for gameplay
facts that VMFramework does not own. Implementations must use authoritative
gameplay components for faction, abilities, and lifecycle; names, hierarchy,
Prefab paths, tags, and UI state are not substitutes.

Long waits and reverse-reference scans use persistent Automation jobs. Poll,
cancel, and clean them only through the job contracts returned by the catalog.
Runtime session cleanup tokens identify resources owned by that session and do
not authorize arbitrary pool returns.

GamePrefab updates use one transactional path. A successful commit returns
verified current identity and SHA-256 evidence. A failed mutation is reported
as rolled back only after byte restoration, synchronous import, and semantic
readback all succeed; restoration failure remains a distinct terminal result.

## Configuration

Effective values use this precedence:

1. explicit command argument;
2. `Project Settings > VMFramework Pipeline`;
3. `Preferences > VMFramework Pipeline` or the shared
   `Preferences > VM Unity Automation` result budget;
4. package default.

`ProjectSettings/VMFrameworkPipelineSettings.json` is team-owned and contains
only GameTag validation coverage. Personal inspection depth, collection size,
optional snapshots, and property-trace capacity remain Editor preferences.

See [Documentation~/configuration.md](Documentation~/configuration.md) for the
per-command ownership and response audit.

## Development

This package contains no runtime assembly. Its test assembly is
`VMFramework.Pipeline.Editor.Tests`; tests remain opt-in and are not a substitute
for compiling the consuming project after each published immutable revision.
