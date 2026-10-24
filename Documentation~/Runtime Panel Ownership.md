# Runtime panel ownership

The runtime-ui-panel open action and runtime-game-item-session panel binding use
the same native panel creation boundary. Unique panels are published by the
framework's UniqueUIPanelCreationInitializer; automation may open that published
instance, but cannot create a replacement before initialization. An unpublished
unique panel returns ui_panel_initialization_pending without renting or opening
anything. An unknown config returns ui_panel_not_found.

An explicit panelObjectID is exact: an absent or mismatching object fails instead
of selecting or creating another panel. Item-session binding rejects multiple
opened instances and reopens its selected existing instance without creating one.

Static Cost Ledger before executable writes: each admission reads one config and
one native unique-panel dictionary entry, O(1) time and one error allocation on
rejection. Existing opened-panel discovery keeps its previous O(P) scan; the new
ambiguity decision uses collection Count, O(1). No polling, delay, cache, scene
scan, runtime readiness copy or second panel registry. Tests allocate one hidden
GameObject/manager and one unique config, register/unregister that config and
destroy the object in finally. PASS for one bounded fixture and no saved assets.
