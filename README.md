# ClassHelper 课堂助手

面向中小学教室固定电脑的 Windows 课堂辅助软件。仓库包含可运行的 `0.1 Preview` 初版实现和产品设计文档。

## 0.1 Preview

当前可用：

- WPF 主控面板；
- 固定名单的新增、粘贴预览、启用/停用与本地保存；
- 独立随机和均衡轮选两种点名方式；
- 始终置顶、可拖动并自动吸边的快捷启动器；
- 当前 Windows 用户可选开机自启；
- 本地 JSON 数据文件和原子写入。

课程表、节假日、教学日历和桌面课程栏由 ClassIsland 提供，本项目不再实现。多屏增强、SQLite 和安装器暂未接入。

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

### 自动构建与发布

GitHub Actions 会在分支推送和 Pull Request 时自动执行格式检查、Release 构建、测试及 WPF 冒烟验证。

发布版本使用严格的 `v主版本.次版本.修订版本` 标签，例如：

```powershell
git tag v0.1.0
git push origin v0.1.0
```

推送标签后会自动创建 GitHub Release，并附带可直接运行的 Windows x64 自包含 ZIP 和 SHA-256 校验文件。也可以先在 GitHub 中发布同格式标签的 Release，工作流会自动构建并补充产物；失败时可从 Actions 页面输入已有标签手动重跑。

## 设计文档

- [领域词汇表](./CONTEXT.md)
- [产品范围](./docs/product-scope.md)
- [技术设计](./docs/technical-design.md)
- [UI/UX 设计规范](./docs/uiux/ui-ux-spec.md)
- [UI/UX 审查清单](./docs/uiux/review-checklist.md)
- [历史 UI/UX 高保真预览](./docs/uiux/previews/README.md)
- [历史 Imagine 提示记录](./docs/uiux/preview-prompts.md)
- [0.1 Preview 实现说明](./docs/implementation-v0.1.md)
- [架构决策记录](./docs/adr/)
