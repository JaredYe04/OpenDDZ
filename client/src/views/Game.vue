<template>
  <div class="game">
    <div class="header">
      <span class="title">斗地主</span>
      <span v-if="errorMsg" class="error">{{ errorMsg }}</span>
      <span v-if="gameEnd" class="winner">游戏结束：{{ winnerName }} 获胜</span>
      <span v-if="requestPlay && myTurn && countdown >= 0" class="timer-badge">{{ countdown }}s</span>
    </div>
    <div class="messages" v-if="messages.length">
      <div v-for="(m, i) in messages" :key="i" class="msg">{{ m }}</div>
    </div>

    <div class="table" v-if="playerCount > 0">
      <div class="seat seat-left" v-if="playerCount >= 3">
        <div class="seat-label">{{ seatLabel(seatLeft) }}</div>
        <div class="seat-cards">
          <GameCard
            v-for="(c, i) in (lastMoveBySeat[seatLeft] || [])"
            :key="`${seatLeft}-${i}-${c}`"
            :card="c"
          />
        </div>
      </div>
      <div class="seat seat-top" v-if="playerCount === 4">
        <div class="seat-label">{{ seatLabel(seatTop) }}</div>
        <div class="seat-cards">
          <GameCard
            v-for="(c, i) in (lastMoveBySeat[seatTop] || [])"
            :key="`${seatTop}-${i}-${c}`"
            :card="c"
          />
        </div>
      </div>
      <div class="seat seat-right" v-if="playerCount >= 3">
        <div class="seat-label">{{ seatLabel(seatRight) }}</div>
        <div class="seat-cards">
          <GameCard
            v-for="(c, i) in (lastMoveBySeat[seatRight] || [])"
            :key="`${seatRight}-${i}-${c}`"
            :card="c"
          />
        </div>
      </div>

      <div class="my-area">
        <div class="action-bar" v-if="requestPlay && myTurn">
          <span class="timer" v-if="countdown >= 0">{{ countdown }}s</span>
          <button class="btn" :disabled="selected.length === 0 || responded" @click="doPlay">出牌</button>
          <button class="btn" :disabled="responded" @click="doPass">不出</button>
          <button class="btn btn-hint" :disabled="responded" @click="doHint">提示</button>
        </div>
        <div class="hand-empty" v-if="!hand.length && !requestCall && !requestDiscard && !requestPlay">
          等待发牌…
        </div>
        <div
          class="hand"
          v-else-if="hand.length"
          @mousedown="onHandMouseDown"
          @mousemove="onHandMouseMove"
          @mouseup="onHandMouseUp"
        >
          <div
            v-for="(c, i) in hand"
            :key="`hand-${i}-${c}`"
            class="hand-card-wrap"
            :data-index="i"
          >
            <GameCard
              :card="c"
              :selected="selected.includes(c)"
              :is-hint="hintCards.includes(c)"
            />
          </div>
        </div>
      </div>
    </div>

    <div class="prompt" v-if="requestCall">
      请叫地主：
      <button v-for="opt in callOptions" :key="opt" class="btn small" @click="doCall(opt)">{{ opt }}</button>
    </div>
    <div class="prompt" v-if="requestDiscard">
      后手可弃一张牌（选一张后点弃牌，或不弃）：
      <button class="btn small" @click="doDiscard(null)">不弃</button>
      <button class="btn small" v-for="c in hand" :key="c" @click="doDiscard(c)">{{ cardLabel(c) }}</button>
    </div>

    <div class="back-link">
      <router-link to="/">返回大厅</router-link>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import GameCard from '../components/GameCard.vue'
import { roomStore } from '../store/room'

const myPlayerIndex = ref(0)
const playerCount = ref(3)

const hand = ref<string[]>([])
const lastCards = ref<string[]>([])
const lastMoveBySeat = ref<Record<number, string[]>>({})
const messages = ref<string[]>([])
const errorMsg = ref('')
const gameEnd = ref(false)
const winnerName = ref('')
const requestCall = ref(false)
const requestDiscard = ref(false)
const requestPlay = ref(false)
const callOptions = ref<string[]>([])
const selected = ref<string[]>([])
const countdown = ref(-1)
const responded = ref(false)
const currentPlayRequestId = ref<string | null>(null)
const hintCards = ref<string[]>([])
const myTurn = computed(() => requestPlay.value)
const n = computed(() => playerCount.value)
const seatLeft = computed(() => (myPlayerIndex.value + 1) % n.value)
const seatRight = computed(() => (myPlayerIndex.value + n.value - 1) % n.value)
const seatTop = computed(() => (myPlayerIndex.value + 2) % 4)
let timerId: ReturnType<typeof setInterval> | null = null

function seatLabel(seatIndex: number): string {
  return seatIndex === myPlayerIndex.value ? '我' : `玩家${seatIndex + 1}`
}

function cardLabel(c: string): string {
  if (c === 'X') return '小王'
  if (c === 'Y') return '大王'
  const suit: Record<string, string> = { H: '红桃', S: '黑桃', D: '方片', C: '梅花' }
  const s = c.charAt(0)
  const r = c.slice(1)
  return (suit[s] || s) + r
}

function toggleCard(c: string) {
  const i = selected.value.indexOf(c)
  if (i >= 0) selected.value = selected.value.filter((x) => x !== c)
  else selected.value = [...selected.value, c]
}

let dragStartIndex: number | null = null
let dragDidMove = false

function onHandMouseDown(e: MouseEvent) {
  const wrap = (e.target as HTMLElement).closest('.hand-card-wrap')
  if (!wrap) return
  const idx = parseInt(wrap.getAttribute('data-index') ?? '-1', 10)
  if (idx < 0) return
  dragStartIndex = idx
  dragDidMove = false
}

function onHandMouseMove(e: MouseEvent) {
  if (dragStartIndex == null) return
  const wrap = (e.target as HTMLElement).closest('.hand-card-wrap')
  if (!wrap) return
  const idx = parseInt(wrap.getAttribute('data-index') ?? '-1', 10)
  if (idx < 0) return
  dragDidMove = true
  const lo = Math.min(dragStartIndex, idx)
  const hi = Math.max(dragStartIndex, idx)
  selected.value = hand.value.slice(lo, hi + 1)
}

function onHandMouseUp() {
  if (dragStartIndex != null && !dragDidMove && hand.value[dragStartIndex]) {
    toggleCard(hand.value[dragStartIndex])
  }
  dragStartIndex = null
  dragDidMove = false
}

function doPlay() {
  const api = (window as any).electronAPI
  if (!api || selected.value.length === 0) return
  if (responded.value) return
  responded.value = true
  stopTimer()
  api.play([...selected.value], currentPlayRequestId.value ?? undefined).then(() => {
    selected.value = []
    requestPlay.value = false
  })
}

function doPass() {
  const api = (window as any).electronAPI
  if (!api) return
  if (responded.value) return
  responded.value = true
  stopTimer()
  api.pass(currentPlayRequestId.value ?? undefined).then(() => {
    requestPlay.value = false
  })
}

function doHint() {
  if (responded.value) return
  hintCards.value = computeHint()
}

function cardRankOrder(c: string): number {
  if (c === 'Y') return 14
  if (c === 'X') return 13
  const r = c.slice(1)
  const order: Record<string, number> = {
    '3': 0, '4': 1, '5': 2, '6': 3, '7': 4, '8': 5, '9': 6,
    '10': 7, 'J': 8, 'Q': 9, 'K': 10, 'A': 11, '2': 12
  }
  return order[r] ?? -1
}

function computeHint(): string[] {
  if (!hand.value.length) return []
  if (!lastCards.value.length) return [hand.value[0]]
  if (lastCards.value.length === 1) {
    const last = lastCards.value[0]
    const lastOrder = cardRankOrder(last)
    const higher = hand.value.find((c) => cardRankOrder(c) > lastOrder)
    if (higher) return [higher]
  }
  return []
}

function doCall(choice: string) {
  const api = (window as any).electronAPI
  if (!api) return
  api.call(choice).then(() => {
    requestCall.value = false
  })
}

function doDiscard(card: string | null) {
  const api = (window as any).electronAPI
  if (!api) return
  api.discard(card ?? '').then(() => {
    requestDiscard.value = false
  })
}

function stopTimer() {
  if (timerId) {
    clearInterval(timerId)
    timerId = null
  }
  countdown.value = -1
}

function onMessage(raw: string) {
  try {
    const msg = JSON.parse(raw)
    const t = msg.type
    if (t === 'ready') return
    if (t === 'message') {
      messages.value = [...messages.value.slice(-19), msg.content]
      return
    }
    if (t === 'error') {
      errorMsg.value = msg.content || '错误'
      setTimeout(() => { errorMsg.value = '' }, 4000)
      return
    }
    if (t === 'engine_exit') {
      const reason = msg.reason || (msg.code != null ? `exit ${msg.code}` : '')
      errorMsg.value = reason ? `游戏进程已结束：${reason}` : '游戏进程已结束'
      return
    }
    if (t === 'game_end') {
      gameEnd.value = true
      winnerName.value = msg.winnerName || ('P' + (msg.winnerIndex ?? 0))
      requestPlay.value = false
      requestCall.value = false
      requestDiscard.value = false
      stopTimer()
      return
    }
    if (t === 'player_index') {
      myPlayerIndex.value = msg.playerIndex ?? 0
      return
    }
    if (t === 'state') {
      if (msg.playerIndex === myPlayerIndex.value) {
        hand.value = msg.hand || []
      }
      return
    }
    if (t === 'last_move') {
      const idx = msg.lastPlayerIndex
      const cards = msg.lastCards || []
      lastCards.value = cards
      if (typeof idx === 'number' && idx >= 0) {
        lastMoveBySeat.value = { ...lastMoveBySeat.value, [idx]: cards }
        if (idx >= 3) playerCount.value = 4
      }
      return
    }
    if (t === 'request_call') {
      requestCall.value = msg.playerIndex === myPlayerIndex.value
      callOptions.value = msg.options || ['1分', '2分', '3分', '不叫']
      return
    }
    if (t === 'request_discard') {
      requestDiscard.value = msg.playerIndex === myPlayerIndex.value
      return
    }
    if (t === 'play_rejected') {
      if (msg.requestId === currentPlayRequestId.value) {
        responded.value = false
        errorMsg.value = msg.reason || '出牌不合法，请重选'
        setTimeout(() => { errorMsg.value = '' }, 4000)
      }
      return
    }
    if (t === 'request_play') {
      requestPlay.value = msg.playerIndex === myPlayerIndex.value
      currentPlayRequestId.value = msg.requestId ?? null
      if (requestPlay.value) {
        responded.value = false
        const sec = msg.deadlineSec ?? 30
        countdown.value = sec
        stopTimer()
        timerId = setInterval(() => {
          countdown.value--
          if (countdown.value <= 0 && !responded.value) {
            stopTimer()
            responded.value = true
            requestPlay.value = false
            const api = (window as any).electronAPI
            if (api) api.pass(currentPlayRequestId.value ?? undefined)
          }
        }, 1000)
      }
      return
    }
  } catch {
    // not JSON, ignore
  }
}

onMounted(() => {
  const api = (window as any).electronAPI
  if (api?.onGameMessage) api.onGameMessage(onMessage)
  window.addEventListener('mouseup', onHandMouseUp)
  if (roomStore.pendingStart) {
    playerCount.value = roomStore.pendingStart.playerCount
    api?.startGame(roomStore.pendingStart)
    roomStore.pendingStart = null
  }
})

onUnmounted(() => {
  stopTimer()
  window.removeEventListener('mouseup', onHandMouseUp)
})
</script>

<style scoped>
.game { max-width: 960px; margin: 0 auto; padding: 1rem; min-height: 100vh; }
.header { display: flex; align-items: center; gap: 1rem; margin-bottom: 1rem; flex-wrap: wrap; }
.title { font-size: 1.25rem; }
.error { color: #f88; }
.winner { color: #8f8; }
.messages { font-size: 0.85rem; color: #aaa; margin-bottom: 0.5rem; max-height: 80px; overflow-y: auto; }
.msg { margin: 0.1rem 0; }
.prompt { margin-bottom: 1rem; }
.prompt .btn { margin-right: 0.5rem; margin-bottom: 0.25rem; }
.timer-badge { margin-left: 0.5rem; background: #fa0; color: #000; padding: 0.2rem 0.5rem; border-radius: 4px; font-weight: bold; }
.back-link { margin-top: 2rem; }
.back-link a { color: #8cf; }

.table {
  position: relative;
  min-height: 320px;
  background: linear-gradient(160deg, #1b5e20 0%, #2e7d32 50%, #1b5e20 100%);
  border-radius: 16px;
  padding: 1rem;
  margin-bottom: 1rem;
  box-shadow: inset 0 0 40px rgba(0,0,0,0.2);
}
.seat {
  position: absolute;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
}
.seat-label { font-size: 0.8rem; color: rgba(255,255,255,0.9); }
.seat-cards { display: flex; flex-wrap: wrap; gap: 2px; justify-content: center; }
.seat-left { left: 12px; top: 50%; transform: translateY(-50%); }
.seat-right { right: 12px; top: 50%; transform: translateY(-50%); }
.seat-top { left: 50%; top: 12px; transform: translateX(-50%); }
.my-area {
  position: absolute;
  bottom: 12px;
  left: 50%;
  transform: translateX(-50%);
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  width: 100%;
  max-width: 640px;
}
.hand-empty {
  color: rgba(255,255,255,0.85);
  font-size: 0.95rem;
  padding: 1rem;
}
.action-bar {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 4px;
}
.action-bar .timer { color: #fff8e1; font-weight: bold; margin-right: 0.25rem; }
.action-bar .btn { padding: 0.4rem 0.8rem; font-size: 0.9rem; }
.action-bar .btn-hint { background: #f9a825; color: #212121; }
.hand { display: flex; flex-wrap: wrap; gap: 4px; justify-content: center; }
.hand-card-wrap { display: inline-block; cursor: pointer; }
.btn { padding: 0.5rem 1rem; border-radius: 6px; border: none; cursor: pointer; background: #4a7c59; color: #fff; }
.btn:disabled { opacity: 0.5; cursor: not-allowed; }
.btn.small { padding: 0.35rem 0.7rem; font-size: 0.9rem; }
</style>
