# ARCManagement

Portable graph management over ARCBaseModel and PolyglotSQLite.
`ARCManagement.Session` owns a repository; `ManagedGraph` provides mutation and
history, and `DatasetYaml` provides detached codecs. Workspace-specific
`ARCtrl.ARC` lives in the nested `ARCtrl` folder in this same assembly.

See the [management guide](../../docs/project/arc-management.md) and
[workspace guide](../../docs/project/arctrl.md) for usage and verification.
