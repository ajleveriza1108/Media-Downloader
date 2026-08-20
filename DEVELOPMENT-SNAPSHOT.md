# R1.5.9.8.1 development snapshot

Status: Windows verification pending.

This snapshot contains the compile/BAT-encoding repair layered over the R1.5.9.8 download-runtime stability work.

The prior R1.5.9.8 local installer transaction failed during compilation because `ProcessRunner.cs` referenced `StreamReader` without importing `System.IO`. The transaction rolled back successfully. R1.5.9.8.1 adds the missing import and uses a BOM-free BAT launcher.

This GitHub publication intentionally contains source and documentation only. It does not publish downloaded runtime tools, build output, installer ZIPs, logs, backups, or crash evidence.
