# Roadmap

This roadmap reflects the long-term vision for **ConsoleUi** as a
cross-platform console UI framework.

---

## Phase 1 – Foundation (Current)

- [X] Core primitives
- [x] Deterministic layout engine
- [x] Windows framebuffer renderer
- [x] Async pipe-based logging
- [x] Dedicated log viewer UI

---

## Phase 2 – Rendering Abstraction

- [ ] `IConsoleSurface`
- [ ] Renderer capability detection
- [ ] Framebuffer vs VT selection
- [ ] Platform-agnostic render API

---

## Phase 3 – Cross Platform

- [ ] VT / ANSI renderer
- [ ] Linux support
- [ ] macOS support
- [ ] Terminal capability probing

---

## Phase 4 – Input System

- [ ] Keyboard input abstraction
- [ ] Mouse input (where supported)
- [ ] Focus management
- [ ] Event routing

---

## Phase 5 – UI Features

- [ ] Text wrapping
- [ ] Clipping
- [ ] Scroll containers
- [ ] Panels and stacks
- [ ] Theming system

---

## Phase 6 – Developer Experience

- [ ] Stable public API
- [ ] Documentation site
- [ ] Examples gallery
- [ ] NuGet packages

---

## Long-Term Ideas

- GPU-style render command buffers
- Retained-mode UI layer
- Terminal animations
- Debug layout visualizer
