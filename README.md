# Workflow Spec Recorder（工作流规格录制器）

一个面向自动化工作流生成工具的流程采集与需求文档生成工具。它可以把浏览器和部分 Windows 桌面操作示例、业务资料、输入输出样例整理成结构化的自动化需求规格，当前内置了面向 APA / AI-RPA 的交付模板。

当前版本支持两种输出：

- 原始录制包：把 Chrome 中的实际操作导出为 `workflow.json`、需求文档草稿和截图资源
- 自动化需求文档：把录制步骤和截图作为代表性示例，再结合粗略需求、输入样例、输出样例、任意参考资料和可选 LLM，生成给自动化工作流生成工具使用的准确、详细需求文档

## 当前能力

- 记录页面打开 / 跳转
- 记录点击、文本输入、下拉选择、文件上传
- 记录 Chrome 下载完成/失败事件，补充下载文件路径或来源 URL
- 录制期间可捕获非浏览器页面的 Windows 桌面点击；普通浏览器页面点击会由 DOM 采集负责，Windows 保存/打开对话框和客户端窗口会作为桌面步骤记录
- 在桌面端编辑步骤标题、变量名、成功判定和备注
- 读取多份资料文件；`.xlsx`、`.docx`、`.pptx`、`.txt`、`.md`、`.csv`、`.json`、`.xml`、`.html`、`.log` 会提取可读内容，PDF 和未知类型会作为参考文件加入并记录文件信息
- Word/PPT 资料中的内嵌截图、流程图和示意图会被提取为视觉证据；使用支持图像输入的模型时，会和文字说明一起发送给大模型分析
- 支持 OpenAI-compatible LLM 配置，内置 MiniMax / DeepSeek 预设
- API Key 明文保存到当前 Windows 用户配置文件，便于下次启动继续使用
- API Key 为空时使用规则兜底生成动态循环版需求文档
- 导出原始录制包后自动加入资料集，生成最终需求时无需再手工选择录制包
- 配置支持视觉输入的 OpenAI-compatible 模型时，会把录制截图以 `image_url` 形式随步骤文字一起发送给大模型
- 最终需求文档按 `项目需求描述` 模板收敛输出，避免把录屏分析过程、泛化推断表和流程图混进交付文档
- 需求生成提示词模板可在桌面端编辑、保存、打开配置文件或恢复默认；模板保存到当前 Windows 用户配置目录
- LLM 输出会做基础结构校验，缺少核心章节或输出分析过程章节时会自动请求模型按模板修正
- 支持无界面操作的资料处理、文档生成、数据清洗和文件转换类需求生成
- 支持检测 Chrome 扩展连接，并在录制中显示醒目的录制状态

## 目录

```text
src/ApaFlowRecorder.Desktop/   WPF 桌面端
src/ApaFlowRecorder.Core/      领域模型、Office 读取、需求生成、导出逻辑
extension/                     Chrome 扩展
samples/demo-web-app/          本地演示页面
```

## 运行桌面端

开发运行：

```powershell
$env:DOTNET_ROOT='C:\Program Files\dotnet'
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\src\ApaFlowRecorder.Desktop\ApaFlowRecorder.Desktop.csproj
```

发布包入口：

```text
dist\ApaFlowRecorderSelfContained\start-recorder.bat
```

安装包入口：

```text
dist\installer\WorkflowSpecRecorder-Setup-0.2.6.exe
```

安装向导会显示安装目录选择页；默认安装到当前用户目录下的 `Programs\WorkflowSpecRecorder`，测试用户可以在安装时改成任意有写入权限的路径。

重新构建安装包：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
```

## 安装 Chrome 扩展

Chrome 不允许普通安装包静默安装本地未上架扩展，所以安装桌面端后仍需要手动加载一次扩展。安装后的桌面端顶部有 `打开 Chrome 扩展目录` 按钮，开始菜单也有 `打开 Chrome 扩展目录` 快捷方式。

1. 打开 `chrome://extensions/`
2. 开启右上角“开发者模式”
3. 点击“加载已解压的扩展程序”
4. 选择安装目录下的 `extension/` 文件夹
5. 打开扩展弹窗，看到桌面端已连接即可

## 录制并导出原始录制包

1. 启动桌面端
2. 在 `录制 / 编辑` 页填写流程名称、项目目标、前置条件
3. 点击 `开始/继续录制`
4. 到 Chrome 中执行业务操作
5. 桌面端会实时显示已记录步骤数、收到事件数、最近一次事件
6. 回到桌面端检查时间线和步骤明细
7. 必要时补充变量名、成功判定、备注
8. 点击 `导出原始录制包`

导出后，`workflow.json`、`APA需求文档.md` 和截图资源会自动加入 `泛化需求` 页的资料集。后续生成 APA 需求文档时，不需要再手工选择这份录制包。

默认导出位置：

```text
文档\ApaFlowRecorder\Exports\
```

## 录制和泛化如何配合

`录制 / 编辑` 页是代表性示例轨迹，用来告诉 APA “人工大概怎么操作界面、什么算成功”。`泛化需求` 页是主入口，用来综合粗略需求、输入样例、输出样例、参考附件和可选录制步骤生成最终 APA 需求。

录制步骤不是固定点击脚本。只要示例表达了人的业务意图，大模型会结合资料和截图推断完整流程，例如把只演示一次的分类、标签、表格行、分页或文件处理抽象成动态集合遍历。如果流程没有界面操作，例如资料汇总、文档生成、数据清洗或文件处理，可以不录制，直接添加资料生成。

如果模型支持视觉输入，生成时会同时发送步骤文字和截图，让模型能识别页面布局、按钮语义、可见列表、Tab、表格和状态变化。若使用纯文本模型，仍可生成需求文档，但复杂浏览器场景的推断质量会下降。

最终文档会收敛为清晰的需求模板：`项目目标`、`流程步骤`、`流程输入`、`流程输出`、`约束与异常处理`。中间推理内容只用于帮助模型理解，不应作为最终交付文档的大段分析章节。

## 生成 APA 需求文档

1. 切到 `泛化需求` 页
2. 点击 `添加资料`，可一次选择多个需求文档、输入样例、输出样例、截图说明或参考附件
3. 可选：先在 `录制 / 编辑` 页录制几个浏览器示例步骤
4. 可选：选择 `MiniMax 国内预设` 或 `DeepSeek 预设`，填写 API Key；API Key 会明文保存到当前 Windows 用户配置文件
5. 可选：在 `提示词模板` 区域调整角色、章节模板、禁止事项和行业偏好；保留 `{{recorded_example}}`、`{{source_materials}}`、`{{extra_instruction}}` 占位符可以让程序把录制步骤、资料内容和补充要求注入到模板中
6. 点击 `生成 APA 需求文档`

MiniMax 国内 OpenAI-compatible 预设：

```text
Base URL: https://api.minimaxi.com/v1
Model: MiniMax-M2.7
```

默认导出位置：

```text
文档\ApaFlowRecorder\GeneralizedExports\
```

导出文件：

- `APA-generalized-requirements.md`
- `workflow-spec.json`
- `llm-prompt.txt`
- `generation-mode.txt`

## Chrome 扩展状态

- `REC`：桌面端在线并正在录制
- `等`：桌面端在线，但未开始录制
- `停`：桌面端在线，录制已暂停
- `OFF`：扩展没有连上桌面端

## 桌面与下载采集说明

- 网页内点击仍由 Chrome 扩展采集，带 URL、DOM 元素、选择器和截图。
- 浏览器下载由 Chrome `downloads` 权限采集，当前记录下载完成或失败时的文件名、保存路径和来源 URL。
- Windows 文件保存/打开对话框、客户端软件、Excel/ERP 等浏览器外窗口由桌面采集层记录前台窗口、进程名、窗口标题、控件名称、控件类型、点击动作、可打印输入和 Enter/Tab/Escape 等确认键。
- 桌面采集使用 Windows UI Automation 读取控件信息，读取不到时降级为 Win32 窗口句柄信息。少数自绘客户端可能只能记录窗口级信息，后续可继续增强 OCR/截图识别。

## 本地演示页面

```powershell
cd .\samples\demo-web-app
python -m http.server 8080
```

然后访问：

```text
http://127.0.0.1:8080/
```

## 已知限制

- 已支持基础 Windows 桌面点击、输入、确认键和浏览器下载事件采集；复杂自绘客户端可能仍需要后续增强 OCR/截图理解
- LLM 主要用于需求归纳和文档生成，不参与运行时抓取
- 当前不会自动执行 RPA，只生成给自动化工作流生成工具使用的需求文档
- 复杂站点的反爬、验证码、登录态和限流仍需要执行侧处理

## License

MIT
