# MediaDock R1.6.18 development snapshot

Status: **Windows build/staged smoke verified; install/update-cycle verification pending**.

Current development package: **R1.6.18 — Release-Repo Updater + Trial Build Candidate**.

## Trial contract

The unlicensed trial remains exactly:

- 5 successful Video outputs;
- 5 successful MP3 outputs;
- Stream unavailable while unlicensed;
- Convert unavailable while unlicensed.

R1.6.18 does not embed a license or developer bypass in the customer build.

## Updater contract

The commercial R1.6.18 client checks only:

$StableManifestUrl

The updater accepts only:

- schema version 1;
- product MediaDock;
- channel stable;
- stable=true;
- Windows runtime verified;
- installer published;
- updater published;
- a newer version than the installed build;
- an HTTPS installer URL under jleveriza1108/MediaDock-Release/releases/download/;
- a valid 64-character SHA-256.

The installer is downloaded to Local AppData, SHA-256 verified locally, and only then can it launch after user consent.

Development metadata never triggers customer updates.

## Windows candidate verification

- .NET restore: PASS
- .NET Release build: PASS
- self-contained win-x64 publish: PASS
- engine + updater contract test: PASS
- GUI startup smoke: PASS
- Inno Setup trial installer compile: PASS
- staged EXE SHA-256: $exeSha
- candidate installer SHA-256: $installerSha
- portable ZIP SHA-256: $portableSha

## Publication boundary

This public repository still does **not** publish:

- current commercial source;
- TrialStateService/licensing enforcement internals;
- developer trial-reset utilities;
- license-key inventories;
- backend / Payhip / Google Apps Script secrets;
- customer/device activation data;
- logs, cookies, runtime state, build caches, or downloaded tools.

R1.6.18 is not stable until the installed trial/update cycle is verified and the exact installer is published to the release repository.