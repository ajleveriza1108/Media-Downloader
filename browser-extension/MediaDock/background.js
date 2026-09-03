"use strict";

const HOST = "com.ajcoder.mediadock";
const PROTOCOL_VERSION = 2;
const MAX_URL_LENGTH = 16384;
const MAX_CANDIDATES_PER_TAB = 30;
const AUTO_INTERCEPT_DEFAULT = false;
const SESSION_PREFIX = "mdCandidatesR1656:";
const HLS_FETCH_TIMEOUT_MS = 4500;
const HLS_EXPANSION_CACHE_MS = 5 * 60 * 1000;

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

const DIRECT_MEDIA_EXTENSIONS = new Set([
  "mp4","m4v","webm","mov","mkv","flv","m4a","mp3","aac","ogg","opus","wav"
]);
const MANIFEST_EXTENSIONS = new Set(["m3u8","mpd"]);
const SEGMENT_EXTENSIONS = new Set(["m4s","cmfv","cmfa"]);

const recentIntercepts = new Map();
const candidatesByTab = new Map();
const hlsExpansionCache = new Map();

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
    return match ? match[1] : "";
  } catch {
    return "";
  }
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

function isManifestMime(mime) {
  const v = String(mime || "").toLowerCase();
  return v.includes("mpegurl") || v.includes("dash+xml");
}

function inferQuality(rawQuality, url, height) {
  const supplied = String(rawQuality || "").trim();
  if (supplied) return supplied.slice(0, 48);
  const h = Number(height || 0);
  if (Number.isFinite(h) && h >= 144) return `${Math.round(h)}p`;
  const text = String(url || "");
  const p = text.match(/(?:^|[^0-9])(144|240|360|480|540|576|720|900|1080|1200|1440|1800|2160|2880|4320)p(?:[^0-9]|$)/i);
  if (p) return `${p[1]}p`;
  const res = text.match(/(?:^|[^0-9])(\d{3,5})x(\d{3,5})(?:[^0-9]|$)/i);
  if (res) {
    const parsedHeight = Number(res[2]);
    if (parsedHeight >= 144 && parsedHeight <= 4320) return `${parsedHeight}p`;
  }
  return "";
}

function inferBitrateKbps(rawBitrate, url) {
  const supplied = Number(rawBitrate || 0);
  if (Number.isFinite(supplied) && supplied > 0) return Math.round(supplied);
  const text = String(url || "");
  const named = text.match(/(?:bitrate|bandwidth|bw|br)[=_-](\d{3,9})/i);
  if (named) {
    const value = Number(named[1]);
    if (Number.isFinite(value) && value > 0) return value > 100000 ? Math.round(value / 1000) : Math.round(value);
  }
  const suffix = text.match(/(?:^|[^0-9])(\d{3,6})k(?:bps)?(?:[^a-z0-9]|$)/i);
  if (suffix) return Number(suffix[1]) || 0;
  return 0;
}

function classifyCandidate(url, mimeType, requestedKind) {
  const ext = getExtension(url);
  const mime = String(mimeType || "").toLowerCase();
  if (requestedKind === "page") return { candidateKind: "page", handlerKind: "page" };
  if (ext === "m3u8" || mime.includes("mpegurl")) return { candidateKind: "hls", handlerKind: "page" };
  if (ext === "mpd" || mime.includes("dash+xml")) return { candidateKind: "dash", handlerKind: "page" };
  if (DIRECT_MEDIA_EXTENSIONS.has(ext) || mime.startsWith("video/") || mime.startsWith("audio/")) {
    return { candidateKind: "direct", handlerKind: "file" };
  }
  return { candidateKind: String(requestedKind || "file").slice(0, 32), handlerKind: "file" };
}

function normalizeCandidate(raw) {
  const url = raw?.url || raw?.finalUrl || "";
  if (!isSupportedUrl(url)) return null;
  const fileName = cleanFilename(raw?.fileName || raw?.filename || "");
  const ext = getExtension(fileName) || getExtension(url);
  if (SEGMENT_EXTENSIONS.has(ext)) return null;
  const mimeType = String(raw?.mimeType || raw?.mime || "").slice(0, 160);
  const contentLength = Number(raw?.contentLength ?? raw?.totalBytes ?? raw?.fileSize ?? 0) || 0;
  const classification = classifyCandidate(url, mimeType, raw?.handlerKind === "page" ? "page" : raw?.candidateKind);
  const width = Math.max(0, Number(raw?.width || 0) || 0);
  const height = Math.max(0, Number(raw?.height || 0) || 0);
  const durationSeconds = Math.max(0, Number(raw?.durationSeconds || 0) || 0);
  const quality = inferQuality(raw?.quality, url, height);
  const bitrateKbps = inferBitrateKbps(raw?.bitrateKbps, url);
  return {
    url,
    title: String(raw?.title || fileName || "Detected media").slice(0, 512),
    fileName,
    mimeType,
    referrer: isHttpUrl(raw?.referrer || "") ? raw.referrer : "",
    contentLength,
    ext,
    quality,
    bitrateKbps,
    width,
    height,
    durationSeconds,
    codecs: String(raw?.codecs || "").slice(0, 160),
    handlerKind: raw?.handlerKind === "page" ? "page" : classification.handlerKind,
    candidateKind: String(raw?.candidateKind || classification.candidateKind).slice(0, 32),
    isPageFallback: !!raw?.isPageFallback,
    source: String(raw?.source || "chromium").slice(0, 128)
  };
}

function candidateKey(candidate) {
  const rawUrl = String(candidate?.url || "").split("#", 1)[0];
  let identityUrl = rawUrl;
  try {
    const parsed = new URL(rawUrl);
    for (const name of [
      "token","sig","signature","expires","exp","policy","key-pair-id",
      "hdntl","hmac","auth","auth_key","authkey","cache","cachebust","cb","_"
    ]) {
      parsed.searchParams.delete(name);
    }
    identityUrl = parsed.href;
  } catch {}
  return `${candidate?.candidateKind || candidate?.handlerKind || "media"}:${identityUrl}:${candidate?.quality || ""}:${candidate?.bitrateKbps || 0}`;
}

function candidateRank(candidate) {
  if (candidate?.isPageFallback) return 0;
  const quality = Number(String(candidate?.quality || "").replace(/[^0-9]/g, "")) || 0;
  const bitrate = Number(candidate?.bitrateKbps || 0) || 0;
  const kind = candidate?.candidateKind === "direct" ? 4 : candidate?.candidateKind === "hls-variant" ? 3 : candidate?.candidateKind === "hls" ? 2 : candidate?.candidateKind === "dash" ? 1 : 0;
  return kind * 1_000_000_000 + quality * 1_000_000 + bitrate;
}

function sortCandidates(list) {
  return [...list].sort((a, b) => candidateRank(b) - candidateRank(a));
}

function sessionKey(tabId) {
  return `${SESSION_PREFIX}${tabId}`;
}

async function loadCandidates(tabId) {
  if (!Number.isInteger(tabId) || tabId < 0) return [];
  if (candidatesByTab.has(tabId)) return candidatesByTab.get(tabId) || [];
  try {
    const key = sessionKey(tabId);
    const stored = await chrome.storage.session.get(key);
    const list = Array.isArray(stored?.[key]) ? stored[key].map(normalizeCandidate).filter(Boolean) : [];
    candidatesByTab.set(tabId, list);
    return list;
  } catch {
    return [];
  }
}

async function saveCandidates(tabId, list) {
  candidatesByTab.set(tabId, list);
  try {
    await chrome.storage.session.set({ [sessionKey(tabId)]: list });
  } catch {}
}

async function pushCandidatesToTab(tabId, list) {
  try {
    await chrome.tabs.sendMessage(tabId, { type: "md-candidates", candidates: sortCandidates(list) });
  } catch {}
}

async function addCandidate(tabId, rawCandidate) {
  const candidate = normalizeCandidate(rawCandidate);
  if (!candidate || !Number.isInteger(tabId) || tabId < 0) return;
  const list = [...await loadCandidates(tabId)];
  const key = candidateKey(candidate);
  const index = list.findIndex(item => candidateKey(item) === key);
  if (index >= 0) {
    list[index] = { ...list[index], ...candidate };
  } else {
    list.push(candidate);
  }
  const sorted = sortCandidates(list).slice(0, MAX_CANDIDATES_PER_TAB);
  await saveCandidates(tabId, sorted);
  await pushCandidatesToTab(tabId, sorted);
}

async function clearCandidates(tabId) {
  if (!Number.isInteger(tabId) || tabId < 0) return;
  candidatesByTab.delete(tabId);
  try { await chrome.storage.session.remove(sessionKey(tabId)); } catch {}
  await pushCandidatesToTab(tabId, []);
}

function parseAttributeList(text) {
  const result = Object.create(null);
  const pattern = /([A-Z0-9-]+)=("(?:[^"\\]|\\.)*"|[^,]*)/gi;
  for (const match of String(text || "").matchAll(pattern)) {
    let value = String(match[2] || "").trim();
    if (value.startsWith('"') && value.endsWith('"')) value = value.slice(1, -1);
    result[String(match[1] || "").toUpperCase()] = value;
  }
  return result;
}

async function fetchTextWithTimeout(url) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), HLS_FETCH_TIMEOUT_MS);
  try {
    const response = await fetch(url, {
      credentials: "include",
      cache: "no-store",
      redirect: "follow",
      signal: controller.signal,
      headers: { Accept: "application/vnd.apple.mpegurl,application/x-mpegURL,text/plain,*/*" }
    });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    const text = await response.text();
    if (text.length > 2_000_000) throw new Error("Manifest is unexpectedly large.");
    return text;
  } finally {
    clearTimeout(timer);
  }
}

async function expandHlsCandidate(candidate) {
  const key = String(candidate?.url || "");
  if (!key) return [];
  const cached = hlsExpansionCache.get(key);
  if (cached && Date.now() - cached.at < HLS_EXPANSION_CACHE_MS) return cached.items;

  let text = "";
  try {
    text = await fetchTextWithTimeout(key);
  } catch {
    hlsExpansionCache.set(key, { at: Date.now(), items: [] });
    return [];
  }
  if (!/^\s*#EXTM3U/m.test(text)) return [];

  const lines = text.replace(/\r/g, "").split("\n");
  const variants = [];
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i].trim();
    if (!line.startsWith("#EXT-X-STREAM-INF:")) continue;
    const attributes = parseAttributeList(line.slice("#EXT-X-STREAM-INF:".length));
    let uriLine = "";
    for (let j = i + 1; j < lines.length; j++) {
      const next = lines[j].trim();
      if (!next) continue;
      if (next.startsWith("#")) continue;
      uriLine = next;
      i = j;
      break;
    }
    if (!uriLine) continue;
    let variantUrl = "";
    try { variantUrl = new URL(uriLine, key).href; } catch { continue; }
    const resolution = String(attributes.RESOLUTION || "");
    const resolutionMatch = resolution.match(/^(\d+)x(\d+)$/i);
    const width = resolutionMatch ? Number(resolutionMatch[1]) : 0;
    const height = resolutionMatch ? Number(resolutionMatch[2]) : 0;
    const bandwidth = Number(attributes["AVERAGE-BANDWIDTH"] || attributes.BANDWIDTH || 0) || 0;
    variants.push(normalizeCandidate({
      ...candidate,
      url: variantUrl,
      mimeType: "application/vnd.apple.mpegurl",
      width,
      height,
      quality: height > 0 ? `${height}p` : candidate.quality,
      bitrateKbps: bandwidth > 0 ? Math.round(bandwidth / 1000) : candidate.bitrateKbps,
      codecs: attributes.CODECS || candidate.codecs,
      candidateKind: "hls-variant",
      handlerKind: "page",
      source: "hls-master-variant"
    }));
  }
  const unique = [];
  const seen = new Set();
  for (const variant of variants.filter(Boolean)) {
    const variantKey = candidateKey(variant);
    if (seen.has(variantKey)) continue;
    seen.add(variantKey);
    unique.push(variant);
  }
  hlsExpansionCache.set(key, { at: Date.now(), items: unique });
  return unique;
}

async function processDetectedCandidate(tabId, rawCandidate, sender) {
  const candidate = normalizeCandidate({
    ...rawCandidate,
    title: rawCandidate?.title || sender?.tab?.title || "Detected media",
    referrer: rawCandidate?.referrer || sender?.tab?.url || ""
  });
  if (!candidate) return;

  if (candidate.candidateKind === "hls" || candidate.ext === "m3u8" || isManifestMime(candidate.mimeType) && !String(candidate.mimeType).toLowerCase().includes("dash")) {
    const variants = await expandHlsCandidate(candidate);
    if (variants.length) {
      for (const variant of variants) await addCandidate(tabId, variant);
      return;
    }
  }
  await addCandidate(tabId, candidate);
}

async function sendToMediaDock(raw, mode = "download", kind = "file") {
  const c = normalizeCandidate(raw);
  if (!c) throw new Error("MediaDock accepts http, https, or magnet URLs.");
  return await chrome.runtime.sendNativeMessage(HOST, {
    version: PROTOCOL_VERSION,
    action: "send",
    mode: mode === "analyze" ? "analyze" : "download",
    kind: kind === "page" ? "page" : c.handlerKind,
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
    chrome.contextMenus.create({ id: "md-media", title: "Download media with MediaDock", contexts: ["video", "audio"] });
    chrome.contextMenus.create({ id: "md-analyze", title: "Analyze page in MediaDock", contexts: ["page"] });
  });
}

chrome.runtime.onInstalled.addListener(async () => {
  createMenus();
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
  try { await sendToMediaDock(raw, mode, kind); } catch (error) { console.error("MediaDock context handler failed", error); }
});

chrome.commands.onCommand.addListener(async command => {
  if (command !== "send-to-mediadock") return;
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  if (!tab?.url) return;
  try { await sendToMediaDock({ url: tab.url, title: tab.title || "", source: "keyboard" }, "download", "page"); }
  catch (error) { console.error("MediaDock keyboard handler failed", error); }
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
    const { autoIntercept } = await chrome.storage.local.get({ autoIntercept: AUTO_INTERCEPT_DEFAULT });
    if (!autoIntercept || !shouldAutoInterceptDownload(item)) return;
    const key = `${item.id}:${item.finalUrl || item.url}`;
    if (recentIntercepts.has(key)) return;
    recentIntercepts.set(key, Date.now());
    setTimeout(() => recentIntercepts.delete(key), 30_000);

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
    try { await chrome.downloads.erase({ id: item.id }); } catch {}
  } catch (error) {
    console.error("MediaDock automatic browser download interception failed", error);
  }
});

function pageFallbackCandidate(message, sender) {
  const url = sender?.tab?.url || message?.pageUrl || "";
  if (!isHttpUrl(url) || (!message?.hasVideo && !message?.hasAudio)) return null;
  return normalizeCandidate({
    url,
    title: String(sender?.tab?.title || message?.title || (message?.hasVideo ? "Video page" : "Audio page")),
    mimeType: message?.hasVideo ? "video/page" : "audio/page",
    handlerKind: "page",
    candidateKind: "page",
    isPageFallback: true,
    source: "page-media-fallback"
  });
}

chrome.tabs.onRemoved.addListener(tabId => {
  clearCandidates(tabId).catch(() => {});
});
chrome.tabs.onUpdated.addListener((tabId, changeInfo) => {
  if (changeInfo.status === "loading" || changeInfo.url) clearCandidates(tabId).catch(() => {});
});

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message?.type === "md-get-candidates") {
    loadCandidates(sender.tab?.id).then(list => sendResponse({ ok: true, candidates: sortCandidates(list) }))
      .catch(error => sendResponse({ ok: false, error: error?.message || String(error) }));
    return true;
  }

  if (message?.type === "md-clear-candidates") {
    clearCandidates(sender.tab?.id).then(() => sendResponse({ ok: true }))
      .catch(error => sendResponse({ ok: false, error: error?.message || String(error) }));
    return true;
  }

  if (message?.type === "md-page-media-seen") {
    (async () => {
      const fallback = pageFallbackCandidate(message, sender);
      if (fallback) await addCandidate(sender.tab?.id, fallback);
      return { ok: true };
    })().then(sendResponse).catch(error => sendResponse({ ok: false, error: error?.message || String(error) }));
    return true;
  }

  if (message?.type === "md-media-candidate") {
    processDetectedCandidate(sender.tab?.id, message.candidate || {}, sender)
      .then(() => sendResponse({ ok: true }))
      .catch(error => sendResponse({ ok: false, error: error?.message || String(error) }));
    return true;
  }

  if (message?.type === "md-send") {
    sendToMediaDock(
      message.item || { url: message.url, title: message.title || "", source: message.source || "popup" },
      message.mode || "download",
      message.kind || message.item?.handlerKind || "file"
    ).then(sendResponse).catch(error => sendResponse({ ok: false, error: error?.message || String(error) }));
    return true;
  }

  if (message?.type === "md-send-many") {
    (async () => {
      const incoming = Array.isArray(message.items) ? message.items.slice(0, MAX_CANDIDATES_PER_TAB) : [];
      const unique = [], seen = new Set();
      for (const item of incoming) {
        const normalized = normalizeCandidate(item);
        if (!normalized || normalized.isPageFallback && incoming.some(x => !x?.isPageFallback)) continue;
        const key = candidateKey(normalized);
        if (seen.has(key)) continue;
        seen.add(key);
        unique.push(normalized);
      }
      let sent = 0;
      for (const item of unique) {
        const result = await sendToMediaDock(
          { ...item, source: "floating-media-grabber-batch" },
          "download",
          item.handlerKind || "file"
        );
        if (result?.ok) sent++;
        await new Promise(resolve => setTimeout(resolve, 180));
      }
      return { ok: true, sent, queued: sent };
    })().then(sendResponse).catch(error => sendResponse({ ok: false, error: error?.message || String(error) }));
    return true;
  }

  if (message?.type === "md-get-settings") {
    chrome.storage.local.get({ autoIntercept: AUTO_INTERCEPT_DEFAULT }).then(sendResponse);
    return true;
  }
  if (message?.type === "md-set-auto-intercept") {
    chrome.storage.local.set({ autoIntercept: !!message.value }).then(() => sendResponse({ ok: true }));
    return true;
  }
  return false;
});
