# MediaDock

Windows desktop video/audio downloader, converter, and media-stream workspace built with .NET 10 and WPF.

## Current development track

The active MediaDock development track is **R1.6.15 — Internal Media Capture Streaming**.

R1.6.15 adds and retains:

- Download and Convert workspaces;
- Stream workspace with internal WebView2 webpage/media detection;
- network-response detection for direct media plus HLS (`.m3u8`) and DASH (`.mpd`) candidates;
- live webpage `<video>` isolation for supported dynamic players;
- resolution selection when combined non-DRM direct formats are exposed;
- Stream Play / Pause / Stop / Volume / Full Screen controls;
- Stream-side MP4 and MP3 download actions through the normal queue;
- drag-a-link-anywhere ingestion;
- TXT/CSV batch link import with duplicate skipping;
- persistent queue and settings;
- strict unlicensed trial accounting of 5 successful video outputs + 5 successful MP3 outputs.

## Verification status

**R1.6.15 is a development snapshot, not a stable release.**

The generated R1.6.15 package passed 62/62 static checks, its R1.6.14 predecessor manifest verified 43/43, its R1.6.15 managed-source manifest verified 47/47, and its release manifest verified 142/142.

Target-Windows verification is still required: Windows PowerShell 5.1 parser preflight, NuGet restore, .NET build/publish, WebView2 runtime loading, real webpage media capture, resolution playback, full-screen interaction, and Stream MP4/MP3 interaction.

## Public-repository policy

This repository contains an older historical source snapshot and public development documentation. The current commercial MediaDock source is **not being published here while licensing is being prepared**. Publishing the current trial/licensing client implementation to a public repository would make commercial enforcement easier to remove.

A private source repository will be used for the current commercial source once it is available. This public repository remains the public project/status location.

No installer, downloaded tools, build output, logs, cookies, local state, license keys, customer/device activation data, or private configuration are committed here.

## Media access boundary

MediaDock is intended for public or otherwise authorized media. It does not bypass DRM, paywalls, authentication, subscriptions, or protected access. Page-level UI may be hidden in the Stream workspace after a video is isolated, but server-side/in-stream advertisements are not guaranteed to be removable.
