# GamePrefab construction review static cost ledger

The existing global validation freezes at most 10,000 wrappers/config entries
and 5,000 returned issues. For each non-null entry the construction check reads
one GameItemType and performs constant-count CLR type/constructor metadata
queries. It creates no GameItems and invokes no constructors. IPrefabProvider
configs retain the existing native Prefab contract; null GameItemType denotes a
configuration without a runtime item and requires no construction.

The complete scan has at most 10,000 additional checks, O(N) time and O(1)
additional live memory excluding the existing bounded issues. The Editor main
thread owns the read-only scan; no cache or lifetime survives the call. Seven
focused construction/type cases plus the existing four validation cases cover
abstract/open/interface/invalid/ctor-only rejection and concrete/config-only/
Prefab-backed acceptance without instantiation. Budget: pass.
