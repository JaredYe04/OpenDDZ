#!/usr/bin/env python3
"""
Train ML bot v2/v3: binary classifier for Oracle-best candidate, export DDZM v3 binary or legacy JSON.
"""
import argparse
import json
import struct
from pathlib import Path

import numpy as np
from sklearn.ensemble import RandomForestClassifier
from sklearn.preprocessing import StandardScaler
from sklearn.metrics import accuracy_score

FEATURE_DIM = 90
MAGIC = b"DDZM"
VERSION = 3


def load_jsonl(path):
    rows = []
    skipped = 0
    with open(path, encoding="utf-8-sig") as f:
        for line_no, line in enumerate(f, 1):
            line = line.strip()
            if not line:
                continue
            try:
                rows.append(json.loads(line))
            except json.JSONDecodeError:
                skipped += 1
                if skipped <= 3:
                    print(f"Warning: skip bad JSON at {path}:{line_no}")
    if skipped:
        print(f"Warning: skipped {skipped} invalid lines in {path}")
    return rows


def load_jsonl_files(paths):
    records = []
    seen = set()
    for path in paths:
        p = Path(path)
        if not p.exists():
            print(f"Warning: skip missing {p}")
            continue
        for rec in load_jsonl(p):
            key = (rec.get("source", p.stem), rec.get("game_id"), rec.get("player_index"), len(records))
            if key in seen:
                continue
            seen.add(key)
            rec.setdefault("weight", 1.0)
            records.append(rec)
    return records


def build_xy(records):
    X_list, y_list, w_list = [], [], []
    for rec in records:
        best = rec["best_index"]
        weight = float(rec.get("weight", 1.0))
        for i, feat in enumerate(rec["candidates"]):
            X_list.append(feat)
            y_list.append(1 if i == best else 0)
            w_list.append(weight)
    return (
        np.array(X_list, dtype=np.float32),
        np.array(y_list, dtype=np.int32),
        np.array(w_list, dtype=np.float32),
    )


def export_tree_nodes(tree):
    from sklearn.tree import _tree
    t = tree.tree_
    nodes = []
    for i in range(t.node_count):
        left = int(t.children_left[i])
        right = int(t.children_right[i])
        if left == _tree.TREE_LEAF:
            prob = float(t.value[i][0][1] / max(t.value[i][0].sum(), 1e-9))
            nodes.append({"feature": -1, "threshold": 0.0, "left": -1, "right": -1, "value": prob})
        else:
            nodes.append({
                "feature": int(t.feature[i]),
                "threshold": float(t.threshold[i]),
                "left": left,
                "right": right,
                "value": 0.0,
            })
    return {"root": 0, "nodes": nodes}


def export_binary_model(path, scaler, trees):
    """DDZM v3: header + scaler + flat trees."""
    mean = scaler.mean_.astype(np.float32)
    std = scaler.scale_.astype(np.float32)
    std[std < 1e-6] = 1.0

    with open(path, "wb") as f:
        f.write(MAGIC)
        f.write(struct.pack("<i", VERSION))
        f.write(struct.pack("<i", FEATURE_DIM))
        f.write(struct.pack("<i", len(trees)))
        f.write(mean.tobytes())
        f.write(std.tobytes())

        for tree_dict in trees:
            nodes = tree_dict["nodes"]
            f.write(struct.pack("<i", len(nodes)))
            for n in nodes:
                feat = int(n["feature"])
                f.write(struct.pack("<h", feat))
                f.write(struct.pack("<h", 0))
                f.write(struct.pack("<f", float(n["threshold"])))
                f.write(struct.pack("<i", int(n["left"])))
                f.write(struct.pack("<i", int(n["right"])))
                f.write(struct.pack("<f", float(n["value"])))


def export_json_model(path, scaler, trees):
    out = {
        "version": 2,
        "feature_names": [f"f{i}" for i in range(FEATURE_DIM)],
        "scaler": {"mean": scaler.mean_.tolist(), "std": scaler.scale_.tolist()},
        "trees": trees,
        "note": "RandomForestClassifier on Oracle labels",
    }
    path.write_text(json.dumps(out, indent=2), encoding="utf-8")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", action="append", dest="inputs", help="JSONL training file (repeatable)")
    parser.add_argument("--output", default="OpenDDZ/models/bot_model.bin")
    parser.add_argument("--format", choices=["bin", "json"], default="bin")
    parser.add_argument("--trees", type=int, default=80)
    parser.add_argument("--max-depth", type=int, default=10)
    args = parser.parse_args()

    default_inputs = [
        Path("datasets/train.jsonl"),
        Path("datasets/train_oracle_play.jsonl"),
    ]
    input_paths = [Path(p) for p in args.inputs] if args.inputs else default_inputs
    existing = [p for p in input_paths if p.exists()]
    if not existing:
        raise SystemExit(f"No dataset found among: {input_paths}")

    records = load_jsonl_files(existing)
    X, y, weights = build_xy(records)
    print(f"Samples: {len(X)} rows from {len(records)} decisions ({len(existing)} files)")

    games = sorted({r["game_id"] for r in records})
    train_games = set(games[: int(len(games) * 0.8)])

    train_idx, test_idx = [], []
    offset = 0
    for rec in records:
        n = len(rec["candidates"])
        idx_range = list(range(offset, offset + n))
        (train_idx if rec["game_id"] in train_games else test_idx).extend(idx_range)
        offset += n

    X_train, y_train, w_train = X[train_idx], y[train_idx], weights[train_idx]
    X_test, y_test = X[test_idx], y[test_idx]

    scaler = StandardScaler()
    X_train_s = scaler.fit_transform(X_train)
    X_test_s = scaler.transform(X_test) if len(X_test) else X_train_s[:0]

    clf = RandomForestClassifier(
        n_estimators=args.trees,
        max_depth=args.max_depth,
        min_samples_leaf=6,
        class_weight="balanced",
        random_state=42,
        n_jobs=-1,
    )
    clf.fit(X_train_s, y_train, sample_weight=w_train)

    if len(X_test):
        pred = clf.predict(X_test_s)
        print(f"Hold-out accuracy: {accuracy_score(y_test, pred):.3f}")
        top1 = sum(
            1 for rec in records
            if rec["game_id"] not in train_games
            and int(np.argmax(clf.predict_proba(scaler.transform(np.array(rec["candidates"], dtype=np.float32)))[:, 1])) == rec["best_index"]
        )
        total = sum(1 for rec in records if rec["game_id"] not in train_games)
        if total:
            print(f"Hold-out top-1 oracle match: {top1 / total:.3f} ({top1}/{total})")

    tree_dicts = [export_tree_nodes(est) for est in clf.estimators_]
    out_path = Path(args.output)
    out_path.parent.mkdir(parents=True, exist_ok=True)

    fmt = args.format
    if fmt == "bin" or out_path.suffix.lower() == ".bin":
        export_binary_model(out_path, scaler, tree_dicts)
    else:
        export_json_model(out_path, scaler, tree_dicts)

    size_mb = out_path.stat().st_size / (1024 * 1024)
    print(f"Model written to {out_path} ({len(tree_dicts)} trees, {size_mb:.1f} MB, format={fmt})")


if __name__ == "__main__":
    main()
