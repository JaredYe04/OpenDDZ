# OpenDDZ

斗地主游戏引擎 + Electron-Vue 桌面客户端。支持普通斗地主（3人）、四人斗地主（2v2）、单机机器人、局域网联机。

## 结构

- **OpenDDZ/** — C# 游戏引擎（.NET Framework 4.8），规则、发牌、出牌判定、叫地主等。
- **client/** — Electron + Vite + Vue 3 桌面客户端，通过 stdio 与引擎通信，支持 Host 建房、IP 加入、WebSocket 广播。

## 运行

### 1. 构建引擎

```bash
cd OpenDDZ
dotnet build OpenDDZ.csproj
```

生成 `OpenDDZ/bin/Debug/OpenDDZ.exe`（或 Release）。

### 2. 运行客户端

```bash
cd client
npm install
npm run dev
```

开发模式下，主进程会启动 `../OpenDDZ/bin/Debug/OpenDDZ.exe --stdio`。请先完成步骤 1。

### 3. 玩法

- **创建房间**：选择模式（普通/四人）、副数、人数，点击「创建房间」。Host 会看到本机 IP 和端口 8765。
- **加入房间**：在另一台机器（或本机再开一个窗口）输入 Host 的 IP，点击「加入」。
- **开始游戏**：Host 在房间页点击「开始游戏」。若只有 Host，其余位置由机器人补齐；若有其他玩家连接，则按连接顺序分配人类玩家。
- **游戏内**：手牌点击选中，再点「出牌」或「过」；叫地主时选 1分/2分/3分/不叫；四人模式中第三、第四顺位可弃一张牌或不弃。出牌限时 30 秒。

## 模式说明

- **普通斗地主**：3 人，1 副牌（可多副），叫地主、底牌、地主先出。
- **四人斗地主**：4 人 2v2，相邻组队（随机 0+1 vs 2+3 或 3+0 vs 1+2），牌堆去掉 3/4/5，至少 2 副牌。先手及下一家不弃牌；第三、第四顺位在首轮出牌前可弃一张或不弃。队友手牌明牌可见。

## 协议（引擎 stdio）

- 下行（引擎 → 客户端）：每行一个 JSON，如 `{"type":"ready"}`, `{"type":"state","playerIndex":0,"hand":["H3","S4",...]}`, `{"type":"request_play","playerIndex":0,"deadlineSec":30}`, `{"type":"request_call",...}`, `{"type":"request_discard",...}`, `{"type":"game_end",...}`, `{"type":"error","content":"..."}`。
- 上行（客户端 → 引擎）：每行一个 JSON，如 `{"cmd":"start","mode":"Normal","deckCount":1,"playerCount":3,"seed":123,"players":[{"human":true},{"human":false},{"human":false}]}`, `{"cmd":"play","cards":["H3","S4"]}`, `{"cmd":"pass"}`, `{"cmd":"call","choice":"2分"}`, `{"cmd":"discard","card":"H6"}` 或 `{"cmd":"discard"}`。

牌串格式：`H`/`S`/`D`/`C` + 点数（3–10,J,Q,K,A,2），`X`=小王，`Y`=大王。

## 自动化测试（无 UI）

从仓库根目录运行脚本，模拟“创建游戏 → 开始游戏（全机器人）”，引擎用贪心策略自动出牌，脚本打时间戳并逐行输出引擎 stdout，便于调试：

```bash
# 先构建引擎（见上文）
node scripts/run-engine-test.mjs
```

可选环境变量：`OPENDDZ_EXE` 指定引擎可执行文件路径。
