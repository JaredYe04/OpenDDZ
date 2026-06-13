import { ipcRenderer, contextBridge } from 'electron'

contextBridge.exposeInMainWorld('electronAPI', {
  createRoom: (opts: { mode: string; deckCount: number; playerCount: number }) =>
    ipcRenderer.invoke('create-room', opts),
  joinRoom: (hostIP: string) => ipcRenderer.invoke('join-room', hostIP),
  startGame: (opts: { mode: string; deckCount: number; playerCount: number; players: { human: boolean }[]; seed?: number }) =>
    ipcRenderer.invoke('start-game', opts),
  play: (cards: string[], requestId?: string) => ipcRenderer.invoke('game-play', cards, requestId),
  pass: (requestId?: string) => ipcRenderer.invoke('game-pass', requestId),
  call: (choice: string) => ipcRenderer.invoke('game-call', choice),
  discard: (card: string) => ipcRenderer.invoke('game-discard', card),
  onGameMessage: (cb: (msg: string) => void) => {
    ipcRenderer.on('game-message', (_e, msg: string) => cb(msg))
  },
})

declare global {
  interface Window {
    electronAPI: {
      createRoom: (opts: { mode: string; deckCount: number; playerCount: number }) => Promise<{ ok: boolean; port?: number; addresses?: string[] }>
      joinRoom: (hostIP: string) => Promise<{ ok: boolean; host?: string; port?: number }>
      startGame: (opts: { mode: string; deckCount: number; playerCount: number; players: { human: boolean }[]; seed?: number }) => Promise<{ ok: boolean }>
      play: (cards: string[], requestId?: string) => Promise<{ ok: boolean }>
      pass: (requestId?: string) => Promise<{ ok: boolean }>
      call: (choice: string) => Promise<{ ok: boolean }>
      discard: (card: string) => Promise<{ ok: boolean }>
      onGameMessage: (cb: (msg: string) => void) => void
    }
  }
}
