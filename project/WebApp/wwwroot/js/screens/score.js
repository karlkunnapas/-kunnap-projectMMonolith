// src/screens/score.ts
var content = document.getElementById("content");
var screen = window.__screen || "----";
var appName = "team-name-" + screen;
content.style.backgroundImage = "url('/img/score.png')";
content.style.backgroundSize = "cover";
content.style.backgroundPosition = "center";
content.style.backgroundRepeat = "no-repeat";
content.style.position = "relative";
var textInfo = document.createElement("div");
textInfo.id = "scoreText";
textInfo.style.position = "absolute";
textInfo.style.top = "50%";
textInfo.style.left = "50%";
textInfo.style.transform = "translate(-50%, -50%)";
textInfo.style.color = "white";
textInfo.style.textAlign = "center";
textInfo.style.whiteSpace = "nowrap";
content.appendChild(textInfo);
var REFERENCE_TEXT = "8888";
var TARGET_WIDTH_RATIO = 0.8;
var scaleFontSize = () => {
  const viewportWidth = window.innerWidth;
  const targetWidth = viewportWidth * TARGET_WIDTH_RATIO;
  const measurer = document.createElement("span");
  measurer.style.position = "absolute";
  measurer.style.visibility = "hidden";
  measurer.style.whiteSpace = "nowrap";
  measurer.style.font = window.getComputedStyle(textInfo).font || "inherit";
  measurer.style.fontSize = "100px";
  measurer.textContent = REFERENCE_TEXT;
  document.body.appendChild(measurer);
  const measuredWidth = measurer.offsetWidth;
  document.body.removeChild(measurer);
  const scaledFontSize = targetWidth / measuredWidth * 100;
  textInfo.style.fontSize = `${scaledFontSize}px`;
  console.log(`[${appName}] scaling font size ${scaledFontSize}px for target width ${targetWidth}px`);
};
textInfo.textContent = screen;
scaleFontSize();
window.addEventListener("resize", scaleFontSize);
console.log(`[${screen}] initialized`);
var handleSSECommand = (command) => {
  switch (command.type) {
    case "score":
      if (command.screen && command.screen === screen) {
        if (command.value) {
          textInfo.textContent = command.value;
        } else {
          textInfo.textContent = "";
        }
      }
      break;
    default:
      console.log(`[${screen}] Unhandled command: ` + JSON.stringify(command));
  }
};
var initSSE = (sseUrl = "/api/sse") => {
  const eventSource = new EventSource(sseUrl);
  eventSource.onopen = () => {
    console.log(`[${screen}] SSE connection established`);
  };
  eventSource.onmessage = (event) => {
    try {
      const command = JSON.parse(event.data);
      handleSSECommand(command);
    } catch (error) {
      console.error(`[${screen}] SSE failed to parse command:`, error);
    }
  };
  eventSource.onerror = (error) => {
    console.error(`[${screen}] SSE connection error:`, error);
  };
  window.__sseEventSource = eventSource;
};
initSSE();
//# sourceMappingURL=score.js.map
