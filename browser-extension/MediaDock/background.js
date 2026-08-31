const HOST = "com.ajcoder.mediadock";
const PROTOCOL_VERSION = 2;
const MAX_URL_LENGTH = 16384;
const MAX_CANDIDATES_PER_TAB = 30;
const AUTO_INTERCEPT_DEFAULT = true;

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

const MEDIA_EXTENSIONS = new Set([
  "3g2","3gp","aac","ac3","aif","aiff","amr","ape","asf","avi","flac","flv","m2ts","m2v","m3u8","m4a",
  "m4b","m4v","mid","midi","mka","mkv","mov","mp3","mp4","mpa","mpd","mpe","mpeg","mpg","mts","oga","ogg",
  "ogv","opus","qt","ra","rm","rmvb","ts","vob","wav","webm","wma","wmv"
]);

const NOISY_SEGMENT_EXTENSIONS = new Set(["m4s","cmfv","cmfa"]);
const recentIntercepts = new Map();
const candidatesByTab = new Map();

function isHttpUrl(value) {
  if (typeof value !== "string" || value.length === 0 || value.length > MAX_URL_LENGTH) return false;
  try {
    const u = new URL(value);
    return u.protocol === "http:" || u.protocol === "https:";
  } catch {
    return false;
  }
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
    if (!match) return "";
    const ext = match[1];
    if (/^r\d{2,3}$/.test(ext)) return ext;
    return ext;
  } catch {
    return "";
  }
}

function isSupportedExtension(ext) {
  return FILE_EXTENSIONS.has(ext) || /^r\d{2,3}$/.test(ext);
}

function headerValue(headers, name) {
  if (!Array.isArray(headers)) return "";
  const hit = headers.find(h => String(h.name || "").toLowerCase() === name.toLowerCase());
  return typeof hit?.value === "string" ? hit.value : "";
}

function filenameFromDisposition(disposition) {
  if (!disposition) return "";
  const star = disposition.match(/filename\*\s*=\s*UTF-8''([^;]+)/i);
  if (star) {
    try { return decodeURIComponent(star[1].trim().replace(/^["']|["']$/g, "")); } catch {}
  }
  const plain = disposition.match(/filename\s*=\s*("?)([^";]+)\1/i);
  return plain ? plain[2].trim() : "";
}

function cleanFilename(value) {
  if (typeof value !== "string") return "";
  return value.split(/[\\/]/).pop().slice(0, 240);
}

function formatBytes(value) {
  const n = Number(value);
  if (!Number.isFinite(n) || n <= 0) return "";
  const units = ["B","KB","MB","GB","TB"];
  let size = n, i = 0;
  while (size >= 1024 && i < units.length - 1) { size /= 1024; i++; }
  return `${size >= 100 || i === 0 ? size.toFixed(0) : size.toFixed(1)} ${units[i]}`;
}

function inferQuality(url, filename) {
  const text = `${url || ""} ${filename || ""}`;
  const m = text.match(/(?:^|[^0-9])(2160|1440|1080|720|480|360|240|144)p(?:[^0-9]|$)/i);
  return m ? `${m[1]}p` : "";
}

function looksLikeMediaMime(mime) {
  const v = String(mime || "").toLowerCase();
  return v.startsWith("video/") || v.startsWith("audio/") ||
    v.includes("mpegurl") || v.includes("dash+xml") || v.includes("application/ogg");
}

function looksLikeDownloadMime(mime) {
  const v = String(mime || "").toLowerCase();
  if (!v) return false;
  return looksLikeMediaMime(v) ||
    v === "application/pdf" ||
    v === "application/zip" ||
    v === "application/x-7z-compressed" ||
    v === "application/x-rar-compressed" ||
    v === "application/octet-stream" ||
    v.includes("application/vnd.android.package-archive");
}

function normalizeCandidate(raw) {
  const url = raw?.url || raw?.finalUrl || "";
  if (!isSupportedUrl(url)) return null;
  const fileName = cleanFilename(raw?.fileName || raw?.filename || "");
  const ext = getExtension(fileName) || getExtension(url);
  const mimeType = String(raw?.mimeType || raw?.mime || "").slice(0, 160);
  const contentLength = Number(raw?.contentLength ?? raw?.totalBytes ?? raw?.fileSize ?? 0) || 0;
  return {
    url,
    title: String(raw?.title || fileName || "Download").slice(0, 512),
    fileName,
    mimeType,
    referrer: isHttpUrl(raw?.referrer || "") ? raw.referrer : "",
    contentLength,
    ext,
    quality: inferQuality(url, fileName),
    handlerKind: ext === "m3u8" || ext === "mpd" ? "page" : "file",
    source: String(raw?.source || "chromium").slice(0, 128)
  };
}

async function sendToMediaDock(raw, mode = "download", kind = "file") {
  const c = normalizeCandidate(raw);
  if (!c) throw new Error("MediaDock accepts http, https, or magnet URLs.");
  const payload = {
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
  };
  return await chrome.runtime.sendNativeMessage(HOST, payload);
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
  const current = await chrome.storage.local.get({ autoIntercept: AUTO_INTERCEPT_DEFAULT });
  if (typeof current.autoIntercept !== "boolean") {
    await chrome.storage.local.set({ autoIntercept: AUTO_INTERCEPT_DEFAULT });
  }
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
  try {
    await sendToMediaDock({url: tab.url, title: tab.title || "", source: "keyboard"}, "download", "page");
  } catch (e) { console.error("MediaDock keyboard handler failed", e); }
});

function shouldAutoInterceptDownload(item) {
  if (!item || item.byExtensionId && item.byExtensionId !== chrome.runtime.id) return false;
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

    const raw = {
      url: item.finalUrl || item.url,
      fileName: item.filename || "",
      mimeType: item.mime || "",
      referrer: item.referrer || "",
      contentLength: item.fileSize > 0 ? item.fileSize : item.totalBytes,
      title: cleanFilename(item.filename || "") || "Browser download",
      source: "browser-download-intercept"
    };

    const response = await sendToMediaDock(raw, "download", "file");
    if (!response?.ok) throw new Error(response?.error || "MediaDock did not accept the download.");
    await chrome.downloads.cancel(item.id);
    try { await chrome.downloads.erase({id: item.id}); } catch {}
  } catch (e) {
    console.error("MediaDock automatic browser download interception failed", e);
  }
});

function looksLikeNoisySegment(url, ext, contentLength) {
  if (NOISY_SEGMENT_EXTENSIONS.has(ext)) return true;
  const lower = String(url || "").toLowerCase();
  if (/\b(segment|seg|chunk|frag|fragment)[-_]?\d+\b/.test(lower)) return true;
  if (ext === "ts" && contentLength > 0 && contentLength < 1024 * 1024) return true;
  return false;
}

function candidateFromResponse(details) {
  if (details.tabId < 0 || !isHttpUrl(details.url)) return null;
  const mimeType = headerValue(details.responseHeaders, "content-type").split(";")[0].trim().toLowerCase();
  const disposition = headerValue(details.responseHeaders, "content-disposition");
  const contentLength = Number(headerValue(details.responseHeaders, "content-length")) || 0;
  const fileName = cleanFilename(filenameFromDisposition(disposition));
  const ext = getExtension(fileName) || getExtension(details.url);
  const media = MEDIA_EXTENSIONS.has(ext) || looksLikeMediaMime(mimeType);
  if (!media) return null;
  if (looksLikeNoisySegment(details.url, ext, contentLength)) return null;

  return normalizeCandidate({
    url: details.url,
    fileName,
    mimeType,
    contentLength,
    referrer: details.documentUrl || details.initiator || "",
    title: fileName || `${ext ? ext.toUpperCase() : "Media"} media`,
    source: "floating-media-grabber"
  });
}

function addCandidate(tabId, candidate) {
  if (!candidate) return;
  const list = candidatesByTab.get(tabId) || [];
  const oldIndex = list.findIndex(x => x.url === candidate.url);
  if (oldIndex >= 0) {
    list[oldIndex] = candidate;
  } else {
    list.unshift(candidate);
    if (list.length > MAX_CANDIDATES_PER_TAB) list.length = MAX_CANDIDATES_PER_TAB;
  }
  candidatesByTab.set(tabId, list);
  chrome.tabs.sendMessage(tabId, {type: "md-candidates", candidates: list}).catch(() => {});
}

chrome.webRequest.onHeadersReceived.addListener(
  details => {
    try { addCandidate(details.tabId, candidateFromResponse(details)); } catch (e) { console.debug("MediaDock response inspection skipped", e); }
  },
  {urls: ["http://*/*","https://*/*"], types: ["media","xmlhttprequest","other"]},
  ["responseHeaders"]
);

chrome.tabs.onRemoved.addListener(tabId => candidatesByTab.delete(tabId));
chrome.tabs.onUpdated.addListener((tabId, changeInfo) => {
  if (changeInfo.status === "loading") candidatesByTab.delete(tabId);
});

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message?.type === "md-get-candidates") {
    sendResponse({ok: true, candidates: candidatesByTab.get(sender.tab?.id) || []});
    return false;
  }

  if (message?.type === "md-send") {
    sendToMediaDock(message.item || {url: message.url, title: message.title || "", source: message.source || "popup"},
      message.mode || "download", message.kind || "file")
      .then(r => sendResponse(r))
      .catch(e => sendResponse({ok:false,error:e?.message || String(e)}));
    return true;
  }

  if (message?.type === "md-send-many") {
    (async () => {
      const items = Array.isArray(message.items) ? message.items.slice(0, 20) : [];
      let sent = 0;
      for (const item of items) {
        const result = await sendToMediaDock(item, "download", item?.handlerKind || "file");
        if (result?.ok) sent++;
        await new Promise(resolve => setTimeout(resolve, 120));
      }
      return {ok:true, sent};
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
