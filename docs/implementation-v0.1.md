# 0.1 Preview 实现说明

## 本版目标

这一版验证教室电脑上的最短点名闭环：应用启动后同时提供桌面占位栏、贴边启动器和主控面板；教师维护一份固定名单后，可以从桌面两次点击内得到点名结果。

按当前范围，课表和教学日历不进入第一版。核心项目保留日期与课程数据契约，方便后续接入时不重写窗口生命周期，但界面没有课表编辑入口。

## 已实现

- `.NET 10 + WPF` 解决方案，核心逻辑不引用 WPF；
- 主控面板：总览、随机点名、固定名单、显示与启动、关于；
- 固定名单：姓名必填，学号/座号可选，可暂停参与点名；
- 文本名单导入预览：支持每行姓名，以及“姓名 + 逗号/制表符 + 编号”；
- 独立随机：使用 `RandomNumberGenerator.GetInt32` 从完整候选集抽取；
- 均衡轮选：一轮内不重复，耗尽后自动开始下一轮；
- 点名会话可切换模式和重置本轮；
- 快捷启动器可拖动，并吸附主屏幕工作区最近边缘；
- 顶部占位栏使用公开 Win32 z-order API 尝试保持桌面底层；
- 每用户开机自启使用 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`；
- JSON 数据以临时文件写入后原子替换；
- `--smoke-test` 检查 WPF XAML 能否加载；
- 点名策略和吸边算法共 8 个自动测试。

## 当前限制

- 顶部栏只显示日期、时间和课表未配置提示；
- 吸边和顶部栏只按主屏工作区定位，尚未处理多屏与混合 DPI；
- 预览数据存于当前用户目录，尚未迁移到整机共享 SQLite；
- 没有 XLSX/CSV 文件选择器、法定节假日更新、软件更新和 MSI；
- 底层 z-order 仍需在 Windows 10/11 与 Explorer 重启场景进行人工验证；
- 开发构建开启自启时会记录当前开发输出路径，安装版需改为安装目录路径。

## 工程结构

```text
src/ClassHelper.Core
  Display/       吸边纯算法
  RollCall/      两种点名策略与随机源
  Scheduling/    后续课表接入的数据契约

src/ClassHelper.App
  MainWindow     主控面板
  BannerWindow   桌面顶部占位栏
  LauncherWindow 贴边启动器
  RollCallWindow 点名结果窗口
  Services/      生命周期、本地保存、自启
  ViewModels/    UI 状态和数据适配

tests/ClassHelper.Core.Tests
  RollCall/      抽取行为测试
  Display/       吸边行为测试
```

## 下一版建议

先对三个窗口做 Windows 10/11、多 DPI 和 Explorer 重启人工验收，再决定是否进入 SQLite 共享数据、完整课表或安装器。这样能尽早确认“顶部底层 + 启动器置顶”这一最高风险体验。
