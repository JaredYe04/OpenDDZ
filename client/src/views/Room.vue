<template>
  <div class="room">
    <h1>{{ isHost ? '房间已创建' : '已加入房间' }}</h1>
    <div class="card" v-if="isHost">
      <p>等待其他玩家加入，或直接开始游戏（机器人补齐）。</p>
      <p class="addr">本机地址：{{ addresses.join(', ') }} : {{ port }}</p>
      <div class="row">
        <label>模式</label>
        <select v-model="mode">
          <option value="Normal">普通斗地主 (3人)</option>
          <option value="FourPlayer">四人斗地主 (2v2)</option>
        </select>
      </div>
      <div class="row">
        <label>副数</label>
        <input v-model.number="deckCount" type="number" min="1" max="4" />
      </div>
      <div class="row">
        <label>人数</label>
        <input v-model.number="playerCount" type="number" :min="mode === 'FourPlayer' ? 4 : 3" :max="mode === 'FourPlayer' ? 4 : 3" />
      </div>
      <button class="btn primary" @click="startGame">开始游戏</button>
    </div>
    <div class="card" v-else>
      <p>已连接到 Host，等待 Host 开始游戏。</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { roomStore } from '../store/room'

const router = useRouter()
const isHost = ref(false)
const addresses = ref<string[]>([])
const port = ref(8765)
const mode = ref<'Normal' | 'FourPlayer'>('Normal')
const deckCount = ref(1)
const playerCount = ref(3)

onMounted(() => {
  isHost.value = roomStore.role === 'host'
  addresses.value = roomStore.addresses || []
  port.value = roomStore.port ?? 8765
})

function startGame() {
  const humanCount = 1
  const players = Array.from({ length: playerCount.value }, (_, i) => ({ human: i < humanCount }))
  roomStore.pendingStart = {
    mode: mode.value,
    deckCount: deckCount.value,
    playerCount: playerCount.value,
    players,
  }
  router.push('/game')
}
</script>

<style scoped>
.room { max-width: 420px; margin: 2rem auto; padding: 1rem; }
h1 { text-align: center; margin-bottom: 1.5rem; }
.card { background: #2d5a3d; border-radius: 12px; padding: 1.25rem; }
.addr { font-family: monospace; font-size: 0.9rem; margin: 0.5rem 0; }
.row { margin-bottom: 0.75rem; }
.row label { display: block; margin-bottom: 0.25rem; }
.row input, .row select { width: 100%; padding: 0.5rem; border-radius: 6px; border: 1px solid #4a7c59; background: #1a472a; color: #eee; }
.btn { padding: 0.6rem 1.2rem; border-radius: 8px; border: none; cursor: pointer; margin-top: 0.5rem; }
.btn.primary { background: #4a7c59; color: #fff; }
</style>
