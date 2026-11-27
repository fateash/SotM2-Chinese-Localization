# 抽牌阶段次数不递减：Build 26880829 核查

## 结论与边界

2026-11-27 用户确认：同一个抽牌阶段内，第一张牌已抽入手牌，HUD 仍显示“抽牌阶段（剩余2次）”。2026-11-27 用户进一步完成关闭汉化对照，并明确确认这是游戏原生问题。归类为已确认的上游显示缺陷，不作为汉化回归失败。

归因已由用户实机 A/B 确认，日志及原游戏二进制静态检查提供一致的技术依据。保留原游戏次数值，汉化仅负责显示转换；不加入规则层修补。A/B 结果来源为用户明确反馈，本轮未收到额外的 A/B 日志包。

## 运行证据

- 新证据：SotM2-Dynamic-Evidence-20261127-102842-e9de65.zip。
- 安装 DLL、独立配置、词典哈希与 0.1.10-rc.1 已接受组合匹配。
- 216 条去重显示记录；出牌捕获 2、1、0 及对应中文；抽牌带后缀只捕获 `DRAW PHASE (2 left)` 和中文结果，未捕获 1 或 0。
- 去重日志不能证明原游戏没有重复写入 2，也不能用它推算实际抽牌次数；本次不据日志缺项独立认定根因。
- 上一份 101554 证据确认出牌 3→2→1→0，以及抽牌 2 的中文显示。抽牌递减不列入已通过范围。

## 原游戏静态证据

校验对象为用户提供的 GameAssembly.dll，SHA-256：`f7d7ca3479310c4e09c90849a6d65c78fdc2cf3a648fbd6bf51c48601e6ed7ee`。

| 方法／地址（RVA） | 已确认行为 |
| --- | --- |
| HUDView.UpdatePhaseLabel，0x19B82C0 | 调用 GetPhaseActionCountTotal；总次数大于 1 时调用 GetPhaseActionCountRemaining，拼接后缀并写入标签。不从已显示的中文标签解析数字。 |
| GameController.GetPhaseActionCountRemaining，0x189DB20 | 使用总次数减当前阶段模型的 PhaseActionCountUsed。 |
| DrawCardActionView.DidPerformAction，0x196F830 | 在 0x196F8A4 直接调用 UpdatePhaseLabel，说明抽牌完成后存在刷新入口。 |
| DrawPhaseController.<RunPhase>d__1.MoveNext，0x18F6A40 | 开始时取得剩余次数作为局部循环上限；逐次创建数量为 1 的抽牌激活。完成后在 0x18F7479 递增局部循环索引并返回循环判定，未在该循环同步写入 PhaseActionCountUsed。 |
| GameController.IncreasePhaseActionUsedCountIfApplicable，0x1899670 | 专门增加阶段模型的已用次数。对 il2cpp 代码段直接调用扫描仅找到出牌、失能能力、异能三条激活路径，未找到抽牌路径。 |

三个直接调用位置分别为 0x1848A92（PlayCardActivation）、0x1850552（UseIncapacitatedAbilityActivation）、0x1851509（UsePowerActivation）。直接调用扫描不等价于穷尽虚调用／内联写入，其静态分析局限不变；原生问题的实机归因由后续用户 A/B 反馈确认。

推断：抽牌循环已经推进，而 HUD 读取的阶段已用次数仍未同步，原游戏即可能反复写入相同的 `(2 left)`；本地化将该当前原文翻译为“剩余2次”。

## 已完成的实机对照（步骤留档，无需重复）

无需卸载或更换 DLL。

1. 保存可重现的局面并关闭游戏。
2. 将游戏目录 `BepInEx/config/sotm2.zh.prototype.cfg` 中 `[Prototype]` 下的 `EnableChinese = true` 改为 `false`。
3. 重启并重现同一抽牌阶段，观察第一张牌入手后的英文标签是否仍为 `DRAW PHASE (2 LEFT)`；记录第二张牌后是否正常结束阶段。
4. 关闭游戏，将该项恢复 `true` 后重启。

此开关在当前源码中会跳过中文 Harmony 挂钩安装及字体准备，也让扫描器跳过文本读写；用于对照汉化是否造成冻结。用户按此对照后确认原生问题。原生问题归因结案，未来游戏更新时可按相同步骤复查。
