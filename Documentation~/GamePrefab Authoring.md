# GamePrefab authoring

Discover the current creation and update schemas through the official Unity CLI
catalog. Both operations use the same serialized-value conversion and member
resolution, including inherited private fields and writable properties.

Nested JSON objects describe the target type's fields and properties. An optional
`$type` supplies an assignable concrete type for a managed reference. Arrays and
collections convert each element to its declared type. Localization references
use their serialized table and entry fields; serialization callbacks run after
structured values are populated.

Unity Object references accept an asset path or an identity descriptor containing
`assetPath` and optional `guid` and `fileID`. Use the identity descriptor when a
file contains multiple compatible objects. The reference resolver owns identity
validation and reports ambiguous or missing references at the authored member.

Creation receives identity through its root `id` argument. Supplying `id` or `_id`
inside `serializedValues` is rejected. Updates retain their explicit identity
migration operation. Unknown members, incompatible concrete types and invalid
references fail rather than substituting another value.

The creation producer finishes configuration before creating, registering and
saving a wrapper in its matching GeneralSetting. Updates keep their transaction,
rollback and semantic readback contract. Inspect the returned nominal GamePrefab
reference after either operation and run global GamePrefab validation after the
complete authoring task.

## Runtime inspection and UI query scopes

Runtime panel visibility reports input permission separately as `uiEnabled`.
`actuallyVisible` follows the open state and rendered hierarchy, so a visible
Tooltip can have input disabled without being reported as hidden.

VisualElementPath validation follows each field's authored query scope.
Panel paths use the UIDocument tree, while local paths use the nearest
non-GameItem `IVisualElementGenerator`, respecting `MustFromParent`. Each
provider generates one audit tree per panel invocation. Runtime-generated
entries do not require placeholder instances in production UXML.
