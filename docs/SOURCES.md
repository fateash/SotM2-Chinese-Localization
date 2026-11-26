# 实现依据（2026-11-26 核对）

- ParaTranz 官方 OpenAPI：https://paratranz.cn/api-docs?format=json 。本次读取 version=0.7.1；选用 GET /projects/{projectId}/files 与 GET /projects/{projectId}/files/{fileId}/translation，不使用可能丢失审核状态的纯游戏格式导出。
- GitHub Actions artifact：https://docs.github.com/en/actions/tutorials/store-and-share-data 。候选文本使用 artifact 留供测试，PR 只提交译文快照。
- GitHub 工作流触发规则：https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/trigger-a-workflow 。GITHUB_TOKEN 创建/更新 PR 的运行可能处于待批准状态。
- GitHub CLI PR：https://cli.github.com/manual/gh_pr_create 。PR 描述通过 --body-file 保留换行。

实际项目授权与工作流配置尚未执行；上述为接口及实现依据。
