#!/usr/bin/env node
/**
 * Automated engine test (Plan A): spawn OpenDDZ --stdio, send start (all bots),
 * read stdout with timestamped logs until game_end or process exit.
 * Matches UI flow: create-room => start-game with same payload.
 * Run from repo root: node scripts/run-engine-test.mjs
 * Requires: OpenDDZ/bin/Debug/OpenDDZ.exe built first.
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
  const exe = path.join(REPO_ROOT, 'OpenDDZ', 'bin', 'Debug', 'OpenDDZ.exe')
  if (fs.existsSync(exe)) return exe
  const alt = path.join(REPO_ROOT, 'OpenDDZ', 'bin', 'Debug', 'OpenDDZ')
  if (fs.existsSync(alt)) return alt
  return exe
}

function ts() {
  return new Date().toISOString()
}

function logOut(line) {
  console.log(`[${ts()}] OUT: ${line}`)
  try {
    const msg = JSON.parse(line)
    const t = msg.type
    if (t) console.log(`[${ts()}] EVT: type=${t}${msg.playerIndex != null ? ` playerIndex=${msg.playerIndex}` : ''}${msg.requestId ? ` requestId=${msg.requestId}` : ''}${msg.lastPlayerIndex != null ? ` lastPlayerIndex=${msg.lastPlayerIndex}` : ''}`)
  } catch (_) {}
}

function logIn(line) {
  console.log(`[${ts()}] IN:  ${line}`)
}

function logErr(line) {
  console.error(`[${ts()}] ERR: ${line}`)
}

function main() {
  const exe = getEnginePath()
  if (!fs.existsSync(exe)) {
    console.error(`Engine not found. Build first: dotnet build OpenDDZ/OpenDDZ.csproj`)
    console.error(`Expected: ${exe}`)
    process.exit(1)
  }

  const child = spawn(exe, ['--stdio'], {
    cwd: path.dirname(exe),
    stdio: ['pipe', 'pipe', 'pipe'],
    windowsHide: true,
  })

  let stdoutBuf = ''
  child.stdout.setEncoding('utf8')
  child.stdout.on('data', (chunk) => {
    stdoutBuf += chunk
    const lines = stdoutBuf.split('\n')
    stdoutBuf = lines.pop() ?? ''
    lines.forEach((l) => { if (l.trim()) logOut(l.trim()) })
  })

  child.stderr.setEncoding('utf8')
  child.stderr.on('data', (data) => logErr(data.toString().trim()))

  child.on('error', (err) => {
    logErr(`Process error: ${err.message}`)
    process.exit(1)
  })

  child.on('exit', (code, signal) => {
    if (stdoutBuf.trim()) logOut(stdoutBuf.trim())
    console.log(`[${ts()}] EXIT: code=${code} signal=${signal}`)
    process.exit(code ?? 0)
  })

  const startCmd = {
    cmd: 'start',
    mode: 'Normal',
    deckCount: 1,
    playerCount: 3,
    seed: Date.now(),
    players: [
      { human: false },
      { human: false },
      { human: false },
    ],
  }
  const startLine = JSON.stringify(startCmd)
  child.stdin.write(startLine + '\n')
  logIn(startLine)
  child.stdin.end()
}

main()
