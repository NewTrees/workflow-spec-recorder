const statusElement = document.getElementById("status");

async function refreshStatus() {
  try {
    const response = await fetch("http://127.0.0.1:8765/health");
    if (!response.ok) {
      throw new Error("not ok");
    }

    statusElement.textContent = "桌面端已连接";
    statusElement.className = "status ok";
  } catch {
    statusElement.textContent = "桌面端未连接";
    statusElement.className = "status bad";
  }
}

refreshStatus();

