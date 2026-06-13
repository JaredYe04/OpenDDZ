<template>
  <div class="lobby">
    <h1>斗地主 OpenDDZ</h1>
    <div class="card opts">
      <h2>创建房间</h2>
      <div class="row">
        <label>模式</label>
        <select v-model="createMode">
          <option value="Normal">普通斗地主 (3人)</option>
          <option value="FourPlayer">四人斗地主 (2v2)</option>
        </select>
      </div>
      <div class="row">
        <label>副数</label>
        <input v-model.number="createDecks" type="number" min="1" max="4" />
      </div>
      <div class="row">
        <label>人数</label>
        <input v-model.number="createPlayers" type="number" :min="createMode === 'FourPlayer' ? 4 : 3" :max="createMode === 'FourPlayer' ? 4 : 3" />
      </div>
      <button class="btn primary" @click="createRoom">创建房间</button>
    </div>
    <div class="card opts">
      <h2>加入房间</h2>
      <div class="row">
        <label>Host IP</label>
        <input v-model="joinIP" placeholder="例如 192.168.1.100" />
      </div>
      <button class="btn" @click="joinRoom">加入</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { roomStore } from '../store/room'

const router = useRouter()
const createMode = ref<'Normal' | 'FourPlayer'>('Normal')
const createDecks = ref(1)
const createPlayers = ref(3)
const joinIP = ref('')

watch(createMode, (m) => {
  if (m === 'FourPlayer') {
    createPlayers.value = 4
    if (createDecks.value < 2) createDecks.value = 2
  } else {
    createPlayers.value = 3
  }
})

async function createRoom() {
  const api = (window as any).electronAPI
  if (!api) return
  const r = await api.createRoom({
    mode: createMode.value,
    deckCount: createDecks.value,
    playerCount: createPlayers.value,
  })
  if (r?.ok) {
    roomStore.role = 'host'
    roomStore.addresses = r.addresses || []
    roomStore.port = r.port ?? 8765
    router.push('/room')
  } else if (r?.error) {
    alert(r.error)
  }
}

async function joinRoom() {
  const api = (window as any).electronAPI
  if (!api || !joinIP.value.trim()) return
  const r = await api.joinRoom(joinIP.value.trim())
  if (r?.ok) {
    roomStore.role = 'client'
    roomStore.host = joinIP.value.trim()
    router.push('/room')
  }
}
</script>

<style scoped>
.lobby { max-width: 420px; margin: 2rem auto; padding: 1rem; }
h1 { text-align: center; margin-bottom: 1.5rem; }
.card { background: #2d5a3d; border-radius: 12px; padding: 1.25rem; margin-bottom: 1rem; }
.card h2 { margin: 0 0 1rem; font-size: 1rem; }
.row { margin-bottom: 0.75rem; }
.row label { display: block; margin-bottom: 0.25rem; font-size: 0.9rem; }
.row input, .row select { width: 100%; padding: 0.5rem; border-radius: 6px; border: 1px solid #4a7c59; background: #1a472a; color: #eee; }
.btn { padding: 0.6rem 1.2rem; border-radius: 8px; border: none; cursor: pointer; margin-top: 0.5rem; }
.btn.primary { background: #4a7c59; color: #fff; }
</style>
