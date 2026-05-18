const API_ROOT = "http://127.0.0.1:8765";

async function postEvent(event) {
  try {
    const response = await fetch(`${API_ROOT}/api/events`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify(event)
    });

    await chrome.action.setBadgeBackgroundColor({ color: response.ok ? "#16A34A" : "#F97316" });
    await chrome.action.setBadgeText({ text: response.ok ? "ON" : "!" });
  } catch (error) {
    await chrome.action.setBadgeBackgroundColor({ color: "#DC2626" });
    await chrome.action.setBadgeText({ text: "OFF" });
  }
}

async function captureScreenshot(windowId) {
  try {
    return await chrome.tabs.captureVisibleTab(windowId, { format: "png" });
  } catch {
    return null;
  }
}

chrome.runtime.onMessage.addListener((message, sender) => {
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

