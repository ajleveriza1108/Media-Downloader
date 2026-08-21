# MediaDock R1.6.17 development snapshot

Status: **development / installer-runtime verification pending**.

Current development package: **R1.6.17 — Trial Queue Workflow Repair**.

## Cumulative behavior

R1.6.17 retains the existing downloader/converter foundation and adds the current queue/trial workflow:

- trial remains exactly 5 successful video outputs + 5 successful MP3 outputs;
- Stream and Convert are disabled while unlicensed;
- playlist paste automatically loads accepted entries directly into Download Queue;
- trial-mode playlist admission is limited by the remaining allowance for the selected MP4/MP3 format;
- playlist-limit messaging explains accepted/skipped items;
- playlist and queue thumbnail discovery is repaired;
- queue pagination is removed in favor of full-height scrolling;
- Select All, Clear Selection, Download All, Download Selected and Remove Selected are available;
- strict local trial persistence/hardening from R1.6.16 remains cumulative;
- Payhip purchase URL and **Release This Device** terminology remain cumulative.

## Generated package verification

- R1.6.17 static checks: **45/45**;
- exact R1.6.16.3 predecessor manifest: **48/48**;
- R1.6.17 managed source/assets manifest: **48/48**;
- R1.6.17 release manifest: **157/157**;
- package SHA-256: `f406b510f3f0b104ba96b58cc82e649094d5e77f3faac0e708fd70d08a52a40b`.

## Publication boundary

This publication updates public development/status documentation only.

It does **not** publish:
- current commercial source;
- installer binaries;
- developer trial-reset utilities;
- license-key inventories;
- Google Apps Script or Payhip secrets;
- customer/device activation data;
- logs, cookies, runtime state, caches, or downloaded tools.

R1.6.17 must not be called stable until the guarded Windows installer build/install/runtime tests pass on the target Windows machine.
