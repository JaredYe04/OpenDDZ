import { createRouter, createWebHashHistory } from 'vue-router'

const router = createRouter({
  history: createWebHashHistory(),
  routes: [
    { path: '/', name: 'Lobby', component: () => import('../views/Lobby.vue') },
    { path: '/room', name: 'Room', component: () => import('../views/Room.vue') },
    { path: '/game', name: 'Game', component: () => import('../views/Game.vue') },
  ],
})

export default router
