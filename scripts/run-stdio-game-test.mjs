#!/usr/bin/env node
/**
 * Stdio mode multi-round automated game test.
 * Simulates a human client: responds to request_call / request_play until game_end.
 * Run: node scripts/run-stdio-game-test.mjs [--rounds N]
 */
import { spawn } from 'node:child_process'
import path from 'node:path'
import fs from 'node:fs'
import { fileURLToPath } from 'node:url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const REPO_ROOT = path.resolve(__dirname, '..')

function getEnginePath() {
  if (process.env.OPENDDZ_EXE && fs.existsSync(process.env.OPENDDZ_EXE))
    return process.env.OPENDDZ_EXE
  return path.join(REPO_ROOT, 'OpenDDZ', 'bin', 'Debug', 'OpenDDZ.exe')
}

function ts() {
  return new Date().toISOString()
}

function rankValue(card) {
  const r = card.slice(1)
  const map = { '3': 3, '4': 4, '5': 5, '6': 6, '7': 7, '8': 8, '9': 9, '10': 10, J: 11, Q: 12, K: 13, A: 14, '2': 15, X: 16, Y: 17 }
  return map[r] ?? 0
}

function pickPlay(hand, lastCards) {
  if (!lastCards || lastCards.length === 0) {
    const sorted = [...hand].sort((a, b) => rankValue(a) - rankValue(b))
    return sorted.length ? [sorted[0]] : null
  }
  // 跟牌时保守 pass，避免无效出牌死循环
  return null
}

async function runOneGame(exe, round, humanSeat, timeoutMs = 45000) {
  return new Promise((resolve, reject) => {
    const child = spawn(exe, ['--stdio'], {
      cwd: path.dirname(exe),
      stdio: ['pipe', 'pipe', 'pipe'],
      windowsHide: true,
    })

    let stdoutBuf = ''
    let hand = []
    let lastCards = []
    let gameEnded = false
    let callCount = 0
    let playCount = 0
    let forcePass = false
    const seenRequests = new Set()
    const errors = []
    const events = []

    const timer = setTimeout(() => {
      child.kill()
      resolve({ round, code: -1, gameEnded, callCount, playCount, errors: [...errors, 'timeout'], events })
    }, timeoutMs)

    const writeIn = (obj) => {
      child.stdin.write(JSON.stringify(obj) + '\n')
    }

    child.stdout.setEncoding('utf8')
    child.stdout.on('data', (chunk) => {
      stdoutBuf += chunk
      const lines = stdoutBuf.split('\n')
      stdoutBuf = lines.pop() ?? ''
      for (const line of lines) {
        if (!line.trim()) continue
        let msg
        try { msg = JSON.parse(line) } catch { continue }
        events.push(msg.type)

        if (msg.type === 'error') errors.push(msg.content)
        if (msg.type === 'play_rejected') forcePass = true

        if (msg.type === 'state' && msg.playerIndex === humanSeat) {
          hand = msg.hand || []
        }
        if (msg.type === 'last_move') {
          lastCards = msg.isFirstHand ? [] : (msg.lastCards || [])
        }
        if (msg.type === 'request_call' && msg.playerIndex === humanSeat) {
          callCount++
          writeIn({ cmd: 'call', requestId: msg.requestId, choice: '1分' })
        }
        if (msg.type === 'request_play' && msg.playerIndex === humanSeat) {
          playCount++
          if (seenRequests.has(msg.requestId)) return
          seenRequests.add(msg.requestId)
          if (forcePass) {
            forcePass = false
            writeIn({ cmd: 'pass', requestId: msg.requestId })
            return
          }
          const cards = pickPlay(hand, lastCards)
          if (cards) writeIn({ cmd: 'play', requestId: msg.requestId, cards })
          else writeIn({ cmd: 'pass', requestId: msg.requestId })
        }
        if (msg.type === 'game_end') {
          gameEnded = true
        }
      }
    })

    child.stderr.setEncoding('utf8')
    child.stderr.on('data', (d) => errors.push(d.toString().trim()))

    child.on('error', reject)
    child.on('exit', (code) => {
      clearTimeout(timer)
      resolve({ round, code, gameEnded, callCount, playCount, errors, events })
    })

    const startCmd = {
      cmd: 'start',
      mode: 'Normal',
      deckCount: 1,
      playerCount: 3,
      seed: 1000 + round,
      players: [
        { human: humanSeat === 0 },
        { human: humanSeat === 1 },
        { human: humanSeat === 2 },
      ],
    }
    child.stdin.write(JSON.stringify(startCmd) + '\n')
  })
}

async function main() {
  const rounds = parseInt(process.argv.find((a, i) => process.argv[i - 1] === '--rounds') || process.env.STDIO_ROUNDS || '3', 10)
  const exe = getEnginePath()
  if (!fs.existsSync(exe)) {
    console.error(`Engine not found: ${exe}\nRun: dotnet build OpenDDZ/OpenDDZ.csproj`)
    process.exit(1)
  }

  console.log(`[${ts()}] Stdio multi-round test: ${rounds} games, exe=${exe}`)
  let failed = 0

  for (let r = 0; r < rounds; r++) {
    const humanSeat = r % 3
    const result = await runOneGame(exe, r + 1, humanSeat)
    const ok = result.gameEnded && result.code === 0
    console.log(
      `[${ts()}] Round ${result.round} seat${humanSeat}: ` +
      `game_end=${result.gameEnded} calls=${result.callCount} plays=${result.playCount} ` +
      `exit=${result.code} ${ok ? 'OK' : 'FAIL'}`
    )
    if (result.errors.length) console.log('  errors:', result.errors.slice(0, 3).join('; '))
    if (!ok && result.events) console.log('  events:', result.events.slice(0, 20).join(' -> '))
    if (!ok) failed++
  }

  console.log(`[${ts()}] Done: ${rounds - failed}/${rounds} passed`)
  process.exit(failed > 0 ? 1 : 0)
}

main().catch((e) => {
  console.error(e)
  process.exit(1)
})
