<template>
  <div
    class="game-card"
    :class="{ selected, hint: isHint }"
    :data-card="card"
    @click="$emit('click', card)"
  >
    <span class="suit" :class="suitClass">{{ suitSymbol }}</span>
    <span class="rank" :class="rankColor">{{ rankLabel }}</span>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(
  defineProps<{
    card: string
    selected?: boolean
    isHint?: boolean
  }>(),
  { selected: false, isHint: false }
)

defineEmits<{ (e: 'click', card: string): void }>()

const suitClass = computed(() => {
  const s = props.card.charAt(0)
  if (s === 'H') return 'heart'
  if (s === 'S') return 'spade'
  if (s === 'D') return 'diamond'
  if (s === 'C') return 'club'
  return 'joker'
})

const suitSymbol = computed(() => {
  const s = props.card.charAt(0)
  if (s === 'H') return '♥'
  if (s === 'S') return '♠'
  if (s === 'D') return '♦'
  if (s === 'C') return '♣'
  if (props.card === 'X') return 'J'
  if (props.card === 'Y') return 'J'
  return ''
})

const rankLabel = computed(() => {
  if (props.card === 'X') return '小'
  if (props.card === 'Y') return '大'
  return props.card.slice(1) || ''
})

const rankColor = computed(() => {
  const s = props.card.charAt(0)
  if (s === 'H' || s === 'D') return 'red'
  return 'black'
})
</script>

<style scoped>
.game-card {
  width: 52px;
  min-width: 52px;
  height: 72px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  background: #fff;
  border-radius: 6px;
  border: 2px solid #ccc;
  cursor: pointer;
  font-size: 0.75rem;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.2);
  transition: transform 0.1s, border-color 0.1s, box-shadow 0.1s;
}
.game-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 2px 6px rgba(0, 0, 0, 0.25);
}
.game-card.selected {
  border-color: #2e7d32;
  background: #e8f5e9;
  transform: translateY(-6px);
  box-shadow: 0 4px 8px rgba(0, 0, 0, 0.25);
}
.game-card.hint {
  border-color: #f9a825;
  background: #fff8e1;
}
.suit {
  font-size: 1.1rem;
  line-height: 1;
}
.rank {
  font-weight: 700;
  margin-top: 2px;
}
.rank.red {
  color: #c62828;
}
.rank.black {
  color: #212121;
}
.suit.heart,
.suit.diamond {
  color: #c62828;
}
.suit.spade,
.suit.club {
  color: #212121;
}
.suit.joker {
  color: #5e35b1;
}
</style>
