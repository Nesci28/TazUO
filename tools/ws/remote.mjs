import ws from 'ws';
import { execFile } from 'node:child_process';
import { promises as fs } from 'node:fs';
import https from 'node:https';

const port = Number(process.env.TAZUO_REMOTE_PORT ?? 2595);
const captureFile = process.env.TAZUO_CAPTURE_FILE ?? '/tmp/tazuo-remote.jpg';
const rawCaptureFile = `${captureFile}.raw.jpg`;
const intervalMs = Number(process.env.TAZUO_CAPTURE_INTERVAL ?? 250);
const maxPixels = String(process.env.TAZUO_CAPTURE_MAX_PIXELS ?? '1600');
const windowId = process.env.TAZUO_WINDOW_ID;
const mcpUrl = process.env.TAZUO_MCP_URL;
const mcpToken = process.env.TAZUO_MCP_TOKEN;
const windowX = Number(process.env.TAZUO_WINDOW_X ?? 0);
const windowY = Number(process.env.TAZUO_WINDOW_Y ?? 25);
const windowWidth = Number(process.env.TAZUO_WINDOW_WIDTH ?? 2498);
const windowHeight = Number(process.env.TAZUO_WINDOW_HEIGHT ?? 1415);
const inputHelper = process.env.TAZUO_INPUT_HELPER ?? '/tmp/tazuo-input';
const remoteToken = process.env.TAZUO_REMOTE_TOKEN;
const tlsCert = process.env.TAZUO_TLS_CERT;
const tlsKey = process.env.TAZUO_TLS_KEY;
let requestId = 1;
let lastTap = null;
const host = process.env.TAZUO_REMOTE_HOST ?? '127.0.0.1';
const { Server: WebSocketServer } = ws;
const tlsEnabled = Boolean(tlsCert && tlsKey);
const httpServer = tlsEnabled ? https.createServer({ cert: await fs.readFile(tlsCert), key: await fs.readFile(tlsKey) }) : null;
const server = tlsEnabled ? new WebSocketServer({ server: httpServer }) : new WebSocketServer({ port, host });
const clients = new Set();

server.on('connection', (socket, request) => {
  if (remoteToken) {
    const supplied = new URL(request.url ?? '/', tlsEnabled ? 'https://localhost' : 'http://localhost').searchParams.get('token');
    if (supplied !== remoteToken) {
      socket.close(1008, 'Authentication required');
      return;
    }
  }
  clients.add(socket);
  socket.on('close', () => clients.delete(socket));
  socket.on('message', data => handleControl(String(data)));
});

function handleControl(message) {
  let action;
  let payload;
  try { payload = JSON.parse(message); action = payload?.action; } catch { return; }
  const keyCodes = { north: 126, south: 125, west: 123, east: 124 };
  const direction = String(action ?? '').replace(/^move:/, '');
  const keyCode = keyCodes[direction];
  if (mcpUrl && keyCode !== undefined) {
    invokeMcp('Walk', [direction]).then(ok => { if (!ok) sendKey(keyCode); });
    return;
  }
  if (keyCode !== undefined) {
    const script = `tell application "System Events" to tell process "TazUO" to key code ${keyCode}`;
    execFile('/usr/bin/osascript', ['-e', script], () => {});
    return;
  }
  if (String(action ?? '').startsWith('say:')) {
    const message = String(action).slice(4).replace(/\\/g, '\\\\').replace(/"/g, '\\"');
    if (mcpUrl) {
      invokeMcp('Msg', [String(action).slice(4)]).then(ok => { if (!ok) sendChat(message); });
      return;
    }
    const script = `tell application "System Events" to tell process "TazUO" to keystroke "${message}" & return`;
    execFile('/usr/bin/osascript', ['-e', script], () => {});
    return;
  }
  if (action === 'chat') {
    // Return focuses the native client's speech entry line in the standard UO layout.
    sendKey(36);
    return;
  }
  if (action === 'target') {
    if (mcpUrl) invokeMcp('RequestTarget', []);
    return;
  }
  if (action === 'context') {
    if (lastTap) sendMouse(lastTap.x, lastTap.y, 'right');
    return;
  }
  if (action === 'tap' && Number.isFinite(payload?.x) && Number.isFinite(payload?.y)) {
    lastTap = { x: Math.max(0, Math.min(1, payload.x)), y: Math.max(0, Math.min(1, payload.y)) };
    sendMouse(lastTap.x, lastTap.y, 'left');
  }
}

function sendMouse(normalizedX, normalizedY, button) {
  const x = Math.round(windowX + normalizedX * windowWidth);
  const y = Math.round(windowY + normalizedY * windowHeight);
  execFile(inputHelper, [String(x), String(y), button], error => {
    if (!error) return;
    const script = `tell application "System Events" to tell process "TazUO" to click at {${x}, ${y}}`;
    execFile('/usr/bin/osascript', ['-e', script], () => {});
  });
}

function sendKey(keyCode) {
  const script = `tell application "System Events" to tell process "TazUO" to key code ${keyCode}`;
  execFile('/usr/bin/osascript', ['-e', script], () => {});
}

function sendChat(message) {
  const script = `tell application "System Events" to tell process "TazUO" to keystroke "${message}" & return`;
  execFile('/usr/bin/osascript', ['-e', script], () => {});
}

async function invokeMcp(method, args) {
  try {
    const headers = { 'content-type': 'application/json' };
    if (mcpToken) headers.authorization = `Bearer ${mcpToken}`;
    const response = await fetch(mcpUrl, { method: 'POST', headers, body: JSON.stringify({
      jsonrpc: '2.0', id: requestId++, method: 'tools/call',
      params: { name: 'legion_api_invoke', arguments: { method, args } }
    }) });
    return response.ok;
  } catch { return false; }
}

async function capture() {
  try {
    if (!windowId) throw new Error('TAZUO_WINDOW_ID is not set');
    await new Promise((resolve, reject) => execFile('/usr/sbin/screencapture', ['-x', '-t', 'jpg', '-l', windowId, rawCaptureFile], error => error ? reject(error) : resolve()));
    await new Promise((resolve, reject) => execFile('/usr/bin/sips', ['-Z', maxPixels, '-s', 'formatOptions', '60', rawCaptureFile, '--out', captureFile], error => error ? reject(error) : resolve()));
    const frame = await fs.readFile(captureFile);
    for (const client of clients) if (client.readyState === 1) client.send(frame);
  } catch { /* screen recording permission or native client may be unavailable */ }
  setTimeout(capture, intervalMs);
}

if (httpServer) {
  httpServer.listen(port, host, () => console.log(`TazUO real-client stream: wss://${host}:${port} (TLS screen capture every ${intervalMs} ms)`));
} else {
  console.log(`TazUO real-client stream: ws://${host}:${port} (screen capture every ${intervalMs} ms)`);
}
capture();
