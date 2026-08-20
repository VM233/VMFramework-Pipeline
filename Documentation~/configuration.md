# VMFramework Pipeline configuration and project-tool audit

This document is the configuration review for the 28-tool VMFramework Pipeline
catalog. The tool-catalog regression test owns the exact list, requires one
operation kind per tool, requires strict schemas, and registers every valid
tool in the bounded Automation catalog consumed by `vm_automation_call`.

## Ownership and precedence

The effective value order is:

1. explicit project-tool argument;
2. team setting in `ProjectSettings/VMFrameworkPipelineSettings.json`;
3. user preference in `Preferences > VMFramework Pipeline`, or the shared
   `Preferences > VM Unity Automation > Tool Responses` result budget;
4. built-in default.

Hard caps, Play Mode requirements, rollback behavior, selector ambiguity, and
schema validation are invariants rather than settings.

### Team settings

Only GameTag validation coverage is team-owned:

| JSON field | Initial value | Consumer |
|---|---:|---|
| `gameTagValidation.includeMissingTranslations` | `true` | `validate-game-tags` |
| `gameTagValidation.includeGamePrefabReferences` | `true` | `validate-game-tags` |

These values define what the team's normal content audit covers. An explicit
tool argument still wins for a one-off focused validation.

### User preferences

| Preference | Initial value | Consumers |
|---|---:|---|
| GamePrefab inspection max depth | `8` | `inspect-game-prefab`, `update-game-prefab` |
| GamePrefab collection item limit | `100` | `inspect-game-prefab`, `update-game-prefab` |
| Include GamePrefab update snapshots | `false` | `update-game-prefab` |
| Property trace retained event limit | `1000` | `start-property-trace` |

These control local response size or diagnostic capacity, not project content.

The shared VM Unity Automation result preference is consumed by tools with one obvious
primary result collection: GamePrefab type/list/search/query tools, GeneralSettings,
VisualElementPath results, PropertyManagers, GameTags, GameTag issues, and
property-trace event pages. Explicit `limit` or `maxIssues` values win, and
each VMFramework tool keeps its own hard maximum.

## Per-tool review

| Tool | Configurable default | Explicit-only fields and decision |
|---|---|---|
| `get-configuration` | None | Read-only effective snapshot; accepts no arguments. |
| `list-game-prefab-types` | Shared result limit | Filter and abstract/interface inclusion change the requested set and remain explicit. |
| `add-game-prefab` | None | ID, type, overwrite, asset name, and serialized values define an asset mutation. Asset folders remain authoritative in VMFramework GeneralSettings. |
| `find-game-prefab` | Shared result limit | ID, filter, and type are selectors. Every result must be registered to exactly one type-compatible GamePrefabGeneralSetting; orphaned or multiply owned matches fail instead of becoming partial references. |
| `query-game-prefab-configs` | Shared result limit | ID, filter, assignable type, GameTag all/any/none, configured-name state, and enabled-description state select configs. `fields` owns the GameTag/name/description projection, and `locales` narrows localized values. An unconfigured optional name is omitted rather than replaced by an ID. Identity-only results are the default; full serialized content remains owned by `inspect-game-prefab`. |
| `inspect-game-prefab-wrapper` | Shared result limit | Exact ID/path and filter are selectors. Missing exact targets now fail instead of looking like an empty broad query. |
| `list-general-settings` | Shared result limit | `includeGamePrefabDetails` can repeat large provider lists and defaults off. |
| `inspect-ui-panel` | None | Exactly one `panelID` or `prefabPath` is required; runtime-state inclusion is request-owned and defaults off. |
| `inspect-bind-objects` | None | Exactly one `panelID` or `prefabPath` is required; runtime counts are request-owned and default off. |
| `validate-visual-element-paths` | Shared result limit | Exactly one `panelID`, one `prefabPath`, or `allPanels: true` is required. Valid records default off; all-panel output uses one global page, separates source errors from invalid paths, and excludes fields disabled by resolvable Odin `ShowIf`/`HideIf` conditions. |
| `inspect-container-panel` | None | Exactly one `panelID` or `prefabPath` is required; runtime state is request-owned and defaults off. |
| `inspect-property-manager` | Shared result limit | Target, child traversal, property filter, and selection usage remain explicit. Omitted selectors scan loaded scenes rather than depending on hidden Editor selection. |
| `inspect-game-prefab` | VMFramework depth/item preferences | A nominal GamePrefab reference from add/find selects and revalidates the exact registered asset. |
| `update-game-prefab` | VMFramework depth/item/snapshot preferences | A nominal GamePrefab reference selects and revalidates the existing object; ordered typed operations define the mutation. A root `id` set operation performs an atomic identity migration, and post-save verification follows the new ID. Complete snapshots default off; bounded operation summaries and semantic diff remain. |
| `list-game-tags` | Shared result limit | ID/group/filter and locale-value expansion remain explicit; locale values default off. |
| `upsert-game-tag` | None | Group, ID, localization keys/values, registration, and dry-run choices define the mutation. Framework `GameTagGeneralSetting` remains the localization-table authority. Global post-validation is opt-in because the dedicated validation tool owns normal audits. |
| `validate-game-tags` | Team validation coverage; shared issue limit | Explicit coverage flags can narrow one call. |
| `get-property` | None | Manager and property selectors remain explicit. |
| `set-property` | None | Target, value, and `initial` are runtime mutation inputs. The tool is classified as runtime-mutating and requires Play Mode. |
| `start-property-trace` | VMFramework retained-event preference | Target/filter/child traversal remain explicit. Starting a trace mutates diagnostic session state and is not read-only. |
| `get-property-trace` | Shared result limit | Offset/limit select a page; the call no longer exposes a hidden clear mutation. |
| `stop-property-trace` | Shared result limit | Stopping mutates diagnostic session state; returned events are paginated. |
| `runtime-game-item-session` | None | Action, GamePrefab ID, placement, properties, faction, optional Panel binding, and owner-scoped session key define the temporary runtime mutation. Cleanup can return only the item owned by that session; it cannot substitute for a project's spawn, death, drop, or arbitrary-pool-return semantics. |
| `inspect-runtime-game-item` | None | Exactly one session token, Unity object ID, or GameObject path selects a live item. Generic identity, tags, properties, containers, and lifecycle remain framework-owned; abilities, faction, and other project facts come only from an explicit domain adapter. |
| `runtime-ui-panel` | None | Panel ID, action, object selector, binding token/name, wait condition, and timeout remain explicit. Waits require a persistent Job, and actual visibility remains an explicit business boolean. |
| `procedure-state` | None | State queries have no hidden target. Wait-set constraints, loading state, timeout, and `runAsJob` remain explicit because they define the requested lifecycle contract. |
| `logic-tick-control` | None | Query/start/stop/advance/wait, tick count, target tick, timeout, and Job execution remain explicit. Advancing Logic Ticks is reported as an exact side effect. |
| `reference-trace` | None | Query, semantic kind, property filter, reverse-reference choice, and graph/component/reference budgets remain explicit. Reverse-reference scans require a persistent Job; no hidden project root or type-name inference is used. |

## Values deliberately not configurable

The following remain request-owned or invariant:

- asset paths, scene paths, object IDs, GamePrefab IDs/types, GameTag groups,
  property names, and filters;
- `overwrite`, `registerGroup`, `dryRun`, `initial`, ordered operations, and
  mutation values;
- large diagnostic expansions (`includeRuntime`, projected GamePrefab config
  fields, all locale values, all valid paths, complete before/after snapshots,
  and global validation after upsert);
- hard result/depth/event caps, strict unknown-argument rejection, exact target
  ambiguity failures, Play Mode safety, rollback, and readback verification.

Duplicating VMFramework content settings inside the CLI extension is also prohibited.
GamePrefab folders and the default GameTag localization table continue to come
from their owning VMFramework GeneralSettings.

## Response contract

- List and trace tools expose pagination metadata only when another page
  exists; VM Unity Automation's result normalization removes redundant
  completed-page aliases.
- A zero-match primary collection is preserved as an empty collection, so a
  completed queue ticket never loses the semantic result.
- GamePrefab update replies always retain the verified nominal `gamePrefab`
  reference, bounded operation summaries, and a semantic diff. Complete
  before/after snapshots are opt-in. When an update changes the root GamePrefab
  ID, `gamePrefab.id` is the verified new identity and `previousId` records the
  identity supplied by the input reference. Successful updates return
  `terminalState="committed"` with wrapper/meta SHA-256 evidence. Failed updates
  return `rolled_back` only after atomic byte restoration, synchronous import,
  and semantic readback; otherwise they return `rollback_failed` with separate
  original and rollback errors.
- Upsert replies contain focused readback. A potentially large global GameTag
  validation is opt-in or obtained from `validate-game-tags`.
- GameTag validation replies include the effective coverage flags so callers
  can distinguish team defaults from an explicitly narrowed audit.
- VM Unity Automation 0.1.6 preserves `inputSchema` and `outputSchema`
  verbatim, so the reference trace's business `tags` property remains an array
  of tag records rather than being interpreted as capability metadata.
- Catalog selection uses module `vmframework`, capability nouns derived from
  tool names or explicit metadata, and normalized `inspect`, `mutate`, or `job`
  operation kinds. Exact schemas are inspected only when the client gets one
  typed tool.
