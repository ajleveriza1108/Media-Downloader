# MediaDock R1.6.19 development snapshot

Status: **Windows candidate build and staged/portable smoke verified; installed update-cycle verification pending**.

Current development package: **R1.6.19 — Dual-Mode Updater + Trial Build Candidate**.

## Trial contract

The unlicensed trial remains exactly:

- 5 successful Video outputs;
- 5 successful MP3 outputs;
- Stream unavailable while unlicensed;
- Convert unavailable while unlicensed.

Users do **not** have to exhaust the trial before purchasing or activating.
The existing purchase action and license-key/activation UI remain available
while Video or MP3 trial credits are still unused.

Updating either edition must not reset the external hardened trial state.

## Early license access

R1.6.19 explicitly keeps purchase and license activation available before
trial exhaustion. A customer may buy a license or enter/activate a valid key
at any point during the 5+5 trial.

## Dual-mode updater

The private/commercial R1.6.19 client reads only:

https://raw.githubusercontent.com/ajleveriza1108/MediaDock-Release/main/latest-stable.json

Installed mode:
- selects the verified installer EXE.

Portable mode:
- is identified by MediaDock.portable;
- selects the verified portable ZIP;
- rechecks the portable ZIP SHA-256 in the helper;
- waits for the running portable app to exit;
- expands the new portable payload to a staging directory;
- runs engine/updater and GUI smoke tests against the staged portable payload;
- transactionally backs up the existing portable directory;
- overlays the new payload into the same portable folder;
- relaunches MediaDock;
- restores the backup if replacement fails.

The trial state is stored outside the portable application folder and is not cleared by the portable updater.

## Stable manifest schema

R1.6.19 uses stable updater schema version 2 with both:

- installerUrl + installerSha256
- portableUrl + portableSha256

The stable pointer remains inactive until both exact artifacts are hosted and the installed/portable update cycle is verified.

## Candidate hashes

- staged EXE SHA-256: c4be3920e1f44b02a2aba6142c3952e5bd3da38ef9a673930cb136d1e6ec8638
- installer candidate SHA-256: 075fbd0f032997dec4e3384aa5991b65bf7243f7a66a8584f49621180e109631
- portable candidate SHA-256: 2f635b4348c2865d706a403219d80bd31726f38f7bf2c3810e2b483b9aab8f1c

## Publication boundary

This public repository still does **not** publish:
- TrialStateService/licensing enforcement internals;
- UpdateService commercial source;
- developer trial-reset utilities;
- license-key inventories;
- backend / Payhip / Google Apps Script secrets;
- customer/device activation data;
- logs, cookies, runtime state, build caches, or downloaded tools.