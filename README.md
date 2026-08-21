# MediaDock

Windows desktop video/audio downloader and converter built with .NET 10 and WPF.

## Current development track

**R1.6.19 — Dual-Mode Updater + Trial Build Candidate**

The unlicensed edition remains exactly **5 successful Video + 5 successful MP3 outputs**.

Customers may purchase MediaDock or enter/activate their license key at any
time during the trial; trial exhaustion is not required before licensing.

R1.6.19 adds dual update behavior:

- installed MediaDock uses the verified installer EXE;
- portable MediaDock uses a verified portable ZIP and updates its current portable folder;
- both update paths consume only the stable manifest from ajleveriza1108/MediaDock-Release;
- development metadata cannot trigger customer updates;
- both artifacts are SHA-256 verified;
- portable updates preserve the external hardened trial state.

The stable pointer remains inactive until the Windows installed and portable update cycle is verified.

## Public repository policy

Current commercial trial/licensing/updater implementation remains private. This repository contains sanitized public project/status material only.