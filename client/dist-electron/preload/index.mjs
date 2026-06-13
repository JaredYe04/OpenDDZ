"use strict";
const electron = require("electron");
electron.contextBridge.exposeInMainWorld("electronAPI", {
  createRoom: (opts) => electron.ipcRenderer.invoke("create-room", opts),
  joinRoom: (hostIP) => electron.ipcRenderer.invoke("join-room", hostIP),
  startGame: (opts) => electron.ipcRenderer.invoke("start-game", opts),
  play: (cards, requestId) => electron.ipcRenderer.invoke("game-play", cards, requestId),
  pass: (requestId) => electron.ipcRenderer.invoke("game-pass", requestId),
  call: (choice) => electron.ipcRenderer.invoke("game-call", choice),
  discard: (card) => electron.ipcRenderer.invoke("game-discard", card),
  onGameMessage: (cb) => {
    electron.ipcRenderer.on("game-message", (_e, msg) => cb(msg));
  }
});
