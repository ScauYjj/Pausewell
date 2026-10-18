# v0.3.1 控件设计补全包

日期：2026-10-16。状态：设计草案和静态样张，尚未接入运行程序。v0.3 基础样张作为历史保留。

- 总规范入口：`docs/design/2026-10-16-设计系统规范-v0.3.md`，标题及最新规则已更新为 v0.3.1。
- 详细组件契约：`docs/design/2026-10-16-控件配套设计与验收-v0.3.1.md`。
- `tokens.json`：基准尺寸、状态、颜色和 14 类详细契约，包含待验证范围。
- `选择与日期控件样张.png`：下拉选项、禁用/焦点、日期日历和范围错误的部分静态状态。
- `输入与焦点及滚动样张.png`：导航焦点、输入、备注、复选、展开、细滚动及记录分组示意。

样张可通过根目录 `scripts/render-component-contracts.ps1` 重建。它们用于配套设计评审，不能替代原生交互、完整状态覆盖或玻璃材质验收。

用户此次四张截图原样保存在 `artifacts/design-v031/user-review`，校验清单为该目录 `manifest.json`。实色对比预检为 `artifacts/design-v031/token-contrast-results.json`；26 组通过，最终材质合成面与可访问性交互仍待检查。

图标使用 [Microsoft Fluent System Icons](https://github.com/microsoft/fluentui-system-icons) 的标准 SVG，许可证随 `assets/LICENSE.txt` 提供。本轮不生成替代图标，不改动运行中的应用或提醒数据。
