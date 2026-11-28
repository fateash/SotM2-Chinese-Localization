# 翻译维护流程

当前按卡组分文件，活动文件由 `baseline/registry.json` 的 `file` 字段与 `paratranz-project.json` 共同约束。具体迁移、术语和验收见 [Batch01](BATCH01.md)。

## 显示覆盖检查（必做）

每轮修改、导入实机证据和交付前，执行 [显示覆盖与防漏流程](COVERAGE-WORKFLOW.md)。`Validate localization` 已加入覆盖审计：新模板未登记、运行规则未经覆盖复核、未知显示项未分派，均阻止检查通过。日常检查通过不等于全量覆盖或实机验收。

## 编辑与同步

译者在 ParaTranz 选择 `Legacy.json`、`BaronBlade.json` 或 `ui.json`，编辑 translation 与审核状态。稳定键、原文与文件归属由维护者管理；词条上下文注明卡牌及实际资源字段。

手动运行 **Import ParaTranz snapshot** 后，工作流只读下载活动文件，校验每个文件完整键集、原文和格式，构建候选词典并开启 PR。快照无变化时不会开 PR。新文件以 null 配置时按唯一完整文件名发现，指定数字 ID 时固定文件身份；下载前后核对文件 ID 与修订标记。

工作流不回写 ParaTranz，不自动合并，也不发布正式补丁。合并前要求 Validate localization 中的 validate 检查通过。

## 本地构建

```sh
python3 -m unittest discover -s tests -v
python3 tools/catalog.py build --input translations --out build --mode translated
python3 tools/catalog.py export --input translations --out export
```

`export` 输出协作 JSON，`build` 输出游戏使用的 `sample-texts.json` 与报告，两者不可互换。翻译状态策略：translated 接受 1、3、5、9；reviewed 只接受 5、9；pilot 仅供离线往返，可接受未带状态的导入文件。空译文、未翻译、疑问与隐藏词条不进入候选包。审核模式无合格词条时拒绝产生空词典。

关闭游戏，备份插件目录中的 `sample-texts.json` 后再替换，随后重启游戏。新增文本必须实机检查排版和动态规则，不能用静态检查替代。

## 增量维护

新增卡组时在注册表指定新的文件归属，并同步更新配置、翻译快照与平台文件。旧键不得重排；原文有变化应更新指纹并重新审核。新种类文本必须先确认运行支持，未知字段会拒绝构建。

牌组迁移期间先暂停手动拉取，准备平台文件后再合并相应基线。导出备份、保留历史文件并核对最新人工校订，避免旧初稿覆盖新译文。

## 已有证据

2026-11-26，原 44 条样板完成首次授权拉取与 PR #1；PR #2 的单条“卡牌图鉴（测试）”译文经用户截图确认在游戏中显示，随后测试 PR 关闭且不合并后缀。该记录证明单条同步链路；不代表本批 45 条新增正文或所有游戏场景已验收。

`docs/validation.json` 是工具链 0.1.0 的历史离线报告，`baseline/accepted-0.1.4.json` 是已接受的旧词典，均保留作回归基线。当前候选构建状态以本批构建报告与 CI 为准。
