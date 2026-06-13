#!/usr/bin/env node
/**
 * Iterative ML pipeline: self-play -> mine corrections -> retrain -> benchmark -> repeat.
 * Env: ROUNDS, GAMES, SELFPLAY_GAMES, TARGET_ML_VS_MC, BENCHMARK_GAMES, ORACLE_ROLLOUTS, PARALLEL, TRAIN_TREES
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
  return path.join(REPO_ROOT, 'OpenDDZ', 'bin', 'Debug', 'OpenDDZ.exe')
}

function run(cmd, args, opts = {}) {
  const r = spawnSync(cmd, args, {
    stdio: opts.capture ? 'pipe' : 'inherit',
    shell: true,
    cwd: REPO_ROOT,
    encoding: opts.capture ? 'utf8' : undefined,
    ...opts,
  })
  if (r.status !== 0) process.exit(r.status ?? 1)
  return r
}

function parseMlVsMcWinRate(output) {
  const m = output.match(/Seat\s*0\s+win rate:\s*([\d.]+)%/i)
  if (m) return parseFloat(m[1]) / 100
  return 0
}

function appendPool(sourcePath, poolPath) {
  if (!fs.existsSync(sourcePath)) return
  fs.mkdirSync(path.dirname(poolPath), { recursive: true })
  const lines = fs.readFileSync(sourcePath, 'utf8').split('\n').filter(Boolean)
  fs.appendFileSync(poolPath, lines.join('\n') + (lines.length ? '\n' : ''))
}

function modelDstPath(exe) {
  return path.join(path.dirname(exe), 'models', 'bot_model.bin')
}

function main() {
  const rounds = parseInt(process.env.ROUNDS || '3', 10)
  const games = process.env.GAMES || '10000'
  const selfPlayGames = process.env.SELFPLAY_GAMES || '2000'
  const targetWinRate = parseFloat(process.env.TARGET_ML_VS_MC || '0.40')
  const benchGames = process.env.BENCHMARK_GAMES || '500'
  const oracleRollouts = process.env.ORACLE_ROLLOUTS || '50'
  const parallel = process.env.PARALLEL || String(os.cpus().length)
  const trees = process.env.TRAIN_TREES || '80'
  const regen = process.env.REGEN === '1'

  const dataset = path.join(REPO_ROOT, 'datasets', 'train.jsonl')
  const oraclePlay = path.join(REPO_ROOT, 'datasets', 'train_oracle_play.jsonl')
  const trainPool = path.join(REPO_ROOT, 'datasets', 'train_pool.jsonl')
  const modelSrc = path.join(REPO_ROOT, 'OpenDDZ', 'models', 'bot_model.bin')
  const exe = exePath()
  const modelDst = modelDstPath(exe)

  console.log('=== Build ===')
  run('dotnet', ['build', path.join('OpenDDZ', 'OpenDDZ.csproj')])

  if (regen || !fs.existsSync(dataset)) {
    console.log('=== Generate Oracle dataset ===')
    fs.mkdirSync(path.dirname(dataset), { recursive: true })
    run(exe, [
      '--generate-dataset',
      '--games', games,
      '--oracle-rollouts', oracleRollouts,
      '--parallel', parallel,
      '--output', dataset,
      '--seed', '42',
    ])
  }

  function trainModel() {
    const trainArgs = [
      path.join('scripts', 'train-bot-model.py'),
      '--output', modelSrc,
      '--trees', trees,
    ]
    if (fs.existsSync(dataset)) trainArgs.push('--input', dataset)
    if (fs.existsSync(oraclePlay)) trainArgs.push('--input', oraclePlay)
    if (fs.existsSync(trainPool)) trainArgs.push('--input', trainPool)
    run('python', trainArgs)
    fs.mkdirSync(path.dirname(modelDst), { recursive: true })
    fs.copyFileSync(modelSrc, modelDst)
  }

  if (!fs.existsSync(modelDst)) {
    console.log('=== Initial train (no model in bin) ===')
    trainModel()
  }

  let winRate = 0
  for (let round = 0; round < rounds; round++) {
    console.log(`\n========== Iteration round ${round + 1}/${rounds} ==========`)

    const tracesPath = path.join(REPO_ROOT, 'datasets', `selfplay_traces_r${round}.jsonl`)
    const correctionsPath = path.join(REPO_ROOT, 'datasets', `selfplay_corrections_r${round}.jsonl`)

    console.log('--- Self-play ---')
    run(exe, [
      '--self-play',
      '--games', selfPlayGames,
      '--output', tracesPath,
      '--seed', String(42 + round * 10000),
    ])

    console.log('--- Mine corrections ---')
    run(exe, [
      '--mine-selfplay',
      '--input', tracesPath,
      '--output', correctionsPath,
      '--oracle-rollouts', oracleRollouts,
      '--parallel', parallel,
    ])

    appendPool(correctionsPath, trainPool)

    console.log('--- Train ---')
    trainModel()

    console.log('--- Benchmark ml_vs_mc ---')
    const bench = run(exe, [
      '--benchmark',
      '--games', benchGames,
      '--matchup', 'ml_vs_mc',
      '--seed', '42',
      '--rollouts', '15',
    ], { capture: true })

    winRate = parseMlVsMcWinRate(bench.stdout || '')
    console.log(bench.stdout || '')
    console.log(`Round ${round + 1}: ml_vs_mc seat0 win rate = ${(winRate * 100).toFixed(1)}% (target ${(targetWinRate * 100).toFixed(0)}%)`)

    if (winRate >= targetWinRate) {
      console.log('Target win rate reached.')
      break
    }
  }

  console.log('\n=== Final benchmarks ===')
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
