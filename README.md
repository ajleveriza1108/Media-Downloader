# MediaDock

Windows desktop video/audio downloader and converter built with .NET 10 and WPF.

## Current development track

**R1.6.18 — Release-Repo Updater + Trial Build Candidate**

The unlicensed build remains a strict **5 successful Video + 5 successful MP3** trial. Stream and Convert remain unavailable while unlicensed.

R1.6.18 adds the customer-safe updater contract. Commercial builds check only the stable pointer in jleveriza1108/MediaDock-Release. Development metadata cannot trigger customer updates.

Downloaded installers must come from the MediaDock-Release GitHub Releases path and must pass the SHA-256 in the stable manifest before launch.

## Verification status

The R1.6.18 Windows candidate has passed restore/build/publish, engine/updater self-test, GUI startup smoke, and Inno Setup compilation. Installed trial/update-cycle verification and stable artifact publication remain gated.

## Public-repository policy

This repository contains public project/status material and historical source only. Current commercial trial/licensing enforcement source is intentionally withheld.

Do not commit private licensing source, trial reset tools, keys, secrets, customer/device records, downloaded tools, build output, logs, cookies, local state, or caches.

## Media access boundary

MediaDock is intended for public or otherwise authorized media. It does not bypass DRM, paywalls, authentication, subscriptions, or protected/private access.