const originalValues = new WeakMap();

function sendCaptureEvent(payload) {
  chrome.runtime.sendMessage({
    type: "capture-event",
    payload: {
      ...payload,
      capturedAtUtc: new Date().toISOString(),
      pageUrl: location.href,
      pageTitle: document.title
    }
  });
}

function isTextLikeInput(element) {
  if (!(element instanceof HTMLInputElement)) {
    return false;
  }

  return ["text", "email", "password", "number", "search", "tel", "url"].includes(element.type);
}

function inferRole(element) {
  if (element instanceof HTMLButtonElement) return "button";
  if (element instanceof HTMLSelectElement) return "combobox";
  if (element instanceof HTMLTextAreaElement) return "textbox";
  if (element instanceof HTMLInputElement) {
    if (element.type === "checkbox") return "checkbox";
    if (element.type === "radio") return "radio";
    if (element.type === "file") return "textbox";
    return "textbox";
  }
  if (element instanceof HTMLAnchorElement) return "link";
  return element.getAttribute("role") ?? element.tagName.toLowerCase();
}

function findLabelText(element) {
  if (element.id) {
    const explicit = document.querySelector(`label[for="${CSS.escape(element.id)}"]`);
    if (explicit?.textContent?.trim()) {
      return explicit.textContent.trim();
    }
  }

  const wrappingLabel = element.closest("label");
  if (wrappingLabel?.textContent?.trim()) {
    return wrappingLabel.textContent.trim();
  }

  return null;
}

function buildSelector(element) {
  if (element.id) {
    return `#${CSS.escape(element.id)}`;
  }

  const testId = element.getAttribute("data-testid");
  if (testId) {
    return `[data-testid="${CSS.escape(testId)}"]`;
  }

  const name = element.getAttribute("name");
  if (name) {
    return `${element.tagName.toLowerCase()}[name="${CSS.escape(name)}"]`;
  }

  const ariaLabel = element.getAttribute("aria-label");
  if (ariaLabel) {
    return `${element.tagName.toLowerCase()}[aria-label="${CSS.escape(ariaLabel)}"]`;
  }

  const parts = [];
  let current = element;
  while (current && current !== document.body) {
    const tag = current.tagName.toLowerCase();
    const siblings = Array.from(current.parentElement?.children ?? []).filter(
      sibling => sibling.tagName === current.tagName
    );
    const index = siblings.indexOf(current) + 1;
    parts.unshift(`${tag}:nth-of-type(${Math.max(index, 1)})`);
    current = current.parentElement;
  }

  return parts.join(" > ");
}

function buildAlternateSelectors(element) {
  const selectors = [];
  const placeholder = element.getAttribute("placeholder");
  const ariaLabel = element.getAttribute("aria-label");

  if (placeholder) {
    selectors.push(`${element.tagName.toLowerCase()}[placeholder="${placeholder}"]`);
  }

  if (ariaLabel) {
    selectors.push(`${element.tagName.toLowerCase()}[aria-label="${ariaLabel}"]`);
  }

  return selectors;
}

function snapshotElement(element) {
  return {
    name: element.getAttribute("name"),
    role: inferRole(element),
    tagName: element.tagName.toLowerCase(),
    inputType: element instanceof HTMLInputElement ? element.type : null,
    label: findLabelText(element),
    text: element.textContent?.trim() || null,
    placeholder: element.getAttribute("placeholder"),
    ariaLabel: element.getAttribute("aria-label"),
    cssSelector: buildSelector(element),
    alternateSelectors: buildAlternateSelectors(element)
  };
}

document.addEventListener("focusin", (event) => {
  const target = event.target;
  if (
    target instanceof HTMLInputElement ||
    target instanceof HTMLTextAreaElement
  ) {
    originalValues.set(target, target.value);
  }
}, true);

document.addEventListener("focusout", (event) => {
  const target = event.target;
  if (
    !(target instanceof HTMLTextAreaElement) &&
    !isTextLikeInput(target)
  ) {
    return;
  }

  const originalValue = originalValues.get(target);
  if (originalValue === target.value) {
    return;
  }

  sendCaptureEvent({
    eventType: "input",
    value: target instanceof HTMLInputElement && target.type === "password"
      ? null
      : target.value,
    element: snapshotElement(target)
  });
}, true);

document.addEventListener("change", (event) => {
  const target = event.target;

  if (target instanceof HTMLSelectElement) {
    sendCaptureEvent({
      eventType: "select",
      value: target.selectedOptions[0]?.textContent?.trim() ?? target.value,
      element: snapshotElement(target)
    });
    return;
  }

  if (target instanceof HTMLInputElement && target.type === "file") {
    sendCaptureEvent({
      eventType: "upload",
      value: target.files?.[0]?.name ?? null,
      element: snapshotElement(target)
    });
  }
}, true);

document.addEventListener("click", (event) => {
  const target = event.target instanceof Element
    ? event.target.closest("button, a, input[type='button'], input[type='submit'], [role='button']")
    : null;

  if (!target) {
    return;
  }

  sendCaptureEvent({
    eventType: "click",
    element: snapshotElement(target)
  });
}, true);

