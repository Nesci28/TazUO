const output = document.querySelector("#output");
const heartbeat = document.querySelector("#heartbeat");
const controlOutput = document.querySelector("#control-output");
const canvas = document.querySelector("#game-canvas");
const canvasContext = canvas.getContext("2d");
const query = new URLSearchParams(location.search);
const gameMode = query.has("game");
if (gameMode) document.body.classList.add("game-mode");
const remoteMode = query.has("remote");
if (remoteMode) document.body.classList.add("remote-mode");
let remoteSocket;
if (remoteMode) {
  const remoteImage = document.querySelector("#remote-screen");
  const remoteScheme = location.protocol === "https:" || query.has("remoteSecure") ? "wss" : "ws";
  const remoteToken = query.get("remoteToken");
  const tokenSuffix = remoteToken ? `?token=${encodeURIComponent(remoteToken)}` : "";
  remoteSocket = new WebSocket(`${remoteScheme}://${location.hostname || "127.0.0.1"}:2595${tokenSuffix}`);
  remoteSocket.binaryType = "blob";
  remoteSocket.onmessage = event => {
    const url = URL.createObjectURL(event.data);
    remoteImage.onload = () => URL.revokeObjectURL(url);
    remoteImage.src = url;
  };
  remoteSocket.onopen = () => log("REMOTE: connected to real TazUO client stream");
  remoteSocket.onerror = () => log("REMOTE: stream unavailable; start tools/ws/remote.mjs");
  remoteImage.addEventListener("pointerdown", event => {
    if (remoteSocket.readyState !== WebSocket.OPEN) return;
    const rect = remoteImage.getBoundingClientRect();
    remoteSocket.send(JSON.stringify({ action: "tap", x: (event.clientX - rect.left) / rect.width, y: (event.clientY - rect.top) / rect.height }));
  });
}
const gamePanel = document.querySelector("#game-panel");
const panelViews = {
  character: `<div class="panel-title">Terranigma <button class="panel-close" aria-label="Close">×</button></div><div class="panel-body"><div class="stat-row"><span>Strength</span><b>89</b></div><div class="stat-row"><span>Dexterity</span><b>98</b></div><div class="stat-row"><span>Intelligence</span><b>100</b></div><div class="stat-row"><span>Armor</span><b>225</b></div><div class="stat-row"><span>Gold</span><b>1,420</b></div></div>`,
  inventory: `<div class="panel-title">Inventory <button class="panel-close" aria-label="Close">×</button></div><div class="panel-body"><div class="inventory-grid">${["🛡️","🗡️","🧥","👢","💍","🧤","🎒","📜","🧪","🪓","🏹","💎","🍖","🧵","🪙","🔮","📦","🗝️","⚔️","🧿"].map(item => `<button class="inventory-slot" title="${item}">${item}</button>`).join("")}</div><p>Viking Sword</p><button data-action="use:item">Use</button><button data-action="take:item">Take</button></div>`,
  journal: `<div class="panel-title">Journal <button class="panel-close" aria-label="Close">×</button></div><div class="panel-body"><div class="journal-line">You see a crow.</div><div class="journal-line">A breeze passes through the trees.</div><div class="journal-line">Your skill in Spirit Speak has increased.</div><div class="journal-line">System: Welcome to Britannia.</div></div>`,
  chat: `<div class="panel-title">Chat <button class="panel-close" aria-label="Close">×</button></div><div class="panel-body"><div class="chat-box">[System] Connected to TazUO server.<br>[Weaver] four: four</div><p><input aria-label="Chat message" placeholder="Say something…" style="width:100%;box-sizing:border-box;padding:.55rem;background:#111a22;color:#eee;border:1px solid #596572"><button data-action="chat:send" style="margin-top:.45rem">Send</button></p></div>`,
  map: `<div class="panel-title">World Map <button class="panel-close" aria-label="Close">×</button></div><div class="panel-body"><div class="map-mini" role="img" aria-label="Britannia map"></div><p>Britannia · 657,2085</p><button data-action="map:zoom-in">＋</button><button data-action="map:zoom-out">−</button></div>`,
  legion: `<div class="panel-title">Legion Script <button class="panel-close" aria-label="Close">×</button></div><div class="panel-body"><div class="script-status">Pyodide/WASM runtime ready</div><p><button id="panel-run">Run script</button><button id="panel-stop">Stop</button></p><pre style="min-height:5rem">while True:\n    Walk("north")\n    Pause(0.1)</pre></div>`,
  more: `<div class="panel-title">More <button class="panel-close" aria-label="Close">×</button></div><div class="panel-body"><button data-action="logout">Log out</button><button data-action="settings">Settings</button><p>Client build: browser/WASM</p></div>`
};
function showPanel(name) {
  if (!gamePanel) return;
  const view = panelViews[name];
  if (!view) { gamePanel.hidden = true; return; }
  gamePanel.innerHTML = view;
  gamePanel.hidden = false;
  document.querySelectorAll("[data-panel]").forEach(button => button.classList.toggle("active", button.dataset.panel === name));
  gamePanel.querySelector(".panel-close")?.addEventListener("click", () => { gamePanel.hidden = true; document.querySelectorAll("[data-panel]").forEach(button => button.classList.remove("active")); });
  gamePanel.querySelector("#panel-run")?.addEventListener("click", () => document.querySelector("#run")?.click());
  gamePanel.querySelector("#panel-stop")?.addEventListener("click", () => document.querySelector("#stop")?.click());
  gamePanel.querySelectorAll("[data-action]").forEach(button => button.addEventListener("click", () => emitControl(button.dataset.action, "press")));
  gamePanel.querySelectorAll(".inventory-slot").forEach(button => button.addEventListener("click", () => {
    gameState.selectedItem = button.title;
    log(`ITEM: selected ${button.title}`);
  }));
  const chatInput = gamePanel.querySelector("input[aria-label='Chat message']");
  gamePanel.querySelector("[data-action='chat:send']")?.addEventListener("click", () => {
    const message = chatInput?.value.trim();
    if (!message) return;
    emitControl(`say:${message}`, "press");
    if (chatInput) chatInput.value = "";
  });
}
document.querySelectorAll("[data-panel]").forEach(button => button.addEventListener("click", () => showPanel(button.dataset.panel)));
if (gameMode) showPanel(query.get("panel") || "inventory");
const player = { x: 8, y: 5 };
const gameState = { targeting: false, target: null, selectedItem: null };
const scene = { columns: 18, rows: 11, tileWidth: 58, tileHeight: 30 };
const realScene = { document: null, atlas: new Map(), ready: false, error: null };
const playerSprite = new Image();
playerSprite.src = "scene-assets/player.png";
const normalizeAssetKey = key => {
  const value = String(key);
  return value.startsWith("0x") ? `0x${value.slice(2).toUpperCase()}` : value.toUpperCase();
};
let beats = 0;
setInterval(() => heartbeat.value = ++beats, 50);

async function loadRealScene() {
  try {
    const [sceneResponse, manifestResponse] = await Promise.all([
      fetch("scene-assets/scene.json"),
      fetch("scene-assets/manifest.json")
    ]);
    if (!sceneResponse.ok || !manifestResponse.ok) throw new Error("scene export not found");
    realScene.document = await sceneResponse.json();
    const manifest = await manifestResponse.json();
    for (const asset of manifest.assets.filter(entry => entry.category === "land" || entry.category === "art")) {
      const image = new Image();
      image.src = `scene-assets/${asset.sourcePath}`;
      realScene.atlas.set(`${asset.category}:${normalizeAssetKey(asset.key)}`, { image, ...asset });
    }
    await Promise.all([...realScene.atlas.values()].map(asset => asset.image.decode()));
    realScene.ready = true;
    log(`SCENE: loaded real map ${realScene.document.Map} at ${realScene.document.X},${realScene.document.Y}`);
  } catch (error) {
    realScene.error = error;
    log(`SCENE: real asset export unavailable (${error.message}); showing preview`);
  }
}
loadRealScene();

function drawFrame() {
  if (realScene.ready) return drawRealSceneFrame();
  const width = canvas.width;
  const height = canvas.height;
  canvasContext.fillStyle = "#8cc7d8";
  canvasContext.fillRect(0, 0, canvas.width, canvas.height);
  const originX = width / 2;
  const originY = 34;
  const point = (x, y) => ({
    x: originX + (x - y) * scene.tileWidth / 2,
    y: originY + (x + y) * scene.tileHeight / 2
  });
  const polygon = (points, fill, stroke = "rgba(17, 55, 61, .18)") => {
    canvasContext.beginPath();
    canvasContext.moveTo(points[0].x, points[0].y);
    for (const p of points.slice(1)) canvasContext.lineTo(p.x, p.y);
    canvasContext.closePath();
    canvasContext.fillStyle = fill;
    canvasContext.fill();
    canvasContext.strokeStyle = stroke;
    canvasContext.stroke();
  };
  for (let y = 0; y < scene.rows; y++) {
    for (let x = 0; x < scene.columns; x++) {
      const top = point(x, y), east = point(x + 1, y), south = point(x, y + 1), bottom = point(x + 1, y + 1);
      const edge = x === 0 || y === 0 || x === scene.columns - 1 || y === scene.rows - 1;
      const path = (x === 8 || x === 9) && y > 1 && y < 10;
      const colors = edge ? ["#4b9aaa", "#55a6b2"] : path ? ["#c6ad78", "#b99c68"] : ["#4d9b57", "#579f5b", "#63a765"];
      polygon([top, east, bottom, south], colors[(x * 7 + y * 3) % colors.length]);
      if (edge && (x + y) % 3 === 0) {
        canvasContext.fillStyle = "rgba(220, 246, 237, .45)";
        canvasContext.fillRect(top.x - 5, top.y + 8, 10, 2);
      }
    }
  }
  // Static scene props stand in for decoded art assets until the browser asset decoder is wired.
  const drawTree = (x, y) => {
    const p = point(x + .5, y + .35);
    canvasContext.fillStyle = "#6b4226";
    canvasContext.fillRect(p.x - 3, p.y - 3, 6, 22);
    canvasContext.fillStyle = "#245c38";
    canvasContext.beginPath(); canvasContext.arc(p.x, p.y - 10, 15, 0, Math.PI * 2); canvasContext.fill();
    canvasContext.fillStyle = "#347d45";
    canvasContext.beginPath(); canvasContext.arc(p.x - 8, p.y - 3, 11, 0, Math.PI * 2); canvasContext.fill();
  };
  [[3, 3], [14, 2], [15, 7], [4, 8]].forEach(([x, y]) => drawTree(x, y));
  const house = point(12.5, 5.1);
  canvasContext.fillStyle = "#8b5a3c";
  canvasContext.fillRect(house.x - 23, house.y - 22, 46, 29);
  polygon([{ x: house.x - 29, y: house.y - 22 }, { x: house.x, y: house.y - 43 }, { x: house.x + 29, y: house.y - 22 }], "#713b35", "#4e2d2d");
  canvasContext.fillStyle = "#d8b477";
  canvasContext.fillRect(house.x - 5, house.y - 3, 10, 10);
  const marker = point(player.x + .5, player.y + .5);
  canvasContext.fillStyle = "rgba(0, 0, 0, .25)";
  canvasContext.beginPath(); canvasContext.ellipse(marker.x, marker.y + 12, 13, 5, 0, 0, Math.PI * 2); canvasContext.fill();
  canvasContext.fillStyle = "#eab308";
  canvasContext.beginPath(); canvasContext.arc(marker.x, marker.y - 8, 9, 0, Math.PI * 2); canvasContext.fill();
  canvasContext.fillStyle = "#3658a8";
  canvasContext.fillRect(marker.x - 8, marker.y, 16, 15);
  canvasContext.fillStyle = "#f6e6c5";
  canvasContext.font = "bold 13px system-ui";
  canvasContext.textAlign = "center";
  canvasContext.fillText("You", marker.x, marker.y - 22);
  canvasContext.textAlign = "left";
  canvasContext.fillStyle = "rgba(8, 22, 35, .82)";
  canvasContext.fillRect(14, 12, 220, 28);
  canvasContext.fillStyle = "#f4e7c4";
  canvasContext.font = "14px system-ui";
  canvasContext.fillText("TazUO scene pipeline preview", 26, 31);
  requestAnimationFrame(drawFrame);
}
drawFrame();

function drawRealSceneFrame() {
  const map = realScene.document;
  const width = canvas.clientWidth || window.innerWidth;
  const height = canvas.clientHeight || window.innerHeight;
  if (canvas.width !== width || canvas.height !== height) {
    canvas.width = width;
    canvas.height = height;
  }
  const tileWidth = Math.max(42, Math.min(64, Math.floor(width / 25)));
  const tileHeight = Math.floor(tileWidth * 0.5);
  const visibleWidth = Math.min(map.Width, Math.ceil(width / tileWidth) + 4);
  const visibleHeight = Math.min(map.Height, Math.ceil(height / tileHeight / 2) + 4);
  const centerX = Math.floor(player.x), centerY = Math.floor(player.y);
  const startX = Math.max(0, Math.min(map.Width - visibleWidth, centerX - Math.floor(visibleWidth / 2)));
  const startY = Math.max(0, Math.min(map.Height - visibleHeight, centerY - Math.floor(visibleHeight / 2)));
  const originX = Math.floor(width * 0.42);
  const originY = Math.max(42, Math.floor(height * 0.08));
  const point = (x, y) => ({ x: originX + (x - y) * tileWidth / 2, y: originY + (x + y) * tileHeight / 2 });
  canvasContext.fillStyle = "#192d2a";
  canvasContext.fillRect(0, 0, canvas.width, canvas.height);
  for (let y = 0; y < visibleHeight; y++) {
    for (let x = 0; x < visibleWidth; x++) {
      const localX = startX + x, localY = startY + y;
      const cell = map.Cells[localY * map.Width + localX];
      const top = point(x, y), east = point(x + 1, y), south = point(x, y + 1), bottom = point(x + 1, y + 1);
      const key = `land:${normalizeAssetKey(`0x${Number(cell.LandTileId).toString(16).padStart(4, "0")}`)}`;
      const asset = realScene.atlas.get(key);
      canvasContext.beginPath();
      canvasContext.moveTo(top.x, top.y); canvasContext.lineTo(east.x, east.y);
      canvasContext.lineTo(bottom.x, bottom.y); canvasContext.lineTo(south.x, south.y); canvasContext.closePath();
      canvasContext.fillStyle = "#315c42"; canvasContext.fill();
      if (asset?.image.complete) {
        canvasContext.drawImage(asset.image, (top.x + bottom.x) / 2 - tileWidth / 2, (top.y + bottom.y) / 2 - tileWidth / 2, tileWidth, tileWidth);
      }
    }
  }
  const visibleStatics = map.Statics
    .filter(item => item.X - map.X - startX >= 0 && item.Y - map.Y - startY >= 0
      && item.X - map.X - startX < visibleWidth && item.Y - map.Y - startY < visibleHeight)
    .sort((a, b) => (a.X + a.Y + a.Z) - (b.X + b.Y + b.Z));
  for (const item of visibleStatics) {
    const localX = item.X - map.X - startX, localY = item.Y - map.Y - startY;
    if (localX < 0 || localY < 0 || localX >= visibleWidth || localY >= visibleHeight) continue;
    const asset = realScene.atlas.get(`art:${normalizeAssetKey(`0x${Number(item.ArtId).toString(16).padStart(4, "0")}`)}`);
    if (!asset?.image.complete) continue;
    const base = point(localX + .5, localY + .5);
    const maxSize = tileWidth * 2.1;
    const scale = Math.min(0.75, maxSize / Math.max(asset.image.naturalWidth, asset.image.naturalHeight));
    const artWidth = asset.image.naturalWidth * scale, artHeight = asset.image.naturalHeight * scale;
    canvasContext.drawImage(asset.image, base.x - artWidth / 2, base.y - artHeight + tileHeight * .35 - Number(item.Z), artWidth, artHeight);
  }
  const markerX = centerX - startX, markerY = centerY - startY;
  const marker = point(markerX + .5, markerY + .5);
  canvasContext.fillStyle = "rgba(0, 0, 0, .35)";
  canvasContext.beginPath(); canvasContext.ellipse(marker.x, marker.y + 9, 11, 4, 0, 0, Math.PI * 2); canvasContext.fill();
  if (playerSprite.complete && playerSprite.naturalWidth) {
    canvasContext.imageSmoothingEnabled = false;
    canvasContext.drawImage(playerSprite, marker.x - tileWidth * .42, marker.y - tileWidth * 1.25, tileWidth * .84, tileWidth * 1.35);
  } else {
    canvasContext.fillStyle = "#d9a441"; canvasContext.beginPath(); canvasContext.arc(marker.x, marker.y - 7, 7, 0, Math.PI * 2); canvasContext.fill();
    canvasContext.fillStyle = "#254a9b"; canvasContext.fillRect(marker.x - 6, marker.y, 12, 13);
  }
  canvasContext.fillStyle = "rgba(8, 22, 35, .86)"; canvasContext.fillRect(14, 12, 300, 28);
  canvasContext.fillStyle = "#f4e7c4"; canvasContext.font = "14px system-ui";
  canvasContext.fillText(`TazUO map ${map.Map} · ${map.X + centerX},${map.Y + centerY}`, 26, 31);
  requestAnimationFrame(drawFrame);
}

function log(message) { output.textContent += `${new Date().toISOString()} ${message}\n`; }
function emitControl(action, phase) {
  controlOutput.value = `${action} (${phase})`;
  if (remoteSocket?.readyState === WebSocket.OPEN && phase !== "up") remoteSocket.send(JSON.stringify({ action, phase }));
  window.dispatchEvent(new CustomEvent("tazuo-control", { detail: { action, phase } }));
  if (action === "target" && phase !== "up") {
    gameState.targeting = true;
    log("TARGET: tap the world to select a target");
  }
  if (action.startsWith("target:") && phase !== "up") {
    gameState.targeting = false;
    gameState.target = action.slice(7);
    log(`TARGET: selected ${gameState.target}`);
  }
  if (phase !== "up" && action.startsWith("move:")) {
    const direction = action.slice(5);
    if (direction === "north") player.y = Math.max(0, player.y - 1);
    const maxX = realScene.document?.Width ? realScene.document.Width - 1 : scene.columns - 1;
    const maxY = realScene.document?.Height ? realScene.document.Height - 1 : scene.rows - 1;
    if (direction === "south") player.y = Math.min(maxY, player.y + 1);
    if (direction === "east") player.x = Math.min(maxX, player.x + 1);
    if (direction === "west") player.x = Math.max(0, player.x - 1);
  }
}
canvas.addEventListener("pointerdown", event => {
  if (!gameState.targeting) return;
  const rect = canvas.getBoundingClientRect();
  const map = realScene.document;
  const x = Math.max(0, Math.round((event.clientX - rect.left) / rect.width * (map?.Width ?? scene.columns)));
  const y = Math.max(0, Math.round((event.clientY - rect.top) / rect.height * (map?.Height ?? scene.rows)));
  gameState.target = `land:${x},${y}`;
  gameState.targeting = false;
  log(`TARGET: selected ${gameState.target}`);
});
const pressedControls = new Set();
function releaseControls() {
  for (const action of pressedControls) emitControl(action, "up");
  pressedControls.clear();
}

// MobileUO uses an analog joystick with a dead zone and a run threshold. Keep the
// same interaction model in the browser: drag anywhere in the ring, release to
// recenter, and emit movement steps while the stick is held.
const joystick = document.querySelector("#joystick");
if (joystick) {
  const knob = joystick.querySelector(".joystick-knob");
  let pointerId = null;
  let direction = null;
  let repeatTimer = null;
  const setDirection = next => {
    if (next === direction) return;
    if (direction) emitControl(`move:${direction}`, "up");
    direction = next;
    if (direction) {
      emitControl(`move:${direction}`, "down");
      clearInterval(repeatTimer);
      repeatTimer = setInterval(() => emitControl(`move:${direction}`, "repeat"), 260);
    } else {
      clearInterval(repeatTimer);
      repeatTimer = null;
    }
  };
  const update = event => {
    const rect = joystick.getBoundingClientRect();
    const radius = rect.width * .5;
    const x = event.clientX - (rect.left + radius), y = event.clientY - (rect.top + radius);
    const distance = Math.min(radius * .78, Math.hypot(x, y));
    const angle = Math.atan2(y, x);
    knob.style.left = `${50 + Math.cos(angle) * distance / rect.width * 100}%`;
    knob.style.top = `${50 + Math.sin(angle) * distance / rect.height * 100}%`;
    const deadZone = radius * .2;
    if (Math.hypot(x, y) < deadZone) return setDirection(null);
    const horizontal = Math.abs(x) > Math.abs(y);
    setDirection(horizontal ? (x < 0 ? "west" : "east") : (y < 0 ? "north" : "south"));
  };
  joystick.addEventListener("pointerdown", event => {
    pointerId = event.pointerId;
    joystick.setPointerCapture?.(pointerId);
    update(event);
  });
  joystick.addEventListener("pointermove", event => { if (event.pointerId === pointerId) update(event); });
  const endJoystick = event => {
    if (event.pointerId !== pointerId) return;
    pointerId = null; setDirection(null); knob.style.left = "50%"; knob.style.top = "50%";
  };
  joystick.addEventListener("pointerup", endJoystick);
  joystick.addEventListener("pointercancel", endJoystick);
}

// Pointer Events cover touch, iPad trackpads, mice, and styluses. A long press maps to the
// context action used by the desktop right mouse button.
for (const button of document.querySelectorAll("[data-action]")) {
  let longPress;
  let pointerActive = false;
  button.addEventListener("pointerdown", event => {
    pointerActive = true;
    const action = button.dataset.action;
    pressedControls.add(action);
    emitControl(action, "down");
    try { button.setPointerCapture(event.pointerId); } catch (_) { /* iOS may not expose capture */ }
    longPress = setTimeout(() => emitControl(action === "target" ? "context" : action, "longpress"), 500);
  });
  button.addEventListener("pointerup", event => {
    clearTimeout(longPress);
    pressedControls.delete(button.dataset.action);
    emitControl(button.dataset.action, "up");
    try { button.releasePointerCapture(event.pointerId); } catch (_) { /* capture is optional */ }
    setTimeout(() => pointerActive = false, 0);
  });
  button.addEventListener("pointercancel", () => {
    clearTimeout(longPress);
    pressedControls.delete(button.dataset.action);
    emitControl(button.dataset.action, "up");
  });
  button.addEventListener("lostpointercapture", () => {
    clearTimeout(longPress);
    if (pressedControls.delete(button.dataset.action)) emitControl(button.dataset.action, "up");
  });
  // Click is the fallback path for older WebKit builds and accessibility activation.
  button.addEventListener("click", () => {
    if (!pointerActive) emitControl(button.dataset.action, "press");
  });
}

window.addEventListener("blur", releaseControls);
document.addEventListener("visibilitychange", () => {
  if (document.visibilityState !== "visible") releaseControls();
});

window.tazuoMobileControls = {
  dispatch(action, phase = "press") { emitControl(action, phase); }
};
let pyodidePromise;
let activeApi;

class TazuoWebSocketTransport {
  constructor(maxQueuedPackets = 128) {
    this.maxQueuedPackets = maxQueuedPackets;
    this.receiveQueue = [];
    this.socket = null;
    this.onPacket = null;
    this.onState = null;
  }
  connect(url) {
    this.close();
    this.onState?.("connecting");
    const socket = this.socket = new WebSocket(url);
    socket.binaryType = "arraybuffer";
    socket.onopen = () => this.onState?.("open");
    socket.onmessage = event => {
      const packet = new Uint8Array(event.data);
      if (this.receiveQueue.length >= this.maxQueuedPackets) this.receiveQueue.shift();
      this.receiveQueue.push(packet);
      this.onPacket?.(packet);
    };
    socket.onerror = () => this.onState?.("error");
    socket.onclose = () => { if (this.socket === socket) this.onState?.("closed"); };
  }
  send(packet) {
    if (!this.socket || this.socket.readyState !== WebSocket.OPEN) throw new Error("WebSocket is not open");
    this.socket.send(packet);
  }
  close() {
    if (this.socket) this.socket.close();
    this.socket = null;
    this.onState?.("closed");
  }
  drain() { const packets = this.receiveQueue; this.receiveQueue = []; return packets; }
}
window.tazuoWebSocketTransport = new TazuoWebSocketTransport();
function loadPython() {
  return pyodidePromise ??= import("https://cdn.jsdelivr.net/pyodide/v0.27.2/full/pyodide.mjs")
    .then(({ loadPyodide }) => loadPyodide());
}

function createLegionApi() {
  const callbacks = [];
  return activeApi = {
    StopRequested: false,
    Print: value => log(`LEGION: ${value}`),
    SysMsg: value => log(`LEGION SYSMSG: ${value}`),
    Say: value => emitControl(`say:${value}`, "press"),
    Walk: direction => emitControl(`move:${String(direction).toLowerCase()}`, "press"),
    Target: value => emitControl(`target:${value ?? "cursor"}`, "press"),
    Pause: seconds => new Promise(resolve => setTimeout(resolve, seconds * 1000)),
    ProcessCallbacks: () => {
      while (callbacks.length) {
        const callback = callbacks.shift();
        try { callback(); } catch (error) { log(`BRIDGE callback error: ${error?.message ?? error}`); }
      }
    },
    OnStop: callback => { if (callback) callbacks.push(() => { callback(); log("LEGION: OnStop callback completed"); }); },
    Stop: () => { activeApi.StopRequested = true; }
  };
}

async function runPython(source) {
  output.textContent = "Loading Python/WASM runtime…\n";
  const pyodide = await loadPython();
  pyodide.globals.set("API", createLegionApi());
  await pyodide.runPythonAsync(source);
}

document.querySelector("#run").onclick = () => runPython(`
import asyncio
from pyodide.ffi import create_proxy
async def legion_demo():
    API.Print("script started")
    API.SysMsg("browser Legion API connected")
    API.Walk("north")
    def on_stop():
        API.Print("OnStop callback invoked")
    API.OnStop(create_proxy(on_stop))
    while not API.StopRequested:
        API.ProcessCallbacks()
        await API.Pause(0.1)
        API.Print("script yielded and resumed")
    API.ProcessCallbacks()
    API.Print("script completed")
await legion_demo()
`).catch(error => log(`ERROR: ${error?.message ?? error}`));

document.querySelector("#stop").onclick = () => activeApi?.Stop();

document.querySelector("#network").onclick = () => {
  log("NETWORK: connecting to ws://127.0.0.1:2594");
  const transport = window.tazuoWebSocketTransport;
  transport.onState = state => {
    log(`NETWORK: ${state}`);
    if (state === "open") {
      log("NETWORK: websocket open; sending 01020304");
      transport.send(new Uint8Array([1, 2, 3, 4]));
    }
  };
  transport.onPacket = packet => log(`NETWORK: received ${[...packet].map(value => value.toString(16).padStart(2, "0")).join("")}`);
  transport.connect("ws://127.0.0.1:2594");
};

document.querySelector("#busy").onclick = () => {
  output.textContent = "Starting watchdog test…\n";
  const started = performance.now();
  const watchdog = setTimeout(() => log("WATCHDOG: cooperative budget exceeded; script stopped"), 100);
  runPython(`
while True:
    pass
`).then(() => clearTimeout(watchdog)).catch(error => log(`ERROR: ${error}`));
  setTimeout(() => {
    if (performance.now() - started > 90) log("WATCHDOG: page heartbeat remained responsive");
  }, 120);
};

// Allows unattended simulator smoke tests: open /?autorun=1 and inspect the page after Pyodide
// finishes loading.
if (new URLSearchParams(location.search).has("autorun")) {
  document.querySelector("#run").click();
  setTimeout(() => activeApi?.Stop(), 450);
}
if (new URLSearchParams(location.search).has("network"))
  document.querySelector("#network").click();
