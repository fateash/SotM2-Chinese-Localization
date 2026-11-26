# 次元守护者 SotM2 本地化工具链 0.1.0

本包是 GitHub/ParaTranz 协作工程，运行基线为已通过重点实机测试的插件 0.1.4 / Steam Build 26880829。它不是新的 DLL 安装包。

本轮已完成 37 项界面、7 条卡文的离线往返转换。输出与现有样板词典逐字节一致。线上 ParaTranz 导入/导出和 GitHub Actions 尚待接入项目后验证。

## 文件分工

| 文件/目录 | 用途 |
|---|---|
| `baseline/registry.json` | 稳定键、原文、原文指纹、控件名/卡牌 ID、字段类别；维护者管理 |
| `translations/` | 从 ParaTranz 拉回的译文快照；GitHub 记录版本 |
| `paratranz-import/ui.json` | 首次导入 ParaTranz 的 37 项界面文本 |
| `paratranz-import/cards-body.json` | 首次导入 ParaTranz 的 7 条正文样板 |
| `build/sample-texts.json` | 0.1.4 实际读取的格式；由转换器生成 |
| `build/build-report.json` | 收录/跳过条目、输入输出指纹及构建模式 |
| `.github/workflows/` | 校验构建与手动拉取后创建 PR 的工作流 |
| `docs/baseline-acceptance.json` | 0.1.4 重点实机验收范围记录 |

卡名、类型、风味文本、阶段模板尚未纳入本轮运行适配。不要仅在 ParaTranz 增加这些条目就期待它们在游戏中显示；转换器会拒绝未登记键与未支持字段。

## 现在可以做什么

首次在 ParaTranz 项目的文件管理中导入 `paratranz-import` 下两个 JSON，保留文件名，勿上传 `baseline` 或游戏用的 `sample-texts.json`。译者编辑 translation；key/original 由源文本维护流程控制。当前样板译文只是技术验证样本，未被自动标成“已审核”，也没有把人名审定结果混入样板。

ParaTranz 项目及文件编号已配置。可使用本工程查看数据与离线构建。无需重新安装或替换 0.1.4 DLL。

## 本地构建

要求 Python 3.10 或更新版本，无第三方 Python 依赖。在工程根目录执行；Windows 可把 `python3` 换成 `py -3`，或运行配套 CMD。

```sh
python3 -m unittest discover -s tests -v
python3 tools/catalog.py export --input translations --out paratranz-import
python3 tools/catalog.py build --input translations --out build --mode translated
```

模式为明确的状态名单：

| 模式 | 采用的 ParaTranz 状态 | 用途 |
|---|---|---|
| `pilot` | 无 stage、1、3、5、9 | 本地格式往返测试，不能当作审核通过 |
| `translated` | 1 已翻译、3 已检查、5 已审核、9 已锁定 | 候选测试包 |
| `reviewed` | 5 已审核、9 已锁定 | 后续发布文本构建 |

0 未翻译、2 有疑问、-1 已隐藏、空译文均不进入游戏词典，游戏保留英文。没有任何合格译文时构建失败，不生成空词典。标准文件若没有 stage，只能用 pilot；正式候选/审核构建应使用保留状态的“词条数据”或 API 快照。

首次样板均为 stage=1，所以 `03-Build-Reviewed.cmd` 现在会拒绝生成发布文本，这是预期行为。

## 接入 ParaTranz 与 GitHub

1. 把本工程内容放在独立的 SotM2 仓库根目录，勿混入 DW2 仓库。建议仓库名 `SotM2-Chinese-Localization`；公开性待维护者确定。
2. ParaTranz 导入上述两个文件后，在 `paratranz-project.json` 填入 projectId 与两个 fileId。本仓库已填入项目 21585 与已核对的两个文件编号。
3. 在 GitHub 仓库 Secrets 设置 `PARATRANZ_TOKEN`。令牌仅在平台设置中保存，不写入 JSON、不提交仓库、不通过聊天粘贴。
4. 仓库需允许 GitHub Actions 创建 PR，并允许工作流写入分支。
5. 在 Actions 手动运行 **Import ParaTranz snapshot**。
6. 工作流逐文件读取完整词条数据，前后核对文件修订标记，验证 44 个稳定键与原文；生成测试 artifact，提交译文快照分支并创建 PR。
7. 检查译文差异与跳过清单后合并；变更文本实机验收后再进入 Release 发布。当前工作流不自动发布 Release。仓库的默认 GITHUB_TOKEN 权限可以保持只读，导入工作流已显式声明所需的写权限。

此路径只读 ParaTranz，不自动回写原文或覆盖翻译。新增/变更原文先更新 registry，通过 `diff` 审查后再导出上传。

GitHub 当前对 GITHUB_TOKEN 创建的 PR 可能要求在 PR 中点击 **Approve workflows to run** 后启动其校验工作流。导入工作流在创建 PR 前已经执行本轮工具测试与候选构建。不要为了消除提示而把令牌写进代码。

本地拉取方式（先通过本机环境设置 PARATRANZ_TOKEN）：

```sh
python3 tools/pull_paratranz.py --out incoming
python3 tools/catalog.py build --input incoming --out build --mode translated
```

`incoming` 必须是新目录，防止旧文件混入。对已有译文目录的替换放在完成校验之后，由 Git 提交记录。

## 游戏回填与首轮线上验收

1. 在 ParaTranz 只修改一项易辨认的菜单译文，例如把一个指定 Options 词条暂改为“设置”；标为已翻译。
2. 拉取并构建。核对只有这个稳定键对应的游戏目标变化，同原文但不同控件的条目不应被连带更改。
3. 关闭游戏，备份游戏目录 `BepInEx/plugins/SotM2ChinesePrototype/sample-texts.json`，再用新构建的同名文件替换；只替换这一份文件。
4. 重启游戏，查看指定菜单，再检查卡文、生命值刷新、读档与回菜单。不要在运行中覆盖词典。
5. 验证后把试验措辞恢复或正式审定，保留这次 PR 作为首次线上往返证据。

本工程从初始快照构建的输出与当前样板完全相同，当前直接替换它不会增加新中文。构建失败时请勿把输出目录中的旧文件误当作新成果；以本次命令成功与 build-report 的指纹为准。

## 已验证与未验证

- 29 项 Python 测试通过：精确往返、单条上下文隔离、键缺失/重复/未知、旧原文、指纹、占位符、标签嵌套、数字符号、换行、状态筛选、失败不覆盖、完整远程快照夹具等。
- 输出通过现有运行词典的 31 项 .NET 检查，字节指纹与 0.1.4 原词典一致。
- API 行为依据当日官方 OpenAPI 0.7.1；证据摘录在 docs 中。
- ParaTranz 实际导入已确认：2 个文件、44 条已翻译词条。带账号的下载、GitHub 同步工作流与 PR：尚待验证。快照下载测试使用模拟 API 响应，不冒充线上测试。
- Windows CMD、替换后新译文的实机版面：待线上首轮验证。

详细键名规则与覆盖边界见 `docs/KEYS-AND-COVERAGE.md`。
