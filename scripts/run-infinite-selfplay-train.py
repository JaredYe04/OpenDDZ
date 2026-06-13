#!/usr/bin/env python3
"""
Infinite self-play training loop with progress bar.
Runs until Ctrl+C. First ensures 10K Oracle dataset + base model, then iterates forever.

Usage:
  python scripts/run-infinite-selfplay-train.py

Env:
  GAMES=10000          Initial Oracle dataset size
  SELFPLAY_GAMES=2000  Self-play games per round
  TRAIN_TREES=80       RandomForest trees
  ORACLE_ROLLOUTS=50   Oracle rollouts for generate/mine
  PARALLEL=<cpu>       Parallel threads
  BENCHMARK_GAMES=200  Quick benchmark each round (0 to skip)
  MAX_ROUNDS=0         Max rounds (0 = infinite, Ctrl+C to stop)
  SKIP_DATASET=1        Skip Oracle dataset generation entirely
  SKIP_INITIAL_TRAIN=1  Skip initial train if model exists
"""
from __future__ import annotations

import os
import shutil
import signal
import subprocess
import sys
import time
from pathlib import Path

try:
    from tqdm import tqdm
except ImportError:
    tqdm = None

REPO = Path(__file__).resolve().parent.parent
EXE = Path(os.environ.get("OPENDDZ_EXE", REPO / "OpenDDZ" / "bin" / "Debug" / "OpenDDZ.exe"))
MODEL_SRC = REPO / "OpenDDZ" / "models" / "bot_model.bin"
DATASET = REPO / "datasets" / "train.jsonl"
ORACLE_PLAY = REPO / "datasets" / "train_oracle_play.jsonl"
TRAIN_POOL = REPO / "datasets" / "train_pool.jsonl"
TRAIN_SCRIPT = REPO / "scripts" / "train-bot-model.py"

stop_requested = False


def on_sigint(sig, frame):
    global stop_requested
    stop_requested = True
    print("\n收到停止信号，本轮结束后退出…")


signal.signal(signal.SIGINT, on_sigint)
if hasattr(signal, "SIGTERM"):
    signal.signal(signal.SIGTERM, on_sigint)


def run(cmd, *, cwd=REPO, check=True):
    print(f"  $ {' '.join(str(c) for c in cmd)}")
    r = subprocess.run(cmd, cwd=str(cwd))
    if check and r.returncode != 0:
        sys.exit(r.returncode)
    return r.returncode


def parse_bench_win_rate(output: str) -> float:
    for line in output.splitlines():
        if "Seat 0 win rate:" in line:
            pct = line.split(":")[1].strip().split()[0].rstrip("%")
            try:
                return float(pct) / 100.0
            except ValueError:
                pass
    return 0.0


def ensure_built():
    if os.environ.get("SKIP_BUILD", "0") == "1":
        if not EXE.exists():
            print(f"SKIP_BUILD=1 但找不到 {EXE}")
            sys.exit(1)
        print(f"跳过构建，使用 {EXE}")
        return
    run(["dotnet", "build", str(REPO / "OpenDDZ" / "OpenDDZ.csproj")])


def model_dst_path() -> Path:
    return EXE.parent / "models" / "bot_model.bin"


def ensure_dataset(games: int, rollouts: int, parallel: int):
    if os.environ.get("SKIP_DATASET", "0") == "1":
        if DATASET.exists():
            lines = sum(1 for _ in open(DATASET, encoding="utf-8"))
            print(f"跳过数据集生成，使用现有: {DATASET} ({lines} 行)")
            return
        # 尝试合并 shard
        shards = list(DATASET.parent.glob(DATASET.name + ".part*"))
        if shards:
            print(f"发现 {len(shards)} 个 shard，尝试合并…")
            parallel = min(max(1, parallel), 20)
            run([
                str(EXE), "--merge-shards",
                "--parallel", str(parallel),
                "--output", str(DATASET),
            ])
            return
        print("SKIP_DATASET=1 但未找到 train.jsonl 或 shard 文件")
        sys.exit(1)

    min_lines = max(1000, games // 2)
    if DATASET.exists():
        lines = sum(1 for _ in open(DATASET, encoding="utf-8"))
        if lines >= min_lines:
            print(f"数据集已存在: {DATASET} ({lines} 行)，跳过生成")
            return
    DATASET.parent.mkdir(parents=True, exist_ok=True)
    parallel = min(max(1, parallel), 16)
    run([
        str(EXE), "--generate-dataset",
        "--games", str(games),
        "--oracle-rollouts", str(rollouts),
        "--parallel", str(parallel),
        "--output", str(DATASET),
        "--seed", "42",
    ])


def train_model(trees: int):
    args = [sys.executable, str(TRAIN_SCRIPT), "--output", str(MODEL_SRC), "--trees", str(trees)]
    if DATASET.exists():
        args.extend(["--input", str(DATASET)])
    if ORACLE_PLAY.exists():
        args.extend(["--input", str(ORACLE_PLAY)])
    if TRAIN_POOL.exists():
        args.extend(["--input", str(TRAIN_POOL)])
    run(args)
    dst = model_dst_path()
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(MODEL_SRC, dst)


def self_play(games: int, seed: int, out: Path):
    run([
        str(EXE), "--self-play",
        "--games", str(games),
        "--output", str(out),
        "--seed", str(seed),
    ])


def mine_corrections(inp: Path, out: Path, rollouts: int, parallel: int):
    run([
        str(EXE), "--mine-selfplay",
        "--input", str(inp),
        "--output", str(out),
        "--oracle-rollouts", str(rollouts),
        "--parallel", str(parallel),
    ])


def benchmark(games: int) -> float:
    r = subprocess.run(
        [str(EXE), "--benchmark", "--games", str(games), "--matchup", "ml_vs_mc", "--seed", "42", "--rollouts", "15"],
        cwd=str(REPO),
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    print(r.stdout)
    return parse_bench_win_rate(r.stdout)


def append_pool(src: Path):
    if not src.exists():
        return
    TRAIN_POOL.parent.mkdir(parents=True, exist_ok=True)
    with open(src, encoding="utf-8") as f:
        lines = [ln for ln in f if ln.strip()]
    if lines:
        with open(TRAIN_POOL, "a", encoding="utf-8") as f:
            f.write("\n".join(lines) + "\n")


def main():
    games = int(os.environ.get("GAMES", "10000"))
    selfplay_games = int(os.environ.get("SELFPLAY_GAMES", "2000"))
    trees = int(os.environ.get("TRAIN_TREES", "80"))
    rollouts = int(os.environ.get("ORACLE_ROLLOUTS", "50"))
    parallel = int(os.environ.get("PARALLEL", str(os.cpu_count() or 4)))
    bench_games = int(os.environ.get("BENCHMARK_GAMES", "200"))

    if not EXE.exists():
        print(f"找不到引擎: {EXE}\n请先 dotnet build")
        sys.exit(1)

    print("=== 构建引擎 ===")
    ensure_built()

    print(f"=== 确保 {games} 条 Oracle 数据集 ===")
    ensure_dataset(games, rollouts, parallel)

    skip_initial = os.environ.get("SKIP_INITIAL_TRAIN", "0") == "1"
    model_dst = model_dst_path()
    if skip_initial and model_dst.exists():
        print(f"=== 跳过初始训练（已有模型 {model_dst}） ===")
    else:
        print("=== 初始训练（合并 train + train_oracle_play） ===")
        train_model(trees)

    round_num = 0
    max_rounds = int(os.environ.get("MAX_ROUNDS", "0"))  # 0 = 无限
    use_tqdm = tqdm is not None
    pbar = tqdm(desc="自对抗训练", unit="轮", dynamic_ncols=True) if use_tqdm else None

    while not stop_requested:
        if max_rounds > 0 and round_num >= max_rounds:
            break
        round_num += 1
        t0 = time.time()
        traces = REPO / "datasets" / f"selfplay_r{round_num}.jsonl"
        corrections = REPO / "datasets" / f"corrections_r{round_num}.jsonl"

        if pbar is not None:
            pbar.set_description(f"第 {round_num} 轮 | 自对弈")
        else:
            print(f"\n========== 第 {round_num} 轮 ==========")

        self_play(selfplay_games, 42 + round_num * 10000, traces)

        if stop_requested:
            break

        if pbar is not None:
            pbar.set_description(f"第 {round_num} 轮 | 挖掘纠错")
        mine_corrections(traces, corrections, rollouts, parallel)
        append_pool(corrections)

        if stop_requested:
            break

        if pbar is not None:
            pbar.set_description(f"第 {round_num} 轮 | 重训模型")
        train_model(trees)

        win_rate = 0.0
        if bench_games > 0:
            if pbar is not None:
                pbar.set_description(f"第 {round_num} 轮 | 基准测试")
            win_rate = benchmark(bench_games)

        elapsed = time.time() - t0
        msg = f"轮次={round_num} ml_vs_mc={win_rate:.1%} 耗时={elapsed:.0f}s pool={TRAIN_POOL.stat().st_size // 1024 if TRAIN_POOL.exists() else 0}KB"

        if pbar is not None:
            pbar.update(1)
            pbar.set_postfix_str(msg)
        else:
            print(msg)

    if pbar is not None:
        pbar.close()
    print(f"\n已停止，共完成 {round_num} 轮。模型: {model_dst_path()}")


if __name__ == "__main__":
    main()
