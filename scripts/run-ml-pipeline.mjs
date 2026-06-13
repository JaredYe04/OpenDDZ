#!/usr/bin/env node
/**
 * Full ML pipeline: generate Oracle dataset -> train -> copy model -> run benchmarks.
 * Env: GAMES, ORACLE_ROLLOUTS, PARALLEL, TRAIN_TREES, BENCHMARK_GAMES, OPENDDZ_EXE
 */
import { spawnSync } from 'node:child_process'
import path from 'node:path'
import fs from 'node:fs'
import os from 'node:os'
import { fileURLToPath } from 'node:url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const REPO_ROOT = path.resolve(__dirname, '..')

function exePath() {
  if (process.env.OPENDDZ_EXE && fs.existsSync(process.env.OPENDDZ_EXE))
    return process.env.OPENDDZ_EXE
  const p = path.join(REPO_ROOT, 'OpenDDZ', 'bin', 'Debug', 'OpenDDZ.exe')
  return p
}

function run(cmd, args, opts = {}) {
  const r = spawnSync(cmd, args, { stdio: 'inherit', shell: true, cwd: REPO_ROOT, ...opts })
  if (r.status !== 0) process.exit(r.status ?? 1)
}

function main() {
  const games = process.env.GAMES || '10000'
  const oracleRollouts = process.env.ORACLE_ROLLOUTS || '50'
  const parallel = process.env.PARALLEL || String(os.cpus().length)
  const trees = process.env.TRAIN_TREES || '80'
  const benchGames = process.env.BENCHMARK_GAMES || '500'
  const dataset = path.join(REPO_ROOT, 'datasets', 'train.jsonl')
  const oraclePlay = path.join(REPO_ROOT, 'datasets', 'train_oracle_play.jsonl')
  const modelSrc = path.join(REPO_ROOT, 'OpenDDZ', 'models', 'bot_model.bin')

  console.log('=== Step 1: Build ===')
  run('dotnet', ['build', path.join('OpenDDZ', 'OpenDDZ.csproj')])

  const exe = exePath()
  const modelDst = path.join(path.dirname(exe), 'models', 'bot_model.bin')
  console.log('=== Step 2: Generate Oracle dataset ===')
  fs.mkdirSync(path.dirname(dataset), { recursive: true })
  run(exe, [
    '--generate-dataset',
    '--games', games,
    '--oracle-rollouts', oracleRollouts,
    '--parallel', parallel,
    '--output', dataset,
    '--seed', '42',
  ])

  console.log('=== Step 3: Train model (merged JSONL) ===')
  const trainArgs = [
    path.join('scripts', 'train-bot-model.py'),
    '--output', modelSrc,
    '--trees', trees,
  ]
  if (fs.existsSync(dataset)) trainArgs.push('--input', dataset)
  if (fs.existsSync(oraclePlay)) trainArgs.push('--input', oraclePlay)
  run('python', trainArgs)

  console.log('=== Step 4: Copy model to bin ===')
  fs.mkdirSync(path.dirname(modelDst), { recursive: true })
  fs.copyFileSync(modelSrc, modelDst)

  console.log('=== Step 5: Benchmarks ===')
  for (const matchup of ['greedy_all', 'mc_vs_greedy', 'ml_vs_greedy', 'ml_vs_mc']) {
    console.log(`\n--- ${matchup} ---`)
    run(exe, [
      '--benchmark',
      '--games', benchGames,
      '--matchup', matchup,
      '--seed', '42',
      '--rollouts', '15',
    ])
  }
}

main()
