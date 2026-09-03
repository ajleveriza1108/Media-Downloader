(() => {
  "use strict";

  // MEDIADOCK_ADJUSTABLE_FLOATING_R1656
  // Detection runs in every frame so embedded players are visible to MediaDock.
  // Only the top frame renders the movable/resizable grabber UI.

  const IS_TOP = window.top === window;
  const CHANNEL = "MEDIADOCK_PAGE_MEDIA_R1656";
  const LAYOUT_KEY = "mediadockGrabberLayoutR1656";
  const MIN_REPORT_INTERVAL_MS = 900;
  const MEDIA_EXTENSIONS = new Set([
    "m3u8","mpd","mp4","m4v","webm","mov","mkv","flv","m4a","mp3","aac","ogg","opus","wav"
  ]);
  const SEGMENT_EXTENSIONS = new Set(["m4s","cmfv","cmfa"]);

  let candidates = [];
  let host = null;
  let shadow = null;
  let panel = null;
  let list = null;
  let status = null;
  let grab = null;
  let count = null;
  let reportTimer = 0;
  let lastReportAt = 0;
  let lastMediaSignature = "";
  let dragMoved = false;
  let panelOpen = false;
  let saveLayoutTimer = 0;
  let layout = { x: 20, y: 12, width: 560, height: 360 };

  function extensionOf(value) {
    try {
      const u = new URL(String(value || ""), location.href);
      const leaf = u.pathname.split("/").pop() || "";
      const match = leaf.toLowerCase().match(/\.([a-z0-9]{1,8})$/);
      return match ? match[1] : "";
    } catch {
      return "";
    }
  }

  function isHttpMediaUrl(url, mimeType = "") {
    if (typeof url !== "string" || !/^https?:/i.test(url) || url.length > 16384) return false;
    const ext = extensionOf(url);
    if (SEGMENT_EXTENSIONS.has(ext)) return false;
    const mime = String(mimeType || "").toLowerCase();
    return MEDIA_EXTENSIONS.has(ext) || mime.startsWith("video/") || mime.startsWith("audio/") || mime.includes("mpegurl") || mime.includes("dash+xml");
  }

  function inferMime(url) {
    switch (extensionOf(url)) {
      case "m3u8": return "application/vnd.apple.mpegurl";
      case "mpd": return "application/dash+xml";
      case "mp4": case "m4v": case "mov": return "video/mp4";
      case "webm": return "video/webm";
      case "m4a": return "audio/mp4";
      case "mp3": return "audio/mpeg";
      case "aac": return "audio/aac";
      case "ogg": return "audio/ogg";
      case "opus": return "audio/opus";
      case "wav": return "audio/wav";
      default: return "";
    }
  }

  function formatBytes(value) {
    const n = Number(value);
    if (!Number.isFinite(n) || n <= 0) return "";
    const units = ["B", "KB", "MB", "GB", "TB"];
    let size = n, index = 0;
    while (size >= 1024 && index < units.length - 1) {
      size /= 1024;
      index++;
    }
    return `${size >= 100 || index === 0 ? size.toFixed(0) : size.toFixed(1)} ${units[index]}`;
  }

  function formatDuration(seconds) {
    const total = Math.floor(Number(seconds || 0));
    if (!Number.isFinite(total) || total <= 0) return "";
    const hours = Math.floor(total / 3600);
    const minutes = Math.floor((total % 3600) / 60);
    const secs = total % 60;
    return hours > 0
      ? `${hours}:${String(minutes).padStart(2, "0")}:${String(secs).padStart(2, "0")}`
      : `${minutes}:${String(secs).padStart(2, "0")}`;
  }

  function typeLabel(item) {
    const kind = String(item?.candidateKind || "").toLowerCase();
    const ext = String(item?.ext || extensionOf(item?.url) || "").toUpperCase();
    if (kind === "hls-variant" || kind === "hls") return "HLS";
    if (kind === "dash") return "DASH";
    if (kind === "page") return "PAGE";
    return ext || (String(item?.mimeType || "").startsWith("audio/") ? "AUDIO" : "VIDEO");
  }

  function qualitySummary(item) {
    const parts = [];
    if (item?.quality) parts.push(String(item.quality));
    else if (Number(item?.height || 0) > 0) parts.push(`${Math.round(Number(item.height))}p`);
    if (Number(item?.bitrateKbps || 0) > 0) parts.push(`${Math.round(Number(item.bitrateKbps))} kbps`);
    if (item?.codecs) parts.push(String(item.codecs).split(",", 1)[0]);
    return parts.join(" · ");
  }

  function clampLayout() {
    const badgeWidth = host?.offsetWidth || 220;
    const badgeHeight = host?.offsetHeight || 30;
    layout.x = Math.max(4, Math.min(Number(layout.x) || 20, Math.max(4, innerWidth - badgeWidth - 4)));
    layout.y = Math.max(4, Math.min(Number(layout.y) || 12, Math.max(4, innerHeight - badgeHeight - 4)));
    layout.width = Math.max(360, Math.min(Number(layout.width) || 560, Math.max(360, innerWidth - 24)));
    layout.height = Math.max(190, Math.min(Number(layout.height) || 360, Math.max(190, innerHeight - 70)));
  }

  function applyLayout() {
    if (!host) return;
    clampLayout();
    host.style.left = `${Math.round(layout.x)}px`;
    host.style.top = `${Math.round(layout.y)}px`;
    if (panel) {
      panel.style.width = `${Math.round(layout.width)}px`;
      panel.style.height = `${Math.round(layout.height)}px`;
      const opensUp = layout.y > innerHeight / 2;
      const opensLeft = layout.x + layout.width > innerWidth - 8;
      panel.style.top = opensUp ? "auto" : "34px";
      panel.style.bottom = opensUp ? "34px" : "auto";
      panel.style.left = opensLeft ? "auto" : "0";
      panel.style.right = opensLeft ? "0" : "auto";
    }
  }

  function scheduleLayoutSave() {
    clearTimeout(saveLayoutTimer);
    saveLayoutTimer = setTimeout(() => {
      if (!IS_TOP) return;
      chrome.storage.local.set({ [LAYOUT_KEY]: layout }).catch(() => {});
    }, 250);
  }

  async function loadLayout() {
    if (!IS_TOP) return;
    try {
      const stored = await chrome.storage.local.get(LAYOUT_KEY);
      const value = stored?.[LAYOUT_KEY];
      if (value && typeof value === "object") {
        layout = {
          x: Number(value.x) || 20,
          y: Number(value.y) || 12,
          width: Number(value.width) || 560,
          height: Number(value.height) || 360
        };
      }
    } catch {}
    applyLayout();
  }

  function beginDrag(event, source) {
    if (!IS_TOP || event.button !== 0 || !host) return;
    if (source === "grab" && event.target?.closest?.("button")) return;
    const startX = event.clientX;
    const startY = event.clientY;
    const originX = layout.x;
    const originY = layout.y;
    dragMoved = false;
    event.preventDefault();
    source?.setPointerCapture?.(event.pointerId);

    const move = moveEvent => {
      const dx = moveEvent.clientX - startX;
      const dy = moveEvent.clientY - startY;
      if (Math.abs(dx) + Math.abs(dy) > 4) dragMoved = true;
      layout.x = originX + dx;
      layout.y = originY + dy;
      applyLayout();
    };
    const up = () => {
      source?.removeEventListener?.("pointermove", move);
      source?.removeEventListener?.("pointerup", up);
      source?.removeEventListener?.("pointercancel", up);
      scheduleLayoutSave();
    };
    source?.addEventListener?.("pointermove", move);
    source?.addEventListener?.("pointerup", up);
    source?.addEventListener?.("pointercancel", up);
  }

  function build() {
    if (!IS_TOP || host || !document.documentElement) return;
    host = document.createElement("div");
    host.id = "mediadock-floating-host";
    host.style.cssText = "all:initial;position:fixed;left:20px;top:12px;z-index:2147483647;display:none";
    shadow = host.attachShadow({ mode: "closed" });

    const style = document.createElement("style");
    style.textContent = `
      *{box-sizing:border-box;font-family:Segoe UI,Arial,sans-serif}
      button{font:inherit}
      .grab{display:flex;align-items:center;height:30px;border:1px solid #77838b;border-radius:5px;background:#eef3f6;color:#12212a;box-shadow:0 2px 9px rgba(0,0,0,.34);font-size:12px;font-weight:650;cursor:move;overflow:hidden;user-select:none;touch-action:none}
      .play{width:28px;height:100%;display:grid;place-items:center;background:#23a34a;color:#fff;font-size:13px;text-shadow:0 1px 1px rgba(0,0,0,.3)}
      .label{padding:0 9px;color:#e23a20;background:#eef3f6;white-space:nowrap;cursor:pointer}
      .count{min-width:27px;padding:0 7px;border-left:1px solid #c7d0d6;color:#33444f;text-align:center;cursor:pointer}
      .panel{position:absolute;top:34px;left:0;width:560px;height:360px;min-width:360px;min-height:190px;max-width:calc(100vw - 24px);max-height:calc(100vh - 70px);overflow:hidden;background:#242424;color:#f4f4f4;border:1px solid #5f5f5f;border-radius:5px;box-shadow:0 9px 28px rgba(0,0,0,.48);display:none;resize:both}
      .panelHead{height:34px;display:grid;grid-template-columns:minmax(0,1fr) auto;align-items:center;padding:0 7px 0 11px;background:#2c2c2c;border-bottom:1px solid #444;cursor:move;user-select:none;touch-action:none}
      .panelTitle{font-size:12px;font-weight:650;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.tools{display:flex;gap:4px}
      .iconBtn{width:25px;height:24px;border:0;border-radius:4px;background:transparent;color:#ddd;cursor:pointer}.iconBtn:hover{background:#444}
      .all{height:35px;display:flex;align-items:center;padding:0 12px;border-bottom:1px solid #414141;font-size:12px;cursor:pointer;color:#fff}.all:hover,.row:hover{background:#343434}
      .scroll{height:calc(100% - 69px);overflow:auto;overscroll-behavior:contain}
      .row{display:grid;grid-template-columns:30px minmax(0,1fr) auto;gap:8px;padding:8px 10px;align-items:start;cursor:pointer;border-bottom:1px solid #303030}
      .num{text-align:right;color:#cfd3d5;padding-top:1px}.main{min-width:0}.title{white-space:nowrap;overflow:hidden;text-overflow:ellipsis;font-size:12px;color:#fff}
      .meta{margin-top:3px;color:#bac2c6;font-size:10.5px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.type{color:#f2f2f2;font-size:10.5px;white-space:nowrap;padding:2px 6px;border:1px solid #565656;border-radius:3px}
      .empty{padding:14px;color:#bfc5c8;font-size:11px}.status{position:absolute;left:8px;right:8px;bottom:8px;padding:7px 9px;background:rgba(23,39,29,.96);border:1px solid #386b49;border-radius:4px;color:#b9e7c4;font-size:10.5px;display:none;z-index:3}
    `;
    shadow.appendChild(style);

    const wrap = document.createElement("div");
    wrap.innerHTML = `
      <div class="grab" id="grab" title="Drag to move MediaDock">
        <span class="play">▶</span><span class="label" id="label">Download this video</span><span class="count" id="count">1</span>
      </div>
      <div class="panel" id="panel">
        <div class="panelHead" id="panelHead"><div class="panelTitle">MediaDock · detected media</div><div class="tools"><button class="iconBtn" id="close" title="Close">×</button></div></div>
        <div class="all" id="all">Download all detected media</div>
        <div class="scroll" id="scroll"><div id="list"></div></div>
        <div class="status" id="status"></div>
      </div>`;
    shadow.appendChild(wrap);

    grab = shadow.getElementById("grab");
    panel = shadow.getElementById("panel");
    list = shadow.getElementById("list");
    status = shadow.getElementById("status");
    count = shadow.getElementById("count");
    const label = shadow.getElementById("label");
    const panelHead = shadow.getElementById("panelHead");

    grab.addEventListener("pointerdown", event => beginDrag(event, grab));
    panelHead.addEventListener("pointerdown", event => {
      if (event.target?.closest?.("button")) return;
      beginDrag(event, panelHead);
    });

    const toggle = event => {
      event.stopPropagation();
      if (dragMoved) {
        dragMoved = false;
        return;
      }
      panelOpen = !panelOpen;
      panel.style.display = panelOpen ? "block" : "none";
      applyLayout();
    };
    label.addEventListener("click", toggle);
    count.addEventListener("click", toggle);
    shadow.getElementById("close").addEventListener("click", event => {
      event.stopPropagation();
      panelOpen = false;
      panel.style.display = "none";
    });

    shadow.getElementById("all").addEventListener("click", async () => {
      const downloadable = candidates.filter(item => !item?.isPageFallback || candidates.every(x => x?.isPageFallback));
      if (!downloadable.length) return;
      setStatus(`Sending ${downloadable.length} item(s) to MediaDock…`);
      try {
        const response = await chrome.runtime.sendMessage({ type: "md-send-many", items: downloadable });
        setStatus(response?.ok ? `Sent ${response.queued ?? response.sent ?? 0} item(s) to MediaDock.` : (response?.error || "MediaDock handler failed."));
      } catch (error) {
        setStatus(error?.message || "MediaDock handler failed.");
      }
    });

    const resizeObserver = new ResizeObserver(() => {
      if (!panelOpen) return;
      const rect = panel.getBoundingClientRect();
      if (rect.width <= 0 || rect.height <= 0) return;
      layout.width = rect.width;
      layout.height = rect.height;
      scheduleLayoutSave();
    });
    resizeObserver.observe(panel);

    document.documentElement.appendChild(host);
    loadLayout().catch(() => {});
  }

  function setStatus(text) {
    if (!status) return;
    status.textContent = text;
    status.style.display = "block";
    clearTimeout(setStatus.timer);
    setStatus.timer = setTimeout(() => {
      if (status) status.style.display = "none";
    }, 3800);
  }

  async function send(item) {
    setStatus("Sending media to MediaDock…");
    try {
      const response = await chrome.runtime.sendMessage({
        type: "md-send",
        item,
        mode: "download",
        kind: item?.handlerKind || "file"
      });
      setStatus(response?.ok ? "Sent to MediaDock." : (response?.error || "MediaDock handler failed."));
    } catch (error) {
      setStatus(error?.message || "MediaDock handler failed.");
    }
  }

  function render(next) {
    if (!IS_TOP) return;
    candidates = Array.isArray(next) ? next : [];
    build();
    if (!host || !list) return;

    host.style.display = candidates.length ? "block" : "none";
    if (!candidates.length) {
      panelOpen = false;
      panel.style.display = "none";
      return;
    }

    count.textContent = String(candidates.length);
    shadow.getElementById("label").textContent = candidates.some(item => String(item?.mimeType || "").startsWith("audio/")) && !candidates.some(item => !String(item?.mimeType || "").startsWith("audio/"))
      ? "Download this audio"
      : "Download this video";

    list.textContent = "";
    candidates.forEach((item, index) => {
      const row = document.createElement("div");
      row.className = "row";
      const num = document.createElement("div");
      num.className = "num";
      num.textContent = `${index + 1}.`;

      const main = document.createElement("div");
      main.className = "main";
      const title = document.createElement("div");
      title.className = "title";
      title.textContent = String(item?.title || item?.fileName || item?.url || "Detected media");
      const meta = document.createElement("div");
      meta.className = "meta";
      const details = [
        qualitySummary(item),
        formatBytes(item?.contentLength),
        formatDuration(item?.durationSeconds),
        item?.isPageFallback ? "Complete page analysis" : ""
      ].filter(Boolean);
      meta.textContent = details.join(" · ") || String(item?.mimeType || "Detected stream");
      main.append(title, meta);

      const type = document.createElement("div");
      type.className = "type";
      type.textContent = typeLabel(item);

      row.append(num, main, type);
      row.title = String(item?.url || "");
      row.addEventListener("click", () => send(item));
      list.appendChild(row);
    });
    applyLayout();
  }

  async function reportCandidate(raw) {
    const url = String(raw?.url || "");
    const mimeType = String(raw?.mimeType || inferMime(url) || "");
    if (!isHttpMediaUrl(url, mimeType)) return;
    try {
      await chrome.runtime.sendMessage({
        type: "md-media-candidate",
        candidate: {
          url,
          title: document.title || "Detected media",
          mimeType,
          referrer: location.href,
          contentLength: Number(raw?.contentLength || 0) || 0,
          quality: String(raw?.quality || ""),
          bitrateKbps: Number(raw?.bitrateKbps || 0) || 0,
          width: Number(raw?.width || 0) || 0,
          height: Number(raw?.height || 0) || 0,
          durationSeconds: Number(raw?.durationSeconds || 0) || 0,
          codecs: String(raw?.codecs || ""),
          source: String(raw?.source || "content-detection").slice(0, 128)
        }
      });
    } catch {}
  }

  function reportMediaElement(element) {
    if (!(element instanceof HTMLMediaElement)) return;
    const urls = new Set();
    const current = String(element.currentSrc || element.src || "");
    if (current && !current.startsWith("blob:")) urls.add(current);
    for (const source of element.querySelectorAll?.("source[src]") || []) {
      const value = String(source.src || source.getAttribute("src") || "");
      if (value && !value.startsWith("blob:")) urls.add(value);
    }
    for (const url of urls) {
      const sourceNode = [...(element.querySelectorAll?.("source[src]") || [])].find(node => String(node.src || "") === url);
      reportCandidate({
        url,
        mimeType: sourceNode?.type || element.getAttribute("type") || inferMime(url) || (element instanceof HTMLVideoElement ? "video/unknown" : "audio/unknown"),
        quality: sourceNode?.getAttribute?.("label") || sourceNode?.dataset?.quality || "",
        width: element instanceof HTMLVideoElement ? element.videoWidth : 0,
        height: element instanceof HTMLVideoElement ? element.videoHeight : 0,
        durationSeconds: Number.isFinite(element.duration) ? element.duration : 0,
        source: "dom-media-element"
      });
    }
  }

  async function reportPageMedia() {
    reportTimer = 0;
    const now = Date.now();
    if (now - lastReportAt < MIN_REPORT_INTERVAL_MS) {
      scheduleMediaReport(MIN_REPORT_INTERVAL_MS - (now - lastReportAt));
      return;
    }
    lastReportAt = now;

    const media = [...document.querySelectorAll("video,audio")];
    if (!media.length) return;
    for (const element of media) reportMediaElement(element);

    const signature = `${location.href}\n${document.title}\n${media.length}\n${media.map(item => item.currentSrc || item.src || "").join("\n")}`;
    if (signature === lastMediaSignature) return;
    lastMediaSignature = signature;
    try {
      await chrome.runtime.sendMessage({
        type: "md-page-media-seen",
        pageUrl: location.href,
        title: document.title,
        hasVideo: media.some(item => item instanceof HTMLVideoElement),
        hasAudio: media.some(item => item instanceof HTMLAudioElement)
      });
    } catch {}
  }

  function scheduleMediaReport(delay = 180) {
    if (reportTimer) return;
    reportTimer = setTimeout(reportPageMedia, Math.max(0, delay));
  }

  function scanExistingPerformanceResources() {
    try {
      for (const entry of performance.getEntriesByType("resource")) {
        if (isHttpMediaUrl(entry?.name || "")) {
          reportCandidate({
            url: entry.name,
            contentLength: Number(entry.transferSize || entry.encodedBodySize || 0) || 0,
            source: `performance-${entry.initiatorType || "resource"}`
          });
        }
      }
    } catch {}
  }

  function installPerformanceObserver() {
    try {
      const observer = new PerformanceObserver(listEntries => {
        for (const entry of listEntries.getEntries()) {
          if (!isHttpMediaUrl(entry?.name || "")) continue;
          reportCandidate({
            url: entry.name,
            contentLength: Number(entry.transferSize || entry.encodedBodySize || 0) || 0,
            source: `performance-${entry.initiatorType || "resource"}`
          });
        }
      });
      observer.observe({ type: "resource", buffered: true });
    } catch {}
  }

  function onPageHookMessage(event) {
    if (event.source !== window || event.data?.source !== CHANNEL) return;
    if (event.data.type === "candidate") {
      reportCandidate({
        url: event.data.url,
        mimeType: event.data.mimeType,
        contentLength: event.data.contentLength,
        source: `page-${event.data.transport || "request"}`
      });
      return;
    }
    if (event.data.type === "navigation") {
      lastMediaSignature = "";
      if (IS_TOP) {
        chrome.runtime.sendMessage({ type: "md-clear-candidates" }).catch(() => {});
      }
      setTimeout(() => {
        scanExistingPerformanceResources();
        scheduleMediaReport(250);
      }, 250);
    }
  }

  function onMediaEvent(event) {
    if (event?.target instanceof HTMLMediaElement) {
      reportMediaElement(event.target);
      scheduleMediaReport(120);
    }
  }

  chrome.runtime.onMessage.addListener(message => {
    if (IS_TOP && message?.type === "md-candidates") render(message.candidates);
  });

  async function boot() {
    if (IS_TOP) {
      build();
      try {
        const response = await chrome.runtime.sendMessage({ type: "md-get-candidates" });
        if (response?.ok) render(response.candidates);
      } catch {}
      addEventListener("resize", () => {
        applyLayout();
        scheduleLayoutSave();
      }, { passive: true });
    }

    addEventListener("message", onPageHookMessage, false);
    document.addEventListener("loadedmetadata", onMediaEvent, true);
    document.addEventListener("durationchange", onMediaEvent, true);
    document.addEventListener("play", onMediaEvent, true);
    addEventListener("pageshow", () => scheduleMediaReport(250), { passive: true });
    document.addEventListener("visibilitychange", () => {
      if (!document.hidden) {
        scanExistingPerformanceResources();
        scheduleMediaReport(250);
      }
    }, { passive: true });

    installPerformanceObserver();
    scanExistingPerformanceResources();
    scheduleMediaReport(0);
  }

  boot().catch(() => {});
})();
