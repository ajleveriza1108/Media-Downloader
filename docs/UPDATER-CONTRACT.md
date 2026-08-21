# MediaDock Stable Updater Contract v1

Stable manifest:

$StableManifestUrl

The installed application accepts a release only when all of these are true:

1. schemaVersion is 1.
2. product is MediaDock.
3. channel is stable.
4. stable, windowsRuntimeVerified, installerPublished, and updaterPublished are all 	rue.
5. ersion is newer than the installed version.
6. installerUrl is HTTPS and starts with:
   https://github.com/ajleveriza1108/MediaDock-Release/releases/download/
7. installerSha256 is a valid SHA-256.
8. The downloaded installer independently hashes to that exact SHA-256.

The stable pointer is moved **last**, after the installer artifact and checksum are verified.

latest-development.json never triggers automatic customer updates.