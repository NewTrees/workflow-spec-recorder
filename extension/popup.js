const statusElement = document.getElementById("status");

async function refreshStatus() {
  try {
    const response = await fetch("http://127.0.0.1:8765/api/extension-heartbeat", {
      method: "POST"
    });
    if (!response.ok) {
      throw new Error("not ok");
    }

    const health = await response.json();
    if (health.isRecording && !health.isPaused) {
      statusElement.textContent = `桌面端已连接，正在录制。已记录 ${health.stepCount} 步`;
      statusElement.className = "status recording";
      return;
    }

    if (health.isPaused) {
      statusElement.textContent = `桌面端已连接，录制已暂停。已记录 ${health.stepCount} 步`;
      statusElement.className = "status waiting";
      return;
    }

    statusElement.textContent = "桌面端已连接，等待开始录制";
    statusElement.className = "status ok";
  } catch {
    statusElement.textContent = "桌面端未连接，请先启动 Workflow Spec Recorder";
    statusElement.className = "status bad";
  }
}

refreshStatus();
