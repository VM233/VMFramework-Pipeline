# Native serialization migration

Capture explicit asset paths with `vmframework/capture-serialization-snapshots` before changing
their serialization schema. Then use `vmframework/apply-serialization-snapshots` to restore the
captured fields into the new schema and verify a real Unity save/unload/import/load round trip.
Snapshots retain source hashes, object types, shared references, and persistent Unity identities.
Changed source files and missing fields fail at the responsible asset. Each asset is independently
atomic; a later asset failure does not undo previously verified assets.
The staged reader explicitly maps the former GamePrefab tag, input-action, and provider fields to their
native fields and converts Type, Guid, and set values to the corresponding native schema.
Restoration invokes native deserialization callbacks after populating each value, so serialized
fields and their derived runtime state agree before Unity saves them. Fields declared NonSerialized
in the current schema are runtime state and are excluded from migration.

## Static Cost Ledger

The command accepts at most 32 explicit assets, each at most 2 MiB. No AssetDatabase-wide search
is performed. The frozen migration inventory is 42 / 623 / 311 assets and 995 / 19546 / 8342 Odin
nodes across MarbleBattlers / Balance / BattleIdle; the largest persisted asset is 28485 bytes.
Each graph allows at most 65536 values and field definitions, depth 64, and 128 fields per type. Shared
reference identity terminates graph cycles. Asset graphs are processed individually, so the
peak retained asset data is one source byte snapshot and two graph trees, budgeted at 128 MiB.
Each apply invokes one save, one unload, one import, and one load; the exceptional rollback adds
one byte write and one import. Deserialization callbacks run once per restored callback value;
the frozen Localization callbacks rebuild state from their own captured fields and variable lists,
so their traversal is bounded by the same 65536 values per graph. Metadata is read once per type and indexed by field name within
one graph invocation; the cache ends with that invocation. There is no Cartesian asset scan.
The bounded traversal budget is 2097152 values and 32 save/import round trips per call on the
Editor thread. Calls are kept below the official CLI command timeout by using smaller explicit
batches when an asset's measured import cost requires it. Pass for the frozen inventory.

The snapshot commands are a staged migration surface. Retire their legacy schema conversions
after every controlled consumer has adopted and verified native serialization.
