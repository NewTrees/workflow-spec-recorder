# Browser-First MVP Design

## Product Goal

Build a Windows desktop recorder that helps business users capture browser workflows and export **APA-ready requirement documents**. The product is not a screen recorder first; it is a workflow specification tool that turns real browser actions into structured automation intent.

## MVP Scope

The first usable release focuses on browser workflows and supports six action families:

1. Open page / navigate
2. Click element
3. Fill text input
4. Select dropdown option
5. Upload file
6. Wait for or verify page state

Out of scope for the first release:

- Native desktop application capture
- Branches, loops, and complex decision modeling
- Deep iframe support
- CAPTCHA handling
- Bulk table editing intelligence
- Multi-user collaboration

## Primary User Journey

1. The user opens the desktop recorder.
2. The user starts a recording session.
3. The user performs a browser workflow in Chrome.
4. The Chrome extension sends browser events, target element metadata, page metadata, and screenshots to the desktop app.
5. The desktop app converts raw events into business-readable steps.
6. The user reviews and refines step titles, variables, and success criteria.
7. The user exports:
   - `workflow.json`
   - `APA需求文档.md`
   - step screenshots

## Architecture

### Components

| Component | Responsibility |
|---|---|
| WPF desktop app | Session lifecycle, review UI, local persistence, export |
| Local capture server | Receives extension events over localhost HTTP |
| Chrome extension | Captures DOM events, navigation events, element metadata, screenshots |
| Core domain library | Models, event-to-step mapping, Markdown export |

### Why This Split

Browser automation quality depends on DOM-level information, not just coordinates or screenshots. The extension owns browser truth; the desktop app owns product workflow and export quality.

## Internal Data Model

### Capture Event

Represents one raw browser observation:

- event type
- timestamp
- page URL and title
- target element metadata
- captured value where safe
- screenshot payload where available

### Recorded Step

Represents one APA-facing action:

- business title
- action kind
- target element
- literal value or variable
- success criteria
- screenshot path
- notes

### Workflow Session

Owns:

- project metadata
- ordered steps
- discovered variables
- discovered elements
- export timestamps

## APA Export Format

The Markdown document must include:

1. 项目目标
2. 适用范围与前置条件
3. 流程概览
4. 详细步骤
5. 变量定义
6. 元素清单
7. 等待与异常策略
8. 特殊说明

The JSON export remains the canonical machine-readable representation. Markdown is rendered from JSON so both outputs remain consistent.

## UX Design

The desktop app is a compact recording workbench:

- top command bar: new, start, pause, stop, export
- left timeline: ordered steps
- center preview: selected step summary and screenshot
- right editor: business title, variable name, sensitive flag, success criteria, notes
- bottom status: recorder state, extension/server connection, export location

## Reliability Rules

- Password values are never stored in clear text.
- File upload captures a variable placeholder, not a real local file path.
- If a click is followed by navigation, the click step should inherit a stronger success criterion.
- If no recording session is active, inbound extension events are ignored safely.
- Export should still succeed when screenshots are missing.

## Definition of Done for MVP

A user can record a real browser workflow such as:

`登录 -> 导航 -> 新建 -> 填写 -> 上传 -> 提交`

and export a Markdown + JSON package where most steps already have:

- readable business wording
- target element metadata
- value / variable information
- basic success criteria
- attached screenshot if available

