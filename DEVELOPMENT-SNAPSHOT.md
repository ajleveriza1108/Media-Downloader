# MediaDock R1.6.28 development snapshot

Status: verified stable UI text, subtitle, and public-media reliability release.

Customer release:
- MediaDock-Setup-R1.6.28.exe
- SHA-256: ab829edfed70575fb50ba3eaffee31cb498319762e89bb0800a419bd35a6b17b

Acceptance gates:
- strict .NET build: 0 warnings / 0 errors
- discovery-based textbox contrast/visible-boundary smoke (one required main URL/input): PASS
- Download Subtitles anchored to the real DownloadCommand, rendered, command-wired, and inside fixed viewport: PASS
- exactly one Settings close control + actual Settings method-definition gate: PASS
- Download Subtitles control present and wired: PASS
- yt-dlp subtitle argument contract: PASS
- bundled Reddit / YouTube / Generic extractor surface: PASS
- Reddit post + v.redd.it + direct-media normalization self-tests: PASS
- rendered persisted queue/status visual text sanitizer self-test: PASS
- DownloadQueueItem schema-independence gate: PASS
- partial R1.6.28 source recovery classifier: PASS
- blank / whitespace-only source lines accepted by punctuation normalizer: PASS
- large embedded/data source lines preserved during punctuation normalization: PASS
- Download / Stream / Convert real-click navigation: PASS
- license persistence / local receipt: PASS
- future R1.6.29 updater receiving self-test: PASS
- exact GitHub Release installer download-back SHA: PASS

Commercial implementation remains private/local.