# 次元守护者2（SotM2）简体中文汉化

《Sentinels of the Multiverse 2》Steam 版非官方简体中文本地化项目。

- [ParaTranz 翻译与校对](https://paratranz.cn/projects/21585)
- [校验与同步工作流](https://github.com/fateash/SotM2-Chinese-Localization/actions)
- [问题反馈](https://github.com/fateash/SotM2-Chinese-Localization/issues)

## 当前进度

当前候选批次按卡组分文件：37 条界面文本、Legacy 27 条正文、Baron Blade 25 条正文，共 89 条。新增 45 条正文尚待实机验收。样板译文尚未完成正式语言审核；卡名、类型、风味文本和阶段模板尚未纳入运行适配。

运行基线为中文插件 0.1.4 / Steam Build 26880829，已通过重点实机测试。本仓库提供译文维护与构建工具，尚未发布完整汉化补丁，也不包含游戏本体文件或插件安装包。

原 44 条样板已完成 ParaTranz → GitHub PR → 游戏显示的单条译文往返测试（2026-11-26）。本批按卡组分组的线上迁移待完成，操作顺序见 [Batch01 迁移与验收](docs/BATCH01.md)。

## 协作与维护

在 ParaTranz 申请加入，翻译时保留占位符、富文本标签和规则数值。专有名词以项目审定结果为准，技术样板中的译名不等于最终定稿。`key` 与 `original` 由维护者管理。

1. 在 ParaTranz 翻译与校对。
2. 手动运行 **Import ParaTranz snapshot**，读取完整译文及状态。
3. 自动校验稳定键、原文、占位符、标签与规则数值，构建候选文本。
4. 有快照变化时创建 PR；维护者检查并合并，再实机验收与发布。

**Validate localization** 在提交和 PR 时自动校验。候选 artifact 仅含词典与构建报告，不是安装包。同步流程不自动合并、不自动发布，也不会回写 ParaTranz。

## 首次接入

`paratranz-project.json` 配置项目 `21585`，界面文件固定为 `3595636`；卡组文件为 `Legacy.json`、`BaronBlade.json`。已固定新文件 ID：3595650、3595651，并映射实际平台路径 `heroes/Legacy.json`、`villians/BaronBlade.json`，严格核对路径、ID 和每组键集。旧 `cards-body.json` 不再参与新流程。

在仓库 Settings → Secrets and variables → Actions 中添加 Repository secret `PARATRANZ_TOKEN`，值为 ParaTranz 账号 API Token。令牌只存入 Secrets，不写入代码、Issue 或聊天。

在 Settings → Actions → General 中允许 GitHub Actions 创建 pull request。工作流已分别声明所需权限，仓库默认权限可保持只读。随后手动运行导入工作流；无快照变化时成功结束而不创建 PR。

## 本地构建

Python 3.10 或更新版本，无第三方依赖。在仓库根目录运行（Windows 可将 `python3` 换为 `py -3`）：

```sh
python3 -m unittest discover -s tests -v
python3 tools/catalog.py build --input translations --out build --mode translated
```

输出 `build/sample-texts.json` 供 0.1.4 插件读取。`translated` 用于候选测试；`reviewed` 仅收录已审核或已锁定译文。当前样板均为已翻译，审核模式拒绝生成空词典属于预期行为。

详细操作参见 [维护说明](docs/WORKFLOW.md)；覆盖边界参见 [稳定键与覆盖规则](docs/KEYS-AND-COVERAGE.md)。

## 项目声明

本项目由玩家维护，与游戏官方无隶属关系。游戏及美术素材版权归原权利人所有。
