# APA 流程录制器

一个面向 APA / AI-RPA 的浏览器流程采集 MVP。它会把用户在 Chrome 中的真实操作沉淀为：

- `workflow.json`：机器可读的结构化流程
- `APA需求文档.md`：人可读、可继续修订的需求文档
- `assets/`：步骤截图

## 当前能力

- 记录页面打开 / 跳转
- 记录点击
- 记录文本输入
- 记录下拉选择
- 记录文件上传
- 生成元素清单、变量定义、等待与异常策略
- 在桌面端编辑步骤标题、变量名、成功判定和备注

## 目录

```text
src/ApaFlowRecorder.Desktop/   WPF 桌面端
src/ApaFlowRecorder.Core/      领域模型与导出逻辑
extension/                     Chrome 扩展
samples/demo-web-app/          本地演示页面
```

## 运行桌面端

开发运行：

```powershell
$env:DOTNET_ROOT='C:\Program Files\dotnet'
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\src\ApaFlowRecorder.Desktop\ApaFlowRecorder.Desktop.csproj
```

> 这台机器上用户级 `DOTNET_ROOT` 指向旧版 .NET，所以这里显式使用系统已安装的 .NET 9。

## 安装 Chrome 扩展

1. 打开 `chrome://extensions/`
2. 开启右上角“开发者模式”
3. 点击“加载已解压的扩展程序”
4. 选择本仓库下的 `extension/` 文件夹
5. 打开扩展弹窗，看到“桌面端已连接”即可

## 录制流程

1. 先启动桌面端
2. 在桌面端填写流程名称、项目目标、前置条件
3. 点击“开始录制”
4. 到 Chrome 中执行业务操作
5. 回到桌面端检查时间线和步骤明细
6. 必要时补充变量名、成功判定、备注
7. 点击“导出”

导出文件默认位于：

```text
文档\ApaFlowRecorder\Exports\
```

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

- 当前优先支持浏览器流程，不支持原生桌面应用
- 当前不自动推断分支、循环、验证码和复杂表格逻辑
- Chrome 扩展目前按事件采集，不做高级语义合并
- 文件上传只记录“文件变量”，不会读取真实本地路径

