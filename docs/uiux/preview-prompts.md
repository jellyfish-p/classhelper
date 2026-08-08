# Imagine 预览提示记录

> 生成方式：Codex 内置 `image_gen`
> 用例分类：`ui-mockup`；日历纠错使用 `precise-object-edit`
> 日期：2026-08-08

所有提示都要求：高保真可实现的 Windows WPF 产品界面、清晰中文、无浏览器框架、无 macOS 控件、无水印、无公司商标、无霓虹渐变、无概念艺术效果。后续页面使用前一张成品作为视觉参考，以保持暖灰纸张底、靛蓝操作色、青绿当前状态和琥珀下一节状态一致。

## 01-desktop-overview-v2.png

```text
Use case: ui-mockup
Asset type: high-fidelity Windows desktop application preview, 16:9 landscape
Primary request: Show the ambient desktop experience for a Chinese middle-school classroom assistant called “课堂助手”. At the top, place a wide slim translucent timetable banner with date, cycle week and all of today’s class periods. Past classes are muted, current English is calm teal, next PE is amber outline, later classes neutral. Add a narrow floating vertical quick launcher snapped to the right edge with four Fluent-like icons and emphasized indigo roll-call action.
Style: polished production Windows UI, warm paper-like neutrals, restrained translucency, precise spacing and crisp Chinese typography.
Text (verbatim): “8 月 8 日 周六”, “第 2 周”, “语文”, “数学”, “英语”, “体育”, “班会”, “进行中”, “下一节”, “10:18”.
Palette: #F3F1EC, #FBFAF7, #18212B, #66717F, #425FC7, #267F73, #C47A20.
Constraints: practical proportions; banner about 84px high; launcher about 56px wide; no people, photos, browser chrome, watermark or random text.
```

最终图增加一次定向编辑，只修正 Launcher 为四个已确认入口：`课程`、`日历`、`点名`、`主控`；`点名`保持靛蓝主操作。桌面、任务栏和课程 Banner 不变。

## 02-control-panel-overview.png

```text
Use case: ui-mockup
Input: desktop overview as visual style reference only.
Primary request: Design the “课堂助手” main control panel overview as a shippable WPF app. Use a 196px left navigation rail, header “总览”, primary “开始点名”, a strong “今日课程” timeline, a larger teaching-calendar alert region and a compact classroom status region. Avoid a card wall and fake analytics.
Navigation (verbatim): “总览”, “课程表”, “教学日历”, “随机点名”, “固定名单”, “显示与启动”, “更新与关于”.
Content (verbatim): “今日课程”, “开始点名”, “英语”, “进行中”, “体育”, “下一节”, “教学日历”, “8 月 10 日 周一”, “调课待确认”, “请选择当天采用星期几的课表”, “固定名单”, “42 人”, “均衡轮选”, “显示器”, “2 台”, “教学日历已同步”.
```

## 03-timetable-editor.png

```text
Use case: ui-mockup
Input: 02-control-panel-overview.png as shell and style reference only.
Primary request: Design the “课程表” editor for a three-week repeating middle-school timetable. Keep the navigation shell. Add segmented weeks, copy/clear/save actions, a weekday-by-period grid with low-saturation course chips, and an attached 300px course inspector for the selected English cell.
Header (verbatim): “课程表”, “三周循环课表”, “第 1 周”, “第 2 周”, “第 3 周”, “复制上一周”, “清空本周”, “保存更改”.
Grid (verbatim): “节次”, “周一”, “周二”, “周三”, “周四”, “周五”, “语文”, “数学”, “英语”, “体育”, “物理”, “化学”, “班会”.
Inspector (verbatim): “课程安排”, “课程名称”, “英语”, “显示简称”, “颜色”, “任课教师”, “王老师”, “备注”, “删除本次安排”.
```

## 04-teaching-calendar.png

最终图先生成教学日历布局，再执行一次精确纠错：

```text
Use case: precise-object-edit
Input: generated teaching-calendar screen as edit target.
Primary request: Change only calendar content to accurate October 2026 mainland-China example. Header “2026 年 10 月”; align October 1 to Thursday in a Monday-first grid; mark only October 1–7 pale red “法定”; mark October 10 amber “待确认”; October 12 indigo “调课”; October 13 teal “正常”; select October 10. Ordinary weekends must not be labeled statutory.
Inspector (verbatim): “10 月 10 日 周六”, “当天安排”, “停课”, “正常教学”, “调课”, “采用周三课表”, “教师设置优先于法定日历建议”, “保存当天设置”.
Constraints: keep shell, navigation, layout, geometry, palette and unrelated UI unchanged.
```

## 05-roster-import.png

```text
Use case: ui-mockup
Input: 02-control-panel-overview.png as shell and style reference only.
Primary request: Design “固定名单” with a roster table and a 380px XLSX import preview panel. Include column mapping, change counts, five-row sample and explicit import confirmation. No avatars, grades or gender.
Page (verbatim): “固定名单”, “42 名成员”, “粘贴名单”, “导入文件”, “新增成员”, “序号”, “姓名”, “学号 / 座号”, “状态”, “操作”, “正常”.
Preview (verbatim): “导入预览”, “学生名单.xlsx”, “识别到的列”, “姓名列”, “姓名”, “座号列”, “学号 / 座号”, “新增 8”, “更新 2”, “忽略 1”, “重复成员按学号或座号匹配”, “取消”, “导入 10 名成员”.
Example names: “陈思远”, “李若溪”, “王子涵”, “周明宇”, “赵一诺”.
```

## 06-roll-call-result.png

```text
Use case: ui-mockup
Inputs: 01 desktop and 02 control panel as visual references only.
Primary request: Design a centered 960x560 random roll-call result window. It must feel focused, fair and classroom-appropriate, never like a casino. Show a teal balanced-selection mode pill, remaining count, very large name, student number, restrained indigo circular focus motif, progress and three clear actions.
Text (verbatim): “随机点名”, “均衡轮选”, “本轮剩余 31 人”, “陈思远”, “12 号”, “本轮已点到 11 人”, “临时排除”, “结束点名”, “再抽一位”, “减少动态效果”.
Avoid: roulette, slot machine, confetti, trophy, portrait, avatar, neon or dramatic glow.
```

## 07-display-settings.png

```text
Use case: ui-mockup
Input: 02-control-panel-overview.png as shell and style reference only.
Primary request: Design “显示与启动”. Show three equal monitors in one horizontal topology, monitor 2 selected with a miniature top timetable. Present radio cards “指定屏幕”, “智能居中”, “每屏复制”; select smart center. Add launcher docking preview, position actions, enabled startup toggle and an undo toast.
Text (verbatim): “显示与启动”, “顶部课程表与快捷启动器”, “顶部课程表”, “指定屏幕”, “智能居中”, “每屏复制”, “将在中间屏幕顶部显示”, “屏幕变化时自动回退到可用显示器”, “快捷启动器”, “锁定位置”, “恢复默认位置”, “开机自启”, “登录 Windows 后自动显示课程表与启动器”, “显示设置已保存”, “撤销”.
```

## 08-first-run-cycle.png

```text
Use case: ui-mockup
Input: 02-control-panel-overview.png as style reference only.
Primary request: Design step 2 of 4 in the first-run wizard. No navigation rail. Show progress “欢迎 / 课表周期 / 节次时间 / 固定名单”, three cycle cards with “三周” selected, cycle anchor date, a simple three-week loop preview, privacy note and previous/next actions.
Text (verbatim): “课堂助手”, “第 2 步，共 4 步”, “欢迎”, “课表周期”, “节次时间”, “固定名单”, “课表多久循环一次？”, “支持一周、单双周或三周循环”, “每周”, “两周”, “三周”, “周期起始周”, “2026 年 8 月 3 日 周一”, “这一天所在周将作为第 1 周”, “第 1 周”, “第 2 周”, “第 3 周”, “课程和名单只保存在这台电脑”, “上一步”, “下一步”.
```
