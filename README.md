# SotM2 Chinese Localization

《Sentinels of the Multiverse 2》Steam 版中文本地化，ParaTranz 项目 [21585](https://paratranz.cn/projects/21585)。

ParaTranz 与 GitHub 已完成全量同步，当前文本库存为 48 个文件、2,444 条原文，英雄、反派、环境按卡组分组。新增内容保持未翻译；已有 89 条译文保留。字体与显示运行支持候选版本为 **0.1.6-rc.1**，适用 Steam Build 26880829，尚待新的 Windows 实机回归；不是正式发行版。

- `inventory-import/`：原文导入文件。已有文件更新原文，不覆盖译文或删除重建。
- `inventory-translations/`：完整 ParaTranz 译文快照。
- `baseline/inventory.json`：稳定键、原文哈希、文件归属及显示映射。
- `paratranz-inventory.json`：文件 ID 与路径；保留已存在的 `villians/` 拼写。
- `runtime/`：卡牌身份及显示类型限定的运行支持源码。

新增译文通过 `python3 tools/inventory.py --out build` 构建。未翻译、审核状态不符合要求、显示路径未启用的条目不会输出到游戏词典。完整库存中的 524 条暂存项不会因已有译文而自动启用。

在 Actions 中运行 **Import ParaTranz snapshot**：首次扩容先选 `plan` 检查，再选 `migrate` 仅更新原文；后续使用 `sync` 读取译文并创建审核 PR。迁移使用已配置的 `PARATRANZ_TOKEN` secret，不需要把令牌写入文件。不同操作均为手动触发，不自动合并或发布。

构建器拒绝测试标记，不改游戏规则模型、内部标识与数值。开发占位符、程序日志和框架常量不作为待译正文。排版阶段暂不增加人工换行。

详见 [完整库存与显示适配说明](docs/INVENTORY-0.2.0.md)。旧 `tools/catalog.py`、`translations/` 和 `paratranz-project.json` 保留用于 0.1.4 回归，不再作为完整库存的生产入口。
