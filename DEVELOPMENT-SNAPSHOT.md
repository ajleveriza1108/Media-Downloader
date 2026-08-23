# MediaDock R1.6.31 development snapshot

Status: guarded cumulative public-safe development source snapshot.

Carried forward:
- R1.6.29 Queue Workspace.
- R1.6.30 persisted output-folder controls and Queue folder shortcut.
- licensed entitlement as the operational source of truth.
- strict unlicensed 5 Video + 5 MP3 successful-output trial.
- up to 5 simultaneous independent queue downloads.
- original-audio / English-aware YouTube multi-audio handling.

R1.6.31:
- queue rows use compact icon actions with tooltips.
- Delete Selected / Completed / All and row Delete explicitly ask whether downloaded files should also be removed from disk.
- Download Ready disables when the queue is empty or MediaDock is busy.
- queue columns prioritize Checkbox, Thumbnail, Title/Video Source, then format/quality/progress/status/actions.

Private/commercial TrialStateService, LicenseService, LicenseEntitlementState, UpdateService, backend details, credentials, serial inventories, customer state, and installer implementation are not published as source.