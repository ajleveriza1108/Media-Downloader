# MediaDock Stable Updater Contract v2

Stable manifest:

https://raw.githubusercontent.com/ajleveriza1108/MediaDock-Release/main/latest-stable.json

A stable release is accepted only when:

1. schemaVersion is 2;
2. product is MediaDock;
3. channel is stable;
4. stable=true;
5. windowsRuntimeVerified=true;
6. installerPublished=true;
7. portablePublished=true;
8. updaterPublished=true;
9. version is newer than the installed build;
10. installerUrl is an HTTPS .exe under:
    https://github.com/ajleveriza1108/MediaDock-Release/releases/download/;
11. portableUrl is an HTTPS .zip under the same release path;
12. both SHA-256 values are valid and independently verified.

Installed mode selects the installer artifact.
Portable mode selects the portable artifact.

latest-development.json never triggers automatic customer updates.

The stable pointer is moved last.