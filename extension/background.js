const API_ROOT = "http://127.0.0.1:18765";
const SCREENSHOT_MIN_INTERVAL_MS = 600;
const SCREENSHOT_RETRY_DELAY_MS = 250;

let screenshotQueue = Promise.resolve();
let lastScreenshotAt = 0;

async function postHeartbeat() {
  try {
    const response = await fetch(`${API_ROOT}/api/extension-heartbeat`, {
      method: "POST"
    });
    const health = response.ok ? await response.json() : null;
    if (health?.isRecording && !health?.isPaused) {
      await chrome.action.setBadgeBackgroundColor({ color: "#DC2626" });
      await chrome.action.setBadgeText({ text: "REC" });
      return;
    }

    if (health?.isPaused) {
      await chrome.action.setBadgeBackgroundColor({ color: "#F97316" });
      await chrome.action.setBadgeText({ text: "停" });
      return;
    }

    await chrome.action.setBadgeBackgroundColor({ color: "#64748B" });
    await chrome.action.setBadgeText({ text: "等" });
  } catch {
    await chrome.action.setBadgeBackgroundColor({ color: "#DC2626" });
    await chrome.action.setBadgeText({ text: "OFF" });
  }
}

async function postEvent(event) {
  try {
    const response = await fetch(`${API_ROOT}/api/events`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify(event)
    });

    if (response.status === 202) {
      await chrome.action.setBadgeBackgroundColor({ color: "#16A34A" });
      await chrome.action.setBadgeText({ text: "REC" });
      return;
    }

    if (response.status === 204) {
      await chrome.action.setBadgeBackgroundColor({ color: "#F97316" });
      await chrome.action.setBadgeText({ text: "等" });
      return;
    }

    await chrome.action.setBadgeBackgroundColor({ color: response.ok ? "#16A34A" : "#DC2626" });
    await chrome.action.setBadgeText({ text: response.ok ? "OK" : "!" });
  } catch (error) {
    await chrome.action.setBadgeBackgroundColor({ color: "#DC2626" });
    await chrome.action.setBadgeText({ text: "OFF" });
  }
}

async function postDownloadEvent(downloadItem, state) {
  const filename = downloadItem?.filename ?? "";
  const filenamePart = filename.split(/[\\/]/).pop() || filename || downloadItem?.finalUrl || downloadItem?.url || "下载文件";
  await postEvent({
    eventType: "download",
    capturedAtUtc: new Date().toISOString(),
    pageUrl: downloadItem?.finalUrl || downloadItem?.url || null,
    pageTitle: "浏览器下载",
    value: filename || downloadItem?.finalUrl || downloadItem?.url || null,
    element: {
      name: filenamePart,
      role: "download",
      tagName: "download",
      text: state === "complete" ? `下载完成：${filenamePart}` : `下载失败：${filenamePart}`,
      cssSelector: `download:${downloadItem?.id ?? "unknown"}`,
      alternateSelectors: []
    }
  });
}

function sleep(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

async function captureScreenshot(windowId) {
  screenshotQueue = screenshotQueue
    .catch(() => null)
    .then(() => captureScreenshotNow(windowId));

  return screenshotQueue;
}

async function captureScreenshotNow(windowId) {
  for (let attempt = 0; attempt < 3; attempt += 1) {
    const elapsed = Date.now() - lastScreenshotAt;
    if (elapsed < SCREENSHOT_MIN_INTERVAL_MS) {
      await sleep(SCREENSHOT_MIN_INTERVAL_MS - elapsed);
    }

    try {
      const screenshot = await chrome.tabs.captureVisibleTab(windowId, { format: "png" });
      lastScreenshotAt = Date.now();
      return screenshot;
    } catch {
      lastScreenshotAt = Date.now();
      await sleep(SCREENSHOT_RETRY_DELAY_MS);
    }
  }

  return null;
}

chrome.runtime.onMessage.addListener((message, sender) => {
  if (message?.type === "extension-heartbeat") {
    postHeartbeat();
    return;
  }

  if (message?.type !== "capture-event") {
    return;
  }

  (async () => {
    const screenshotDataUrl = sender.tab?.windowId !== undefined
      ? await captureScreenshot(sender.tab.windowId)
      : null;

    await postEvent({
      ...message.payload,
      screenshotDataUrl
    });
  })();
});

chrome.webNavigation.onCompleted.addListener(async (details) => {
  if (details.frameId !== 0 || !details.url.startsWith("http")) {
    return;
  }

  const tab = await chrome.tabs.get(details.tabId);
  const screenshotDataUrl = tab.windowId !== undefined
    ? await captureScreenshot(tab.windowId)
    : null;

  await postEvent({
    eventType: "navigation",
    capturedAtUtc: new Date().toISOString(),
    pageUrl: details.url,
    pageTitle: tab.title ?? details.url,
    screenshotDataUrl
  });
});

if (chrome.downloads?.onChanged) {
  chrome.downloads.onChanged.addListener(async (delta) => {
    const state = delta?.state?.current;
    if (state !== "complete" && state !== "interrupted") {
      return;
    }

    const items = await chrome.downloads.search({ id: delta.id });
    if (!items?.[0]) {
      return;
    }

    await postDownloadEvent(items[0], state);
  });
}

postHeartbeat();
