#!/usr/bin/env node
/**
 * Run bot benchmark: build OpenDDZ and execute --benchmark mode.
 * Env: GAMES, MODE, MATCHUP, SEED, ROLLOUTS, OPENDDZ_EXE
 */
import { spawnSync } from 'node:child_process'
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
  return exe
}

function main() {
  const build = spawnSync('dotnet', ['build', path.join(REPO_ROOT, 'OpenDDZ', 'OpenDDZ.csproj')], {
    cwd: REPO_ROOT,
    stdio: 'inherit',
    shell: true,
  })
  if (build.status !== 0) process.exit(build.status ?? 1)

  const games = process.env.GAMES || '100'
  const mode = process.env.MODE || 'Normal'
  const matchup = process.env.MATCHUP || 'greedy_all'
  const seed = process.env.SEED || '42'
  const rollouts = process.env.ROLLOUTS || '15'

  const exe = getEnginePath()
  if (!fs.existsSync(exe)) {
    console.error(`Engine not found: ${exe}`)
    process.exit(1)
  }

  const args = [
    '--benchmark',
    '--games', games,
    '--mode', mode,
    '--matchup', matchup,
    '--seed', seed,
    '--rollouts', rollouts,
  ]

  console.log(`Running: ${exe} ${args.join(' ')}`)
  const run = spawnSync(exe, args, { cwd: path.dirname(exe), stdio: 'inherit' })
  process.exit(run.status ?? 0)
}

main()
