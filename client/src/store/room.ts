export type PendingStart = {
  mode: string
  deckCount: number
  playerCount: number
  players: { human: boolean }[]
}

export const roomStore = {
  role: 'host' as 'host' | 'client',
  addresses: [] as string[],
  port: 8765,
  host: '',
  pendingStart: null as PendingStart | null,
}
