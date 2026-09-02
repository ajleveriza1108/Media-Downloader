(() => {
  if (window.top !== window) return;

  // MEDIADOCK_PERSISTENT_FLOATING_R181: UI stays visible while detection stays event-driven.

  const MIN_REPORT_INTERVAL_MS = 1200;
  let candidates = [];
  let host, shadow, panel, list, status;
  let reportTimer = 0;
  let lastReportAt = 0;
  let lastMediaSignature = "";

  function formatBytes(value) {
    const n = Number(value);
    if (!Number.isFinite(n) || n <= 0) return "";
    const units = ["B","KB","MB","GB","TB"];
    let size = n, i = 0;
    while (size >= 1024 && i < units.length - 1) { size /= 1024; i++; }
    return `${size >= 100 || i === 0 ? size.toFixed(0) : size.toFixed(1)} ${units[i]}`;
  }

  function build() {
    if (host || !document.documentElement) return;
    host = document.createElement("div");
    host.id = "mediadock-floating-host";
    host.style.cssText = "all:initial;position:fixed;left:20px;top:10px;z-index:2147483647;display:block";
    shadow = host.attachShadow({mode:"closed"});
    const style = document.createElement("style");
    style.textContent = `
      *{box-sizing:border-box;font-family:Segoe UI,Arial,sans-serif}
      .grab{display:flex;align-items:center;height:28px;border:1px solid #62717b;border-radius:4px;background:#eef4f7;color:#10212c;box-shadow:0 2px 8px rgba(0,0,0,.32);font-size:12px;font-weight:650;cursor:pointer;overflow:hidden}
      .play{width:27px;height:100%;display:grid;place-items:center;background:#1a9d46;color:white;font-size:13px}
      .label{padding:0 8px;color:#d92b1f;background:#eef4f7;white-space:nowrap}.count{padding:0 7px;border-left:1px solid #c7d0d6;color:#33444f}
      .panel{position:absolute;top:31px;left:0;width:490px;max-height:390px;overflow:auto;background:#262626;color:#f4f4f4;border:1px solid #5d5d5d;border-radius:4px;box-shadow:0 8px 26px rgba(0,0,0,.45);display:none}
      .all{padding:10px 14px;border-bottom:1px solid #444;font-size:13px;cursor:pointer}.all:hover,.row:hover{background:#343434}
      .row{display:grid;grid-template-columns:28px minmax(0,1fr) auto;gap:7px;padding:7px 10px;align-items:start;cursor:pointer;border-bottom:1px solid #303030}
      .num{text-align:right;color:#d7d7d7}.main{min-width:0}.title{white-space:nowrap;overflow:hidden;text-overflow:ellipsis;font-size:12px}
      .meta{margin-top:2px;color:#bfc5c8;font-size:10.5px}.type{color:#fff;font-size:11px;white-space:nowrap}.empty{padding:12px 14px;color:#bfc5c8;font-size:11px}.status{padding:7px 10px;color:#a9d8b3;font-size:10.5px;display:none}
    `;
    shadow.appendChild(style);
    const wrap = document.createElement("div");
    wrap.innerHTML = `<div class="grab" id="grab"><span class="play">▶</span><span class="label">Download with MediaDock</span><span class="count" id="count">0</span></div><div class="panel" id="panel"><div class="all" id="all">Queue all detected media</div><div id="list"></div><div class="status" id="status"></div></div>`;
    shadow.appendChild(wrap);
    panel = shadow.getElementById("panel"); list = shadow.getElementById("list"); status = shadow.getElementById("status");
    shadow.getElementById("grab").addEventListener("click", () => panel.style.display = panel.style.display === "block" ? "none" : "block");
    shadow.getElementById("all").addEventListener("click", async () => {
      if (!candidates.length) return;
      setStatus(`Queueing ${candidates.length} item(s) in MediaDock…`);
      const response = await chrome.runtime.sendMessage({type:"md-send-many",items:candidates});
      setStatus(response?.ok ? `Queued ${response.queued ?? response.sent ?? 0} item(s).` : (response?.error || "MediaDock handler failed."));
    });
    document.documentElement.appendChild(host);
  }

  function setStatus(text) {
    if (!status) return;
    status.textContent = text; status.style.display = "block";
    clearTimeout(setStatus.timer);
    setStatus.timer = setTimeout(() => { if (status) status.style.display = "none"; }, 3500);
  }

  async function send(item) {
    setStatus("Sending complete media page to MediaDock…");
    const response = await chrome.runtime.sendMessage({type:"md-send",item,mode:"download",kind:item?.handlerKind || "page"});
    setStatus(response?.ok ? "Sent to MediaDock." : (response?.error || "MediaDock handler failed."));
  }

  function render(next) {
    candidates = Array.isArray(next) ? next : [];
    build();
    if (!host || !list) return;
    host.style.display = "block";
    shadow.getElementById("count").textContent = String(candidates.length);
    list.textContent = "";
    if (!candidates.length) {
      const empty = document.createElement("div");
      empty.className = "empty";
      empty.textContent = "No media detected yet. MediaDock is idle.";
      list.appendChild(empty);
      return;
    }
    candidates.forEach((item, i) => {
      const row = document.createElement("div"); row.className = "row";
      row.innerHTML = `<div class="num">${i+1}.</div><div class="main"><div class="title"></div><div class="meta"></div></div><div class="type">VIDEO PAGE</div>`;
      row.querySelector(".title").textContent = String(item.title || item.url || "Media page");
      row.querySelector(".meta").textContent = ["Complete page analysis", formatBytes(item.contentLength), item.mimeType || ""].filter(Boolean).join(" • ");
      row.addEventListener("click", () => send(item)); list.appendChild(row);
    });
  }

  async function reportPageMedia() {
    reportTimer = 0;
    const now = Date.now();
    if (now - lastReportAt < MIN_REPORT_INTERVAL_MS) {
      scheduleMediaReport(MIN_REPORT_INTERVAL_MS - (now - lastReportAt));
      return;
    }
    lastReportAt = now;

    const video = document.querySelector("video");
    const audio = document.querySelector("audio");
    if (!video && !audio) return;

    const signature = `${location.href}\n${document.title}\n${video ? 1 : 0}\n${audio ? 1 : 0}`;
    if (signature === lastMediaSignature) return;
    lastMediaSignature = signature;
    try {
      await chrome.runtime.sendMessage({
        type: "md-page-media-seen", pageUrl: location.href, title: document.title,
        hasVideo: !!video, hasAudio: !!audio
      });
    } catch {}
  }

  function scheduleMediaReport(delay = 250) {
    if (reportTimer) return;
    reportTimer = setTimeout(reportPageMedia, Math.max(0, delay));
  }

  chrome.runtime.onMessage.addListener(message => {
    if (message?.type === "md-candidates") render(message.candidates);
  });

  function onMediaEvent(event) {
    if (event?.target instanceof HTMLMediaElement) scheduleMediaReport(150);
  }

  function boot() {
    build();
    render([]);
    scheduleMediaReport(0);
    chrome.runtime.sendMessage({type:"md-get-candidates"}).then(r => { if (r?.ok) render(r.candidates); }).catch(() => {});
    document.addEventListener("loadedmetadata", onMediaEvent, true);
    document.addEventListener("play", onMediaEvent, true);
    window.addEventListener("pageshow", () => scheduleMediaReport(300), {passive:true});
    document.addEventListener("visibilitychange", () => { if (!document.hidden) scheduleMediaReport(300); }, {passive:true});
  }

  boot();
})();
