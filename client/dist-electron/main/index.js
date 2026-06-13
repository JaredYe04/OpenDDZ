import { app, BrowserWindow, ipcMain } from "electron";
import { fileURLToPath } from "node:url";
import path from "node:path";
import fs from "node:fs";
import { spawn } from "node:child_process";
import WebSocket, { WebSocketServer } from "ws";
const __dirname$1 = path.dirname(fileURLToPath(import.meta.url));
const isDev = process.env.NODE_ENV !== "production";
const ROOT = path.join(__dirname$1, isDev ? "../.." : "../../..");
const WS_PORT = 8765;
let mainWindow = null;
let engineProcess = null;
let wss = null;
let wsClients = /* @__PURE__ */ new Set();
function getEnginePath() {
  if (isDev) {
    return path.resolve(ROOT, "..", "OpenDDZ", "bin", "Debug", "OpenDDZ.exe");
  }
  return path.join(process.resourcesPath || ROOT, "OpenDDZ.exe");
}
function broadcastToAll(msg) {
  if (mainWindow && !mainWindow.isDestroyed()) {
    mainWindow.webContents.send("game-message", msg);
  }
  wsClients.forEach((ws) => {
    if (ws.readyState === 1) ws.send(msg);
  });
}
function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1e3,
    height: 700,
    webPreferences: {
      preload: path.join(__dirname$1, "../preload/index.mjs"),
      contextIsolation: true,
      nodeIntegration: false
    }
  });
  if (process.env.VITE_DEV_SERVER_URL) {
    mainWindow.loadURL(process.env.VITE_DEV_SERVER_URL);
  } else {
    mainWindow.loadFile(path.join(ROOT, "dist/index.html"));
  }
  mainWindow.on("closed", () => {
    mainWindow = null;
  });
  mainWindow.webContents.on("before-input-event", (_, input) => {
    if (input.key === "F12") {
      mainWindow == null ? void 0 : mainWindow.webContents.openDevTools({ mode: "detached" });
    }
  });
}
app.whenReady().then(() => {
  createWindow();
  app.on("activate", () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});
app.on("window-all-closed", () => {
  if (engineProcess) {
    engineProcess.kill();
    engineProcess = null;
  }
  if (wss) {
    wss.close();
    wss = null;
  }
  wsClients.clear();
  if (process.platform !== "darwin") app.quit();
});
ipcMain.handle("create-room", async (_, opts) => {
  if (engineProcess) {
    try {
      engineProcess.kill();
    } catch {
    }
    engineProcess = null;
  }
  const exe = getEnginePath();
  if (!fs.existsSync(exe)) {
    return { ok: false, error: `引擎未找到，请先构建：${exe}` };
  }
  engineProcess = spawn(exe, ["--stdio"], {
    cwd: path.dirname(exe),
    stdio: ["pipe", "pipe", "pipe"],
    windowsHide: true
  });
  engineProcess.stdout.setEncoding("utf8");
  engineProcess.stderr.setEncoding("utf8");
  engineProcess.stdout.on("data", (chunk) => {
    const lines = chunk.split("\n").filter((l) => l.trim());
    lines.forEach((line) => broadcastToAll(line));
  });
  let stderrBuf = "";
  engineProcess.stderr.on("data", (data) => {
    stderrBuf += String(data);
    if (mainWindow) mainWindow.webContents.send("game-message", JSON.stringify({ type: "error", content: String(data).trim() }));
  });
  engineProcess.on("exit", (code, signal) => {
    const reason = stderrBuf.trim() || (code != null ? `exit code ${code}` : signal || "unknown");
    if (mainWindow) mainWindow.webContents.send("game-message", JSON.stringify({ type: "engine_exit", code, reason }));
    engineProcess = null;
  });
  engineProcess.on("error", (err) => {
    if (mainWindow) mainWindow.webContents.send("game-message", JSON.stringify({ type: "engine_exit", reason: err.message }));
    engineProcess = null;
  });
  if (!wss) {
    wss = new WebSocketServer({ port: WS_PORT, host: "0.0.0.0" });
    wss.on("connection", (ws) => {
      wsClients.add(ws);
      ws.on("message", (data) => {
        const str = data.toString();
        if ((engineProcess == null ? void 0 : engineProcess.stdin) && str.trim()) engineProcess.stdin.write(str.trim() + "\n");
      });
      ws.on("close", () => wsClients.delete(ws));
    });
  }
  const ifaces = await getLocalAddresses();
  return { ok: true, port: WS_PORT, addresses: ifaces };
});
let clientWs = null;
ipcMain.handle("join-room", async (_, hostIP) => {
  return new Promise((resolve) => {
    const url = `ws://${hostIP.trim()}:${WS_PORT}`;
    clientWs = new WebSocket(url);
    clientWs.on("open", () => resolve({ ok: true, host: hostIP, port: WS_PORT }));
    clientWs.on("message", (data) => {
      const str = data.toString();
      if (mainWindow && !mainWindow.isDestroyed()) mainWindow.webContents.send("game-message", str);
    });
    clientWs.on("error", () => resolve({ ok: false }));
    clientWs.on("close", () => {
      clientWs = null;
    });
  });
});
ipcMain.handle("start-game", async (_, opts) => {
  if (!engineProcess || !engineProcess.stdin) return { ok: false, error: "Engine not running" };
  const humanCount = 1 + wsClients.size;
  const playerCount = opts.playerCount ?? 3;
  const players = opts.players ?? Array.from({ length: playerCount }, (_2, i) => ({ human: i < humanCount }));
  const seed = opts.seed ?? Date.now();
  const startCmd = {
    cmd: "start",
    mode: opts.mode || "Normal",
    deckCount: opts.deckCount ?? 1,
    playerCount,
    seed,
    players
  };
  engineProcess.stdin.write(JSON.stringify(startCmd) + "\n");
  if (mainWindow && !mainWindow.isDestroyed()) {
    mainWindow.webContents.send("game-message", JSON.stringify({ type: "player_index", playerIndex: 0 }));
  }
  let idx = 1;
  wsClients.forEach((ws) => {
    if (ws.readyState === 1) {
      ws.send(JSON.stringify({ type: "player_index", playerIndex: idx }));
      idx++;
    }
  });
  return { ok: true };
});
function sendToEngine(obj) {
  const line = JSON.stringify(obj) + "\n";
  if (engineProcess == null ? void 0 : engineProcess.stdin) {
    engineProcess.stdin.write(line);
    return true;
  }
  if (clientWs && clientWs.readyState === 1) {
    clientWs.send(line.trim());
    return true;
  }
  return false;
}
ipcMain.handle("game-play", async (_, cards, requestId) => {
  const payload = { cmd: "play", cards };
  if (requestId) payload.requestId = requestId;
  return { ok: sendToEngine(payload) };
});
ipcMain.handle("game-pass", async (_, requestId) => {
  const payload = { cmd: "pass" };
  if (requestId) payload.requestId = requestId;
  return { ok: sendToEngine(payload) };
});
ipcMain.handle("game-call", async (_, choice) => {
  return { ok: sendToEngine({ cmd: "call", choice }) };
});
ipcMain.handle("game-discard", async (_, card) => {
  if (card && card.trim())
    return { ok: sendToEngine({ cmd: "discard", card: card.trim() }) };
  return { ok: sendToEngine({ cmd: "discard" }) };
});
async function getLocalAddresses() {
  const { networkInterfaces } = await import("node:os");
  const nets = networkInterfaces();
  const list = [];
  for (const name of Object.keys(nets)) {
    for (const n of nets[name]) {
      if (n.family === "IPv4" && !n.internal) list.push(n.address);
    }
  }
  return list.length ? list : ["127.0.0.1"];
}
