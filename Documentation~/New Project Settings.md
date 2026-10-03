# New project settings

After package adoption, VMFramework discovers its global setting files. Their
GeneralSetting fields must be configured before the Editor's GameTag and
automatic GamePrefab registration initialization can complete.

Discover `vm_pt_vmf_create_missing_general_settings` in a bounded catalog page,
then retrieve its exact schema. For a new project or newly declared settings type,
set `ensureGlobalSettings` to `true`. This delegates global asset creation and
Addressables registration to `GlobalSettingFileEditorManager`, then verifies each
new entry before binding GeneralSettings. An empty business request only binds
GeneralSettings in existing global files.
The facade's `expected_project_path` must identify the intended project.

The tool delegates creation and binding to
`GlobalSettingFile.AutoFindAndCreateSettings`. The configured GeneralSettings
folder comes from `EditorSetting`; it does not accept a second folder authority.
Existing references are retained. Missing fields reuse existing matching assets
or create framework defaults, run their normal Inspector initialization, and save
the global references. Synchronous imports and asset identity checks verify the
returned bindings. The reply reports which global and GeneralSetting assets were created.

The operation creates settings; it does not claim to run Editor initialization.
Afterward, use the official Automation refresh and clean compilation job to
complete domain reload, then inspect GeneralSettings and run the global
GamePrefab audit. Repeating the creation command should return no new assets and
retain the same binding GUIDs.

## Cost and validation

The preflight limits the operation to 64 global files and 256 GeneralSetting
fields. Each file delegates once to the framework creator and receives one
synchronous import. Each binding receives one identity readback; existing
GeneralSetting GUIDs are collected by one indexed type query. There are no
pairwise asset scans or reverse-reference scans in this tool. Optional global
creation adds one configured-path pass of at most 64 entries, one owner call,
and at most 64 Addressables entry readbacks. The created-path set uses an index
for these readbacks.

For the CarrotLand adoption witness, the frozen authoring input contains four
global files, 13 fields and 1,644 project meta records: four creator calls, 13
type-selected find/create operations, four synchronous imports and 13 binding
readbacks. The command must finish within the official main-thread invocation
budget; compilation and initialization remain separate durable operations.
