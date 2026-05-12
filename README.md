# FAST.FlowChart

A fully featured **Flowchart Diagram Editor** built with **Blazor WASM .NET 9**. Design, annotate, validate and export professional flowcharts directly in the browser — no plugins, no server required.

---

## ✨ Features

### Canvas
- Graph paper grid background with snap-to-grid
- Zoom from 0.25× to 3.0× (mouse wheel or toolbar)
- Drag to move artifacts, rubber-band or Ctrl+Click for multi-select
- Fit-to-page and zoom reset
- Inline diagram title rename (double-click)

### Artifacts
| Tab | Artifacts |
|-----|-----------|
| **Chart** | Start Terminal, End Terminal, On-Page Connector, Note, Preparation |
| **Logic** | Process, Decision, Predefined Process, Decision Table, Switch |
| **Data** | Input/Output, Database, Document |
| **User Defined** | Custom artifacts via YAML definition files |

### Connections
- **Line types:** Straight, Orthogonal (default), Curved, Loop
- **Terminators:** configurable start/end arrow styles
- **Colors:** 10 curated colors
- **Loop lines:** special amber routing with ↺ icon, CanBeLoopBegin/CanBeLoopEnd validation
- Smart routing for Switch artifact case ports (horizontal-first exit)
- Auto-label connections from Switch ports with the case value

### Properties
- Double-click any artifact or connection to open the Properties modal
- Type-specific property fields per artifact (text, numeric, date, dropdown, button)
- **General tab** on every artifact: Notes, Tag/Group
- **Variables & Arguments** tabs on Start Terminal
- **Outputs** tab on End Terminal (maps to Start Terminal variables/arguments)
- **Rules Table** tab on Decision Table (full grid editing)
- **Cases** tab on Switch (add/remove/reorder cases, variable selector)

### Decision Table Artifact
- Define N condition factors + 1 result column
- Full grid manipulation: add/remove/reorder factors and rows
- Cell criteria: exact value, range (`23:45`), comparison (`>=3`), wildcard (`*`)
- Result Variable mapped to a Start Terminal variable
- Default / Not Found Value field
- Canvas shape renders as a live mini-table with color-coded zones

### Switch Artifact
- Tall rectangle with diamond header showing the switch variable
- One output port per case row on the right edge
- Fixed **else** output port at the bottom
- Input port at the top
- Auto-labels connections with case values

### Stencil Panel
- Collapsible groups, data-driven tabs
- User Defined Artifacts (UFAs) appear automatically from YAML definitions

### User Defined Artifacts (UFA)
- Define custom artifacts in YAML files under `wwwroot/stencils/`
- Controlled via `manifest.json` — enable/disable without code changes
- Override shape, color, ports, and properties from any base artifact type
- Zero code changes required to add a new UFA

### Toolbar
- Undo / Redo (full command history)
- Delete, Select All
- Align vertical / horizontal centers
- Validate diagram
- File menu: New, Load, Save, Save As, Export SVG, Export PNG (2×/144dpi)

### Validation
- Must have exactly 1 Start Terminal
- Must have at least 1 End Terminal
- Decision must have exactly 2 outgoing connections
- No dangling connections
- All non-Note artifacts reachable from Start
- Loop line source must support `CanBeLoopBegin`
- Loop line target must support `CanBeLoopEnd`
- Dual-output artifacts (Process, PredefinedProcess, Database, Document, InputOutput, Preparation) may only use one output port at a time

---

## 🏗️ Architecture

### Tech Stack
- **Blazor WASM .NET 9** — single project, no RCL split
- **YamlDotNet 16.1.3** — UFA YAML loading
- **SVG canvas** — all rendering via pure SVG

### Key Design Patterns

**OOP Artifact System** — every artifact extends `FlowArtifact` and implements:
- `RenderSvgShape()` — dynamic SVG rendering on canvas
- `RenderStencilShape()` — stencil panel thumbnail
- `GetPortDescriptors()` — declares input/output ports
- `GetPropertyDefinitions()` — type-specific property fields

**Command Pattern** — all diagram modifications go through `IFlowCommand` → `CommandHistory` for full undo/redo support.

**Data-Driven Stencil** — stencil groups and tabs are discovered dynamically from artifact constructors. Adding a new artifact type requires zero UI changes.

**JSON Polymorphism** — `FlowArtifact` uses `[JsonPolymorphic]` with `[JsonDerivedType]` attributes for clean serialization.

**JS Interop Bridges** — reliable SVG event handling via `wwwroot/js/app.js`:
- Double-click on connections via `data-conn-id`
- Global mouseup for endpoint drag/drop
- File menu outside-click detection

---

## 🚀 Getting Started

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Run locally
```bash
git clone https://github.com/aafent/FAST.FlowChart.git
cd FAST.FlowChart/FlowChartEditor
dotnet run
```

Then open your browser at `https://localhost:5001` (or the URL shown in the terminal).

> **Tip:** After code changes, do a hard refresh (`Ctrl+Shift+R`) to clear the browser cache.

---

## 📁 Project Structure

```
FlowChartEditor/
├── Commands/               — Undo/redo command implementations
├── Components/             — Blazor components (canvas, modal, stencil, validation)
├── Models/
│   ├── Artifacts/          — 13 concrete artifact classes + SvgHelper
│   ├── Ufa/                — User Defined Artifact runtime models
│   └── *.cs                — Core models (FlowChart, FlowArtifact, Geometry, etc.)
├── Pages/                  — Single Index.razor page
├── Services/               — FlowChartService, SyntaxValidator, UfaLoader
└── wwwroot/
    ├── css/                — Editor styles
    ├── js/                 — JS interop (loaded before Blazor)
    └── stencils/           — UFA YAML definitions + manifest.json
```

---

## 🧩 Adding a User Defined Artifact

1. Create a YAML file in `wwwroot/stencils/`:

```yaml
name: MyCustomStep
displayName: My Custom Step
baseType: Process
stencilGroup: MY COMPANY
stencilTab: Custom
color:
  fill: "#e0f2fe"
  stroke: "#0369a1"
  text: "#0c4a6e"
properties:
  hide:
    - duration
    - assignee
  add:
    - key: endpoint
      label: Endpoint URL
      type: Text
      required: true
```

2. Add an entry to `wwwroot/stencils/manifest.json`:

```json
{ "file": "my-custom-step.yaml", "enabled": true }
```

That's it — no code changes needed.

---

## 📄 License

MIT
