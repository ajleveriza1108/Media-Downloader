(() => {
  "use strict";

  // Runs in the page's MAIN world. It observes only page-initiated fetch/XHR
  // responses and navigation changes, then forwards small metadata messages to
  // the isolated MediaDock content script through window.postMessage.
  const FLAG = "__MEDIADOCK_PAGE_MEDIA_HOOK_R1656__";
  if (window[FLAG]) return;
  Object.defineProperty(window, FLAG, { value: true, configurable: false });

  const CHANNEL = "MEDIADOCK_PAGE_MEDIA_R1656";
  const MAX_URL_LENGTH = 16384;
  const MEDIA_EXTENSIONS = new Set([
    "m3u8","mpd","mp4","m4v","webm","mov","mkv","flv","m4a","mp3","aac","ogg","opus","wav"
  ]);
  const SEGMENT_EXTENSIONS = new Set(["m4s","cmfv","cmfa"]);

  function extensionOf(value) {
    try {
      const u = new URL(String(value || ""), location.href);
      const leaf = u.pathname.split("/").pop() || "";
      const m = leaf.toLowerCase().match(/\.([a-z0-9]{1,8})$/);
      return m ? m[1] : "";
    } catch {
      return "";
    }
  }

  function isInteresting(url, mimeType) {
    if (typeof url !== "string" || !url || url.length > MAX_URL_LENGTH) return false;
    if (!/^https?:/i.test(url)) return false;
    const mime = String(mimeType || "").toLowerCase().split(";", 1)[0].trim();
    if (mime.startsWith("video/") || mime.startsWith("audio/")) return true;
    if (mime.includes("mpegurl") || mime.includes("dash+xml")) return true;
    const ext = extensionOf(url);
    if (SEGMENT_EXTENSIONS.has(ext)) return false;
    return MEDIA_EXTENSIONS.has(ext);
  }

  function post(type, payload = {}) {
    try {
      window.postMessage({
        source: CHANNEL,
        type,
        pageUrl: location.href,
        title: document.title || "",
        ...payload
      }, "*");
    } catch {
      // Detection must never disturb the page.
    }
  }

  function reportResponse(url, mimeType, contentLength, transport) {
    const absolute = (() => {
      try { return new URL(String(url || ""), location.href).href; } catch { return ""; }
    })();
    if (!isInteresting(absolute, mimeType)) return;
    const length = Number(contentLength || 0);
    post("candidate", {
      url: absolute,
      mimeType: String(mimeType || "").slice(0, 160),
      contentLength: Number.isFinite(length) && length > 0 ? length : 0,
      transport: String(transport || "page").slice(0, 32)
    });
  }

  // Fetch wrapper: preserve the original Response object and only inspect headers.
  try {
    const originalFetch = window.fetch;
    if (typeof originalFetch === "function") {
      window.fetch = async function (...args) {
        const response = await Reflect.apply(originalFetch, this, args);
        try {
          reportResponse(
            response?.url || (typeof args[0] === "string" ? args[0] : args[0]?.url),
            response?.headers?.get?.("content-type") || "",
            response?.headers?.get?.("content-length") || 0,
            "fetch"
          );
        } catch {}
        return response;
      };
    }
  } catch {}

  // XHR wrapper: report only after the request completes and headers are readable.
  try {
    const originalOpen = XMLHttpRequest.prototype.open;
    const requestUrls = new WeakMap();
    XMLHttpRequest.prototype.open = function (method, url, ...rest) {
      try { requestUrls.set(this, String(url || "")); } catch {}
      return Reflect.apply(originalOpen, this, [method, url, ...rest]);
    };
    const originalSend = XMLHttpRequest.prototype.send;
    XMLHttpRequest.prototype.send = function (...args) {
      try {
        this.addEventListener("loadend", () => {
          try {
            reportResponse(
              this.responseURL || requestUrls.get(this) || "",
              this.getResponseHeader("content-type") || "",
              this.getResponseHeader("content-length") || 0,
              "xhr"
            );
          } catch {}
        }, { once: true });
      } catch {}
      return Reflect.apply(originalSend, this, args);
    };
  } catch {}

  // SPA navigation must clear candidates from the previous logical page.
  function navigationChanged() {
    post("navigation", { url: location.href });
  }

  try {
    for (const method of ["pushState", "replaceState"]) {
      const original = history[method];
      if (typeof original !== "function") continue;
      history[method] = function (...args) {
        const result = Reflect.apply(original, this, args);
        queueMicrotask(navigationChanged);
        return result;
      };
    }
    addEventListener("popstate", navigationChanged, { passive: true });
    addEventListener("hashchange", navigationChanged, { passive: true });
  } catch {}
})();
