# MediaDock

Windows desktop video/audio downloader and converter built with .NET 10 and WPF.

## Current development track

The active MediaDock development track is **R1.6.17 — Trial Queue Workflow Repair**.

R1.6.17 retains the strict unlicensed allowance of **5 successful video outputs + 5 successful MP3 outputs** and focuses the trial experience on Download. Stream and Convert are disabled while unlicensed.

Current R1.6.17 behavior includes:

- single-link MP4 and MP3 download workflows;
- playlist paste that automatically loads eligible entries directly into Download Queue;
- trial-aware playlist admission based on the remaining 5-video / 5-MP3 allowance;
- a clear trial playlist-limit notice when a playlist exceeds remaining allowance;
- repaired playlist/queue thumbnail discovery and persistence;
- a full-height queue with scrolling instead of premature fixed pagination;
- Select All / Clear Selection / Download All / Download Selected / Remove Selected actions;
- persistent queue and settings;
- drag-a-link-anywhere ingestion;
- TXT/CSV batch link import with duplicate skipping;
- strict local trial persistence/hardening from the R1.6.16 lineage;
- the Payhip purchase link and customer-facing **Release This Device** licensing terminology.

## Verification status

**R1.6.17 remains a development snapshot, not a stable release.**

The generated R1.6.17 source/update package passed its static and exact-manifest gates, but the R1.6.17 production-style Windows installer/build/runtime cycle is the next stage and has not yet been marked complete.

## Public-repository policy

This public repository contains the historical source snapshot plus public development/status documentation. The current commercial MediaDock source is intentionally **not** published here while trial/licensing protection is being completed.

Developer trial-reset utilities, license-key inventories, customer/device activation data, Google Apps Script secrets, Payhip/backend secrets, downloaded tools, build output, logs, cookies, local state, caches, and private configuration must not be committed here.

## Media access boundary

MediaDock is intended for public or otherwise authorized media. It does not bypass DRM, paywalls, authentication, subscriptions, or protected/private access.
