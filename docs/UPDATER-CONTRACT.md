# MediaDock Stable Updater Contract v3 - Installer Only

Stable manifest:

https://raw.githubusercontent.com/ajleveriza1108/MediaDock-Release/main/latest-stable.json

The installed application accepts a release only when all of these are true:

1. schemaVersion is 3.
2. product is MediaDock.
3. channel is stable.
4. stable is true.
5. windowsRuntimeVerified is true.
6. installerPublished is true.
7. updaterPublished is true.
8. version is newer than the installed version.
9. installerUrl is HTTPS.
10. installerUrl is under:
    https://github.com/ajleveriza1108/MediaDock-Release/releases/download/
11. installerUrl ends in .exe.
12. installerSha256 is a valid 64-character SHA-256.
13. The downloaded installer hashes to exactly installerSha256 before launch.

latest-development.json is informational only and can never trigger a customer update.

The stable pointer must be moved last, after the exact installer asset and its hash are independently verified.