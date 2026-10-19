# GamePrefab value evidence static cost ledger

The frozen witness is the saved `Project Greenhouse F32 Enclosure Native Write
Receipt.json` from DoomsdayDiary on 2026-10-19. The original float changes from
`-0.47999998927116394` to `-0.48000001907348633`; the operation and native snapshots
retain it, while the old semantic diff is empty. Both values format as `-0.48`
under the Editor runtime's default single-precision general formatting. The same
string comparison can incorrectly accept a rollback with a different scalar.

The owner is `VMFrameworkPipelineGamePrefabTools`. Its descriptor publishes the
native typed leaf values; semantic comparison and diff must consume those values
without converting them to display strings. Missing members and present nulls
are different states. Existing conversion, save, import, registration, snapshot
depth and collection admission do not change.

For a descriptor with N visited nodes and L retained leaves, existing flattening
visits N nodes per snapshot, creates two L-entry dictionaries and a union with at
most 2L keys, then sorts at most 2L keys. Exact leaf comparison adds no traversal
axis: all ordinary leaves are primitives, enums, strings, decimal or null. Empty
collections use one shared immutable empty array; their equality has zero element
visits. Rollback retains its existing one paired traversal, O(N). No new asset
search, import, save, worker, cache, static mutable state or Unity call is added.
Typed values replace the old allocated scalar display strings.

The regression domain is frozen to two adjacent float leaves, two adjacent
double leaves, five scalar type/null collision pairs, one empty collection/string
pair, one absent/present-null pair and an equal nested native descriptor control.
Each synthetic pair has at most 8 descriptor nodes, 4 leaves and depth 4. One
native transaction fixture owns one Wrapper, one GeneralSetting and two requested
scalar updates; it saves/imports through the unchanged production transaction and
retires both assets in finally. At most one such fixture is live. No Cartesian
product with project content is introduced. The focused formal Test Runner run
and a public CLI update of the isolated fixture are separate acceptance gates.

Budget: no additional main-thread Unity operation on production updates; no
scalar formatting allocation; O(N + L log L) existing diff complexity and O(N)
rollback complexity; tests at most 32 KiB additional managed descriptor data and
one bounded two-asset native fixture. Status: PASS for implementation admission.
Compilation, native regression and formal CLI acceptance remain unverified until
their receipts are retained. The public diff preserves typed before/after values
and explicit presence flags; consumers must not infer absence from a string.

## Native registration admission correction

The first native package run `b79dccfd28c8` completes all11 independent cases:
10 scalar/equality controls pass, while the native save transaction rejects its
fixture before mutation because its GeneralSetting is outside the authoritative
search folders. This is a test producer defect, not a relaxed registration rule.
Both the scalar and existing rename transaction fixtures now obtain their asset
directory from the actual production GeneralSetting search-folder owner and its
existing native valid-folder fact. The owner publishes at most two folders, so
this adds at most two native folder observations per fixture, O(1), with no asset
scan, fallback, copied folder configuration or production registration change.
The next focused run includes the11 original cases and the existing rename case,
12 total, serially; at most one two-asset fixture is live. Existing scalar and
registration assertions are retained. Admission budget remains PASS; the first
failed native receipt remains separate from the corrected replay.

## Shared managed-reference evidence

The public3.0.29 inspection of Field Camp publishes six `$cycle=true` descriptors
for the same shared empty `SkillRequirement[]`. The global visited set conflates
a previously described sibling with a cycle and loses its target identity. The
owner will retain one reference-identity-to-first-JSON-Pointer map, using `#` for
the root, escaped field components, and `/items/<index>` for a descriptor's
collection entries. Every repeated object publishes only its type and `$ref`.
Genuine ancestor cycles use that same precise reference product. There is no
recursive alias expansion, copied graph, second serializer or changed depth,
collection, save, registration, rollback or diff threshold.

For U unique admitted reference objects and E retained member/item edges, the
existing traversal still visits each unique object once and each edge once.
The map replaces the existing U-entry set; each first entry also retains its
descriptor pointer. Pointer length is bounded by the same depth and actual
field/index names already visited; one pointer concatenation per retained edge
adds O(total retained path characters), with no new Cartesian or recursive axis.
Transient sibling pointers are discarded with the stack. The two focused graph
controls each have at most4 objects,8 edges and depth4, below8KiB additional data.
They cover an acyclic shared array, a separate-equal-array control and an actual
self-cycle. Existing exact scalar/native save/rename and native fileID controls
remain in the focused run. Public inspection and the isolated CLI scalar write
must be replayed after adoption; the failed creation is not reclassified as a
write or a successful test. Admission budget PASS for this fixed owner change.
