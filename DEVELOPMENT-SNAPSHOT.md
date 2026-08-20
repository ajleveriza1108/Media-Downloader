# R1.5.9.8.2 development snapshot

Status: Windows runtime verification pending.

R1.5.9.8.1 compiled successfully on the target Windows machine and was published as the initial source snapshot. A real single-video download then produced a WPF `XamlParseException` while the first queue card was being materialized.

The captured crash identified the exact source defect: `DownloadQueueItem.Quality` and `DownloadQueueItem.Format` are intentionally read-only display properties, but the queue template used bindings that allowed WPF to infer a write-capable mode.

R1.5.9.8.2:

- makes queue display bindings explicit `Mode=OneWay`;
- keeps `Quality` and `Format` immutable in the model;
- hardens queue title, thumbnail, progress, status, speed, collection, and count display bindings;
- extends the startup smoke path to insert and render a real queue item;
- preserves the R1.5.9.8.1 default-quality and download-runtime stability work.

This GitHub publication contains source and documentation only. It does not publish downloaded runtime tools, build output, installer ZIPs, logs, backups, cookies, crash evidence, or local state.

Do not mark R1.5.9.8.2 stable until the guarded Windows installer/runtime smoke and a real single-video download pass on the target machine.
