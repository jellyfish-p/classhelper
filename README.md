# ClassHelper 课堂助手

面向中小学教室固定电脑的 Windows 课堂辅助软件。仓库包含可运行的 `0.1 Preview` 初版实现和产品设计文档。

## 0.1 Preview

当前可用：

- WPF 主控面板；
- 固定名单的新增、粘贴预览、启用/停用与本地保存；
- 独立随机和均衡轮选两种点名方式；
- 始终置顶、可拖动并自动吸边的快捷启动器；
- 桌面底层的顶部占位栏；
- 当前 Windows 用户可选开机自启；
- 本地 JSON 数据文件和原子写入。

按当前版本范围，课表编辑、教学日历、多屏模式、SQLite 和安装器暂未接入。顶部栏会明确显示“课表尚未配置”，不会提供不可用入口。

### 运行

需要 Windows 10/11 x64 和 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)：

```powershell
dotnet restore ClassHelper.sln
dotnet build ClassHelper.sln
dotnet test ClassHelper.sln
dotnet run --project src/ClassHelper.App/ClassHelper.App.csproj
```

首次运行会生成一份可编辑的示例名单，数据保存在：

```text
%LocalAppData%\ClassHelper\classroom.preview.json
```

自动化环境可使用 `--smoke-test` 加载所有 XAML 资源并立即退出：

```powershell
dotnet run --project src/ClassHelper.App/ClassHelper.App.csproj -- --smoke-test
```

## 设计文档

- [领域词汇表](./CONTEXT.md)
- [产品范围](./docs/product-scope.md)
- [技术设计](./docs/technical-design.md)
- [UI/UX 设计规范](./docs/uiux/ui-ux-spec.md)
- [UI/UX 审查清单](./docs/uiux/review-checklist.md)
- [UI/UX 高保真预览](./docs/uiux/previews/README.md)
- [Imagine 提示记录](./docs/uiux/preview-prompts.md)
- [0.1 Preview 实现说明](./docs/implementation-v0.1.md)
- [架构决策记录](./docs/adr/)
