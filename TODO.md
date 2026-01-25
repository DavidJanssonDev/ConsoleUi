# ConsoleUi – TODO / Roadmap

This file tracks the planned work for the ConsoleUi project.
The goal is a clean, cross-platform, framebuffer-based Console UI library
with a real-world logger app as the primary demo.

---

## ✅ DONE

- [x] Framebuffer-based rendering model
- [x] Separation of concerns:
  - Core (layout, primitives)
  - Rendering (surface + backends)
  - Logger (app)
- [x] Win32 backend using WriteConsoleOutputW
- [x] ANSI / VT backend for cross-platform rendering
- [x] IConsoleSurface abstraction
- [x] ConsoleSurfaceFactory for backend selection
- [x] Logger decoupled from rendering backend
- [x] Dirty-flag redraw logic in Logger
- [x] Named-pipe based async logging

---

## 🟡 NEXT (HIGH PRIORITY)

### Rendering – Quality & Performance
- [ ] ANSI backend: hide cursor on start, show on exit
- [ ] ANSI backend: handle resize cleanly (full redraw)
- [ ] ANSI backend: avoid flicker on full redraw
- [ ] Implement **dirty-line diff rendering** for ANSI backend
  - [ ] Track previous frame
  - [ ] Compare per-line changes
  - [ ] Redraw only changed lines using cursor positioning

### Rendering – API Polish
- [ ] Review public vs internal APIs in ConsoleUi.Rendering
- [ ] Add XML documentation to public rendering types
- [ ] Ensure consistent namespace casing (ConsoleUi vs ConsoleUI)

---

## 🟠 LOGGER UX IMPROVEMENTS

### Interaction
- [ ] Scroll up/down with arrow keys
- [ ] PageUp / PageDown support
- [ ] Auto-follow (tail) mode toggle
- [ ] Pause/resume live updates

### Filtering & Search
- [ ] Filter by log level
- [ ] Filter by category
- [ ] Text search in log messages
- [ ] Highlight matching rows

### Visual Improvements
- [ ] Status bar (mode, filters, paused state)
- [ ] Highlight WARN / ERROR rows
- [ ] Optional timestamp format toggle

---

## 🔵 CORE UI LIBRARY (MID TERM)

### Input System
- [ ] Cross-platform key input abstraction
- [ ] Non-blocking input polling
- [ ] Key repeat handling

### UI Concepts
- [ ] App loop abstraction (Update / Draw)
- [ ] Viewport (scroll offset, clipping)
- [ ] Basic widgets:
  - [ ] Label
  - [ ] Table
  - [ ] Box / Border
  - [ ] Status bar

### Layout
- [ ] Expand layout engine beyond vertical stacking
- [ ] Horizontal layout
- [ ] Alignment (start / center / end)
- [ ] Percent-based sizing

---

## 🟣 ADVANCED / OPTIONAL

### Rendering
- [ ] Dirty-rectangle rendering (beyond per-line)
- [ ] Off-screen surfaces / compositing
- [ ] Layered rendering (background / UI / overlay)

### Platform
- [ ] Force ANSI backend on Windows (config option)
- [ ] Terminal capability detection
- [ ] Graceful fallback for limited terminals

### Tooling
- [ ] Benchmarks (ANSI vs Win32)
- [ ] Example apps:
  - [ ] Simple dashboard
  - [ ] File browser
  - [ ] Live metrics viewer

---

## 🧠 LEARNING / RESEARCH

- [ ] Study ncurses screen diffing
- [ ] Study SadConsole rendering pipeline
- [ ] Read VT100 / ANSI escape code specs
- [ ] Review game-loop patterns for TUIs

---

## 🧹 CLEANUP

- [ ] Remove dead code / unused helpers
- [ ] Normalize naming and comments
- [ ] Add README diagrams (architecture + data flow)
- [ ] Add contribution notes for future refactors

---

_Last updated: keep this file honest. If something feels hard, break it down._
