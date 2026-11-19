**Read this in other languages:**
[English](README_en.md) | [中文](README.md)

# MatchPauseManager-for-CS2

CS2 版的**战术暂停 / 技术暂停**比赛管理插件，是 [MatchPauseManager-for-CSGO](https://github.com/A1rLeSS/MatchPauseManager-for-CSGO)（SourceMod / SourcePawn，V1.2.6）的移植版本。

> **重要**：SourceMod 不支持 CS2，因此本版本是基于 **Metamod:Source 2.0 + CounterStrikeSharp（C#/.NET 10）** 的**重写式移植**，不是把 `.sp` 编译一下就能用的直接移植。

## 功能

### 战术暂停（Tactical Pause）
- 指令：`!pause` / `!tac` / `!p`
- 默认每队 **3** 次、每次 **30 秒**，结束前 **5** 秒给出警告
- 同一回合每队只能生效一次；进入加时赛后每队刷新为 **1** 次，并在每个新加时阶段刷新（会聊天提示）
- 倒计时显示在屏幕中央 HUD，结束时自动恢复比赛
- 仅**发起方队伍**或**裁判**可以提前解除

### 技术暂停（Technical Pause）
- 玩家 `!tech` = 队伍发起；服务器控制台 / rcon `mp_pause_match` = **裁判**发起
- **伤害判定**：本回合**未产生伤害**时，立即用 `CCSGameRules.TerminateRound()` 作废当前回合，在随之而来的冻结时间内暂停，并**自动回溯到回合开始前的状态**（无需裁判确认）；本回合**已产生伤害**时，先打完整回合，随后在冻结时间暂停并标记为无效，由**裁判确认**是否回溯
- 裁判发起与队伍发起使用不同文案；裁判发起的暂停只有裁判能恢复
- 等待期间可用 `!unpause` / `mp_unpause_match` 取消请求
- 暂停生效后每 1 秒校验一次游戏暂停状态，避免"插件认为已暂停、服务器还在跑"

### 回合回溯（Rewind）
- 每回合开始快照：**比分、回合计数、每名玩家的经济、生命、护甲、头盔、拆弹器、武器、MVP/得分**
- **无伤害作废的回合会自动回溯**；**已有伤害的回合由裁判确认回溯**（`mp_rewind_match` 控制台 / rcon，或管理员游戏内 `!rewind`）
- 回溯写入比分、回合计数（无效回合不占回合数）、经济、护甲/生命/装备，并**尽力**清理该回合的每回合统计数据
- 无论成功与否都会在聊天框明确提示，服务器日志另有 `[MPM] rewind verified=...` 记录

### 文案风格
`textStyle` 三套风格，参照真实赛事转播/裁判台用词，聊天与 HUD 同步：

| 值 | 风格 |
| --- | --- |
| `0` | 国际赛事（英文，默认） |
| `1` | 中文赛事 |
| `2` | 国际赛事 + 规则提醒（暂停期间禁止交流等） |

## 安装

1. 服务器安装 **Metamod:Source 2.0** 与 **CounterStrikeSharp**（本插件针对 CounterStrikeSharp `1.0.380` / .NET 10 构建）。
2. 将 `MatchPauseManager` 文件夹放到
   `game/csgo/addons/counterstrikesharp/plugins/MatchPauseManager/`
3. 重启服务器，或在服务器控制台执行 `css_plugins reload`。
4. 首次加载会在插件目录生成 `config.json`。

## 指令

| 指令 | 谁能用 | 说明 |
| --- | --- | --- |
| `!pause` / `!tac` / `!p` | 玩家 | 发起战术暂停 |
| `!tech` | 玩家 | 发起技术暂停（队伍发起） |
| `!unpause` | 发起方队伍 / 裁判 | 解除或取消暂停 |
| `!rewind` | 裁判（`@css/root`） | 回溯被判定无效的回合 |
| `mp_pause_match` | 服务器控制台 / rcon | 裁判技术暂停 |
| `mp_unpause_match` | 服务器控制台 / rcon | 裁判解除暂停 |
| `mp_rewind_match` | 服务器控制台 / rcon | 裁判回溯 |
| `css_pause` / `css_tech` / `css_unpause` / `css_rewind` | 控制台或管理员 | 以上指令的控制台形式 |

## 配置（config.json）

```json
{
  "tacticalTime": 30,
  "tacticalCount": 3,
  "overtimeCount": 1,
  "technicalDamageGate": true,
  "rewindEnabled": true,
  "rewindPlayerStats": true,
  "textStyle": 0,
  "warnChatDuringTechnical": true
}
```

| 字段 | 默认 | 说明 |
| --- | --- | --- |
| `tacticalTime` | `30` | 战术暂停时长（秒，1–300） |
| `tacticalCount` | `3` | 常规赛每队战术暂停次数 |
| `overtimeCount` | `1` | 每个加时阶段每队次数 |
| `technicalDamageGate` | `true` | `true` = 按伤害判定；`false` = 技术暂停总是判定当前回合无效 |
| `rewindEnabled` | `true` | 是否允许裁判回溯 |
| `rewindPlayerStats` | `true` | 回溯时是否清理无效回合的每回合统计 |
| `textStyle` | `0` | 文案风格 `0`/`1`/`2` |
| `warnChatDuringTechnical` | `true` | 技术暂停期间是否警告发言玩家 |

## 与原 CS:GO 版本的差异（CS2 限制）

1. **暂停时机**：CS2 的 `mp_pause_match` 只在下个冻结时间生效，无法把进行中的回合原地冻结。插件因此用 `CCSGameRules.TerminateRound()` **立即结束回合**来制造冻结时间，所以"无伤害 → 作废 + 回溯 + 暂停"在 CS2 中与 CS:GO 版行为一致；"已伤害"仍必须等回合自然结束。
2. **回溯机制**：CS2 没有 CS:GO 的 `mp_backup_restore_load_file` 回合备份体系，本插件改为**自己快照 + 直接写 schema 成员**（比分、回合计数、经济、装备、生命）。
3. **选手 K/D/A**：CS2 的累计击杀/死亡/助攻在客户端侧，服务端没有可写字段；插件只能尽力清除无效回合的**每回合统计**（`rewindPlayerStats`）。比分/经济/装备/回合计数的回溯是准确的。
4. 换边与队伍名：沿用 V1.2.4 起的做法，用 `m_totalRoundsPlayed` 推导"阵营 → 队伍"映射，换边后不会显示换边前的队伍名。

## 构建

```bash
dotnet build -c Release
```

产物：`bin/Release/net10.0/MatchPauseManager.dll`，放入
`game/csgo/addons/counterstrikesharp/plugins/MatchPauseManager/`。

依赖通过 NuGet 获取：`CounterStrikeSharp.API` `1.0.380`（目标框架 `net10.0`）。

## 许可

[PolyForm Noncommercial License 1.0.0](LICENSE.md)
