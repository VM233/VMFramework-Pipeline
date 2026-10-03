# VMFramework Pipeline

VMFramework Pipeline supplies VMFramework-aware Editor tools through the official
Unity CLI Pipeline automation catalog. It covers GamePrefab authoring, settings,
properties, UI and runtime inspection without adding another transport.

## Requirements

- Unity 6000.4 or newer
- The official Unity CLI and `com.unity.pipeline` supported by `VMUnityPipeline`
- Dependencies declared by `package.json`

## Installation

Pin this package and its Automation owner with immutable remote Git revisions:

```json
"com.vm233.unity-automation": "https://github.com/VM233/VMUnityAutomation.git#<full-commit-sha>",
"com.vm233.vmframework-pipeline": "https://github.com/VM233/VMFramework-Pipeline.git#<full-commit-sha>"
```

Let Unity resolve `Packages/packages-lock.json`. Use remote pins rather than local
or embedded package overrides.

## First use

Set the exact project path and search a bounded catalog page:

```powershell
$projectPath = (Resolve-Path .).Path
unity --json command --project-path $projectPath vm_catalog_list -- --query vmframework --limit 5
```

Use `vm_catalog_get` to discover one returned command's current contract and
`vm_automation_call` to execute it. The official CLI owns project binding and
transport; `VMUnityAutomation` owns execution and jobs; this package owns the
VMFramework domain. The catalog is the command and schema authority.

## Package layout and documentation

- `Editor/`: domain contracts, handlers and configuration
- `Tests/Editor/`: opt-in Editor regressions
- `Documentation~/`: detailed usage and configuration

See [GamePrefab Authoring](Documentation~/GamePrefab%20Authoring.md),
[New Project Settings](Documentation~/New%20Project%20Settings.md),
[Configuration](Documentation~/configuration.md) and
[Native Serialization Migration](Documentation~/Native%20Serialization%20Migration.md).
Release changes appear in [CHANGELOG](CHANGELOG.md); licensing is in [LICENSE](LICENSE).

Tests are opt-in through the package's test assembly. Read its `.asmdef` for the
current assembly identity and use the official Automation Test Runner contracts.
