# 显示覆盖与防漏维护流程

2026-11-27起作为正式维护要求，适用于动态文本、提示、按钮、固定文本及后续卡组审核。首次接入的自动对账范围为当前106个动态模板、219条动态候选和已归档显示证据；后续类别沿用相同要求扩展登记，不能据此宣称全游戏覆盖。

## 每轮固定步骤

1. **登记原文与语义。** 核对既有词条、冻结术语与完整句式；区分源文缺失、未审译稿、显示位置未接入、保护条件、主动延期。不得用片段全局替换来消除英文。
2. **登记功能和位置。** 每个运行模板归入一个功能族。核对角色牌／场上牌／附属区、小图／大图、当前／非当前英雄、3／4／5人布局；未观察的明确记为`unobserved`，不虚构路径。按组件用途和所属对象识别优先于硬编码单一路径，身份和数值保护保留。
3. **准备正反例。** 同功能跨位置的正例、同文本不同用途的反例、数量／限定条件／富文本／幂等检查。首次基线为51个上下文契约及9个审计分类契约，后续数量以当前契约及审计输出为准，直接执行运行代码中的转换类，避免再实现一套近似正则。
4. **导入实机证据。** 核对版本和DLL／配置哈希；追加原文、完整路径、卡牌ID、输入来源、日志行号及证据文件SHA256。同一原文在不同位置是不同观察项。重复导入幂等，保留旧证据。
5. **审计并分派。** 自动检查基础词典、显示规则与动态规则。未匹配项必须逐项登记处理阶段、原因、责任人和下一步；已有译文但位置未覆盖优先修复。缺少分类证据时记录`pending-diagnosis`，不能擅自归因为保护拦截。
6. **构建与交付。** CI通过说明当前已知范围没有未登记项和回归失败。交付同时说明新增覆盖、仍未观察的位置及待实机验收内容。
7. **人工验收与收尾。** 用户确认的画面、数字变化、点击、生命值与读档结果按实际范围记录；日志命中不自动代替人工验收。阶段收尾使用更严格的完成检查。

## 数据与状态

| 文件 | 用途 |
| --- | --- |
| `coverage/contracts.json` | 每个动态模板的功能归属、显示位置矩阵、可执行正反例、运行文件审核指纹 |
| `coverage/evidence/latest.json` | 累积观察记录；名称保留latest，但导入会追加历史会话，不能覆盖丢失旧项 |
| `coverage/regression-baseline.json` | 已复核观察的离线输出；丢失或退回未匹配会阻止CI，即使有历史待办也不能掩盖回归 |
| `coverage/triage.json` | 未匹配观察项的明确处理方式；已有规则修复后保留历史决定 |
| `coverage/inventory-status.json` | 219条候选的源文指纹、译文审核、显示接入和实机验收状态 |
| `probe/dynamic-text-review.json` | 译稿与拼接分析；不直接输出游戏 |
| `build/coverage/report.json`、`report.md` | 当前规则重放结果、待办、库存状态及阶段完成检查结果 |

库存的`draft`与`reviewed`分开；`covered`／`composed`必须引用实际模板及证据；`accepted`还必须附人工验收来源。片段须指向组成的完整句，才能记为不单独显示。延期必须写明重新开放条件。

显示分类包括：`current-rule-match`、`known-source-route-gap`、`unresolved-full-string`、`protected-number`、`protected-number-markup`、`translated-output-observed`、`protected-input-buffer`、`localized-text-observed`、`truncated-evidence`。

`known-source-route-gap`表示相同完整原文可在已知其他位置转换，但本位置无法匹配，是定位线索，不是允许在本位置强制替换。`unresolved-full-string`仅表示当前完整文本未匹配，不能断言原文不存在或译文不存在。

## 命令

从仓库根目录执行。导入不会提取ZIP中的任意路径，只读取日志与版本清单；原始日志不入库，归档必要的显示证据。

```sh
python3 tools/coverage_audit.py intake /path/to/SotM2-Dynamic-Evidence.zip --out coverage/evidence/latest.json
python3 tools/inventory.py --input inventory-translations --out build --mode translated
python3 tools/coverage_audit.py check
```

新增未知项会令最后一步失败，同时输出报告。维护者审核后编辑`coverage/triage.json`；工具不会自动生成“全部忽略”的决定。若分析另一份快照，可用`--snapshot`、`--triage`、`--catalog`及`--out`指定文件；`--dotnet`可指定.NET 8执行文件。

常规修改前后运行：

```sh
python3 -m unittest discover -s tests -v
dotnet run --project tests/runtime/RuntimeChecks.csproj -- build/sample-texts.json
python3 tools/coverage_audit.py check
```

第一阶段收尾另运行：

```sh
python3 tools/coverage_audit.py check --require-stage-1-complete
```

只要第一阶段仍有待办、未验收位置或未关闭库存项，收尾检查就失败。日常CI允许已明确登记的待办存在，以便持续开发；不得把日常检查通过表述为阶段完成。

## 证据边界与发布门槛

- `markerCandidate=false`只表示动态层未命中；基础词典或独立显示规则可能已处理。必须合并检查，避免把已汉化菜单列为漏译。
- 当前候选词典与实机词典哈希不同，会在报告中明确标出。不能将候选词典缺项误报为实机漏译；已有应用日志可作为线索，待精确词典复核。
- 现有应用日志按对象名／原文去重，不带完整路径，因此不能据此为每个位置标记验收。
- 现有采集最多1200项、每类对象200项，且跳过部分控件。达到上限或采集异常会阻止检查；未达到上限也不代表覆盖完整。
- 本轮未改运行插件，也未改变采集钩子。运行时保护条件的逐项原因尚不能从旧日志完整还原，保持待诊断；后续增加诊断需独立回归，不能调用TMP.text getter。
- 修改运行文件会因指纹变化阻止检查，维护者须审查矩阵和用例后更新指纹。所有新模板都须登记功能归属；已有待办不得通过删除证据消失。
- 首次引入时，219条候选按已有证据保守初始化为译稿与待核验；不倒推全量实机验收。既有通过记录继续保留在版本文档中，后续按词条与显示位置回填。

推进顺序保持：动态／提示／按钮 → 非卡组固定文本 → 卡组逐个审核。已确认的原生抽牌计数问题不再作为汉化缺陷；EA未开放内容按既定条件延期。

### 0.1.14审计补充

带样式数字只按已观察的完整数字格式归类，不能通过剥离任意标签忽略未知文本。英文专名与中文结果混合时，只有同一控件的已捕获原文经当前转换得到完全相同输出，才标记为`translated-output-observed`；仍不代表实机交互验收。修改审计分类也必须具备正反例，避免把未知混合文本当作已完成。
