# Architecture

This document explains how **ConsoleUi** is structured internally and how data flows
through the system.

The core idea is to treat the console like a framebuffer-based rendering target,
similar to a very small GPU.

---

## High-Level Overview

ConsoleUi is split into **three major layers**:

```
┌───────────────────────────────┐
│ Applications                  │
│                               │
│  ConsoleUi.Demo               │
│  ConsoleUi.Logger             │
└───────────────┬───────────────┘
                │
┌───────────────▼───────────────┐
│ ConsoleUi.Core                │
│                               │
│  Layout Engine                │
│  Rendering Math               │
│  Core Primitives              │
│  Logging Contracts            │
└───────────────┬───────────────┘
                │
┌───────────────▼───────────────┐
│ Platform Backends             │
│                               │
│  WinConsoleBuffer (Windows)   │
│  VT Renderer (planned)        │
└───────────────────────────────┘
```

---

## Core Layer

### Primitives

Located in `ConsoleUi.Core.Primitives`.

- `Vec2` – 2D float vector for layout math
- `RectF` – Rectangle defined in logical space
- `Thickness` – Margin / padding abstraction

These types are **pure data** with no rendering or platform logic.

---

### Layout Engine

Located in `ConsoleUi.Core.Layout`.

Responsibilities:
- Compute element positions
- Apply margin and padding
- Produce final rectangles:
  - `BorderBox`
  - `ContentBox`

Layout is:
- deterministic
- single-pass
- easy to reason about

There is no implicit layout magic.

---

### Rendering Math

Located in `ConsoleUi.Core.Rendering`.

- `ConsoleTransform` converts logical coordinates into console cell space
- Enables resolution-independent UI logic

This layer knows nothing about:
- colors
- characters
- platform APIs

---

## Logger Architecture

### Data Flow

```
┌──────────────────┐
│ Demo App         │
│                  │
│ AsyncPipeLogger  │
└────────┬─────────┘
         │ enqueue
         ▼
┌──────────────────┐
│ Background Task  │
│                  │
│ Named Pipe       │
└────────┬─────────┘
         │ IPC
         ▼
┌──────────────────┐
│ Logger UI        │
│                  │
│ Framebuffer      │
└──────────────────┘
```

Key properties:
- Logging is non-blocking
- Backpressure is handled by dropping old messages
- Renderer never waits on IO

---

## Rendering Loop (Logger UI)

The logger UI runs two loops:

### Render Loop
- ~30 FPS
- Checks resize
- Draws only when dirty
- Presents full framebuffer

### Pipe Loop
- Blocks waiting for log data
- Parses log rows
- Marks UI as dirty

These loops are fully decoupled.

---

## Design Goals

- Explicit rendering
- Minimal hidden state
- Clear ownership boundaries
- Easy future platform expansion

---

## Future Extensions

- VT / ANSI renderer
- Input system
- GPU-like command buffers
- Scene graph abstraction
