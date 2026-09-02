const HOST = "com.ajcoder.mediadock";
const PROTOCOL_VERSION = 2;
const MAX_URL_LENGTH = 16384;
const MAX_CANDIDATES_PER_TAB = 12;
const AUTO_INTERCEPT_DEFAULT = false;

const FILE_EXTENSIONS = new Set([
  "3g2","3gp","7z","aac","ac3","ace","aif","aiff","amr","ape","apk","appx","appxbundle","arj","asf","avi",
  "azw","azw3","bin","bmp","br","bz2","cab","cbr","cbz","ckpt","csv","deb","djvu","dmg","doc","docm","docx",
  "epub","exe","fb2","flac","flv","ggml","gguf","gif","gz","gzip","heic","ico","img","iso","jar","jpeg","jpg",
  "json","lz","lz4","lzh","lzma","m2ts","m2v","m4a","m4b","m4v","mid","midi","mka","mkv","mobi","mov","mp3",
  "mp4","mpa","mpd","mpe","mpeg","mpg","msi","msix","msixbundle","msu","mts","odp","ods","odt","oga","ogg","ogv",
  "onnx","opus","ova","ovf","pdf","pkg","plj","png","pps","ppsx","ppt","pptm","pptx","psd","pt","pth","qcow","qcow2",
  "qt","ra","rar","rm","rmvb","rpm","r00","r01","r10","r11","rtf","safetensors","sea","sit","sitx","snap","sql","svg",
  "tar","tbz","tbz2","tgz","tif","tiff","torrent","ts","ttf","txt","txz","vdi","vhd","vhdx","vmdk","vob","wav","webm",
  "webp","wim","wma","wmv","woff","woff2","xls","xlsb","xlsm","xlsx","xml","xpi","xz","z","zip","zipx","zst",
  "ass","srt","vtt"
]);

const recentIntercepts = new Map();
const candidatesByTab = new Map();

function isHttpUrl(value) {
  if (typeof value !== "string" || value.length === 0 || value.length > MAX_URL_LENGTH) return false;
  try {
    const u = new URL(value);
    return u.protocol === "http:" || u.protocol === "https:";
  } catch { return false; }
}

function isSupportedUrl(value) {
  if (isHttpUrl(value)) return true;
  return typeof value === "string" && value.length <= MAX_URL_LENGTH && /^magnet:\?xt=urn:bt/i.test(value.trim());
}

function getExtension(value) {
  if (!value || typeof value !== "string") return "";
  try {
    const path = isHttpUrl(value) ? new URL(value).pathname : value;
    const leaf = path.split(/[\\/]/).pop() || "";
    const match = leaf.toLowerCase().match(/\.([a-z0-9]{1,16})$/);
    return match ? match[1] : "";
  } catch { return ""; }
}

function isSupportedExtension(ext) {
  return FILE_EXTENSIONS.has(ext) || /^r\d{2,3}$/.test(ext);
}

function cleanFilename(value) {
  if (typeof value !== "string") return "";
  return value.split(/[\\/]/).pop().slice(0, 240);
}

function looksLikeDownloadMime(mime) {
  const v = String(mime || "").toLowerCase();
  if (!v) return false;
  return v.startsWith("video/") || v.startsWith("audio/") ||
    v === "application/pdf" || v === "application/zip" ||
    v === "application/x-7z-compressed" || v === "application/x-rar-compressed" ||
    v === "application/octet-stream" || v.includes("application/vnd.android.package-archive");
}

function normalizeCandidate(raw) {
  const url = raw?.url || raw?.finalUrl || "";
  if (!isSupportedUrl(url)) return null;
  const fileName = cleanFilename(raw?.fileName || raw?.filename || "");
  const ext = getExtension(fileName) || getExtension(url);
  const mimeType = String(raw?.mimeType || raw?.mime || "").slice(0, 160);
  const contentLength = Number(raw?.contentLength ?? raw?.totalBytes ?? raw?.fileSize ?? 0) || 0;
  const candidateKind = String(raw?.candidateKind || "").slice(0, 32);
  const handlerKind = raw?.handlerKind === "page" ? "page" : "file";
  return {
    url,
    title: String(raw?.title || fileName || "Download").slice(0, 512),
    fileName,
    mimeType,
    referrer: isHttpUrl(raw?.referrer || "") ? raw.referrer : "",
    contentLength,
    ext,
    quality: "",
    handlerKind,
    candidateKind,
    isPageFallback: !!raw?.isPageFallback,
    source: String(raw?.source || "chromium").slice(0, 128)
  };
}

async function sendToMediaDock(raw, mode = "download", kind = "file") {
  const c = normalizeCandidate(raw);
  if (!c) throw new Error("MediaDock accepts http, https, or magnet URLs.");
  return await chrome.runtime.sendNativeMessage(HOST, {
    version: PROTOCOL_VERSION,
    action: "send",
    mode: mode === "analyze" ? "analyze" : "download",
    kind: kind === "page" ? "page" : "file",
    url: c.url,
    title: c.title,
    fileName: c.fileName,
    mimeType: c.mimeType,
    referrer: c.referrer,
    contentLength: c.contentLength,
    source: c.source
  });
}

function createMenus() {
  chrome.contextMenus.removeAll(() => {
    chrome.contextMenus.create({ id: "md-page", title: "Download page with MediaDock", contexts: ["page"] });
    chrome.contextMenus.create({ id: "md-link", title: "Download link/file with MediaDock", contexts: ["link"] });
    chrome.contextMenus.create({ id: "md-media", title: "Download media with MediaDock", contexts: ["video","audio"] });
    chrome.contextMenus.create({ id: "md-analyze", title: "Analyze page in MediaDock", contexts: ["page"] });
  });
}

chrome.runtime.onInstalled.addListener(async () => {
  createMenus();
  // R1.8 safety reset: an R1.7 profile may have automatic interception enabled.
  // Start the performance-safe release idle; the user can re-enable interception from the popup.
  await chrome.storage.local.set({ autoIntercept: AUTO_INTERCEPT_DEFAULT });
});
chrome.runtime.onStartup.addListener(createMenus);

chrome.contextMenus.onClicked.addListener(async (info, tab) => {
  let raw = { url: info.pageUrl || tab?.url || "", title: tab?.title || "", source: "context-page" };
  let mode = "download", kind = "page";
  if (info.menuItemId === "md-link") {
    raw = { url: info.linkUrl || "", title: info.selectionText || tab?.title || "", source: "context-link", referrer: info.pageUrl || "" };
    kind = "file";
  } else if (info.menuItemId === "md-media") {
    raw = { url: info.srcUrl || info.pageUrl || tab?.url || "", title: tab?.title || "", source: "context-media", referrer: info.pageUrl || "" };
    kind = "file";
  } else if (info.menuItemId === "md-analyze") {
    mode = "analyze";
  }
  try { await sendToMediaDock(raw, mode, kind); } catch (e) { console.error("MediaDock context handler failed", e); }
});

chrome.commands.onCommand.addListener(async command => {
  if (command !== "send-to-mediadock") return;
  const [tab] = await chrome.tabs.query({active: true, currentWindow: true});
  if (!tab?.url) return;
  try { await sendToMediaDock({url: tab.url, title: tab.title || "", source: "keyboard"}, "download", "page"); }
  catch (e) { console.error("MediaDock keyboard handler failed", e); }
});

function shouldAutoInterceptDownload(item) {
  if (!item || (item.byExtensionId && item.byExtensionId !== chrome.runtime.id)) return false;
  const url = item.finalUrl || item.url || "";
  if (!isHttpUrl(url)) return false;
  const fileName = cleanFilename(item.filename || "");
  const ext = getExtension(fileName) || getExtension(url);
  return isSupportedExtension(ext) || looksLikeDownloadMime(item.mime);
}

chrome.downloads.onCreated.addListener(async item => {
  try {
    const {autoIntercept} = await chrome.storage.local.get({autoIntercept: AUTO_INTERCEPT_DEFAULT});
    if (!autoIntercept || !shouldAutoInterceptDownload(item)) return;
    const key = `${item.id}:${item.finalUrl || item.url}`;
    if (recentIntercepts.has(key)) return;
    recentIntercepts.set(key, Date.now());
    setTimeout(() => recentIntercepts.delete(key), 30000);

    const response = await sendToMediaDock({
      url: item.finalUrl || item.url,
      fileName: item.filename || "",
      mimeType: item.mime || "",
      referrer: item.referrer || "",
      contentLength: item.fileSize > 0 ? item.fileSize : item.totalBytes,
      title: cleanFilename(item.filename || "") || "Browser download",
      source: "browser-download-intercept"
    }, "download", "file");
    if (!response?.ok) throw new Error(response?.error || "MediaDock did not accept the download.");
    await chrome.downloads.cancel(item.id);
    try { await chrome.downloads.erase({id: item.id}); } catch {}
  } catch (e) { console.error("MediaDock automatic browser download interception failed", e); }
});

function pageFallbackCandidate(message, sender) {
  const url = message?.pageUrl || sender?.tab?.url || "";
  if (!isHttpUrl(url) || (!message?.hasVideo && !message?.hasAudio)) return null;
  return normalizeCandidate({
    url,
    title: String(message?.title || sender?.tab?.title || (message?.hasVideo ? "Video page" : "Audio page")),
    mimeType: message?.hasVideo ? "video/page" : "audio/page",
    handlerKind: "page",
    candidateKind: "page",
    isPageFallback: true,
    source: "page-media-fallback"
  });
}

function candidateKey(candidate) {
  return `page:${String(candidate?.url || "").split("#")[0]}`;
}

function addCandidate(tabId, candidate) {
  if (!candidate || tabId < 0) return;
  const list = candidatesByTab.get(tabId) || [];
  const key = candidateKey(candidate);
  const index = list.findIndex(x => candidateKey(x) === key);
  if (index >= 0) list[index] = candidate; else list.unshift(candidate);
  if (list.length > MAX_CANDIDATES_PER_TAB) list.length = MAX_CANDIDATES_PER_TAB;
  candidatesByTab.set(tabId, list);
  chrome.tabs.sendMessage(tabId, {type: "md-candidates", candidates: list}).catch(() => {});
}

chrome.tabs.onRemoved.addListener(tabId => candidatesByTab.delete(tabId));
chrome.tabs.onUpdated.addListener((tabId, changeInfo) => {
  if (changeInfo.status === "loading" || changeInfo.url) candidatesByTab.delete(tabId);
});

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message?.type === "md-get-candidates") {
    sendResponse({ok: true, candidates: candidatesByTab.get(sender.tab?.id) || []});
    return false;
  }
  if (message?.type === "md-page-media-seen") {
    if (sender.tab?.id >= 0) addCandidate(sender.tab.id, pageFallbackCandidate(message, sender));
    sendResponse({ok: true});
    return false;
  }
  if (message?.type === "md-send") {
    sendToMediaDock(message.item || {url: message.url, title: message.title || "", source: message.source || "popup"},
      message.mode || "download", message.kind || "file")
      .then(r => sendResponse(r)).catch(e => sendResponse({ok:false,error:e?.message || String(e)}));
    return true;
  }
  if (message?.type === "md-send-many") {
    (async () => {
      const incoming = Array.isArray(message.items) ? message.items.slice(0, 20) : [];
      const unique = [], seen = new Set();
      for (const item of incoming) {
        const normalized = normalizeCandidate(item);
        if (!normalized) continue;
        const key = candidateKey(normalized);
        if (seen.has(key)) continue;
        seen.add(key); unique.push(normalized);
      }
      let sent = 0;
      for (const item of unique) {
        const result = await sendToMediaDock({...item, source: "floating-media-grabber-batch"}, "download", item.handlerKind || "page");
        if (result?.ok) sent++;
        await new Promise(resolve => setTimeout(resolve, 180));
      }
      return {ok:true, sent, queued:sent};
    })().then(sendResponse).catch(e => sendResponse({ok:false,error:e?.message || String(e)}));
    return true;
  }
  if (message?.type === "md-get-settings") {
    chrome.storage.local.get({autoIntercept: AUTO_INTERCEPT_DEFAULT}).then(sendResponse);
    return true;
  }
  if (message?.type === "md-set-auto-intercept") {
    chrome.storage.local.set({autoIntercept: !!message.value}).then(() => sendResponse({ok:true}));
    return true;
  }
  return false;
});
