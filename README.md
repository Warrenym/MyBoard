# MyBoard

MyBoard is a desktop whiteboard application built with **C#** and **WPF** that allows users to freely organize ideas, reference images, and notes on an infinite canvas. It combines the visual organization of applications like Milanote and PureRef while being designed specifically around my own workflow.

> **Status:** Personal project (actively developed)

---

## About Me

I'm a Computer Science student who enjoys building desktop applications and game development. C# is my favorite programming language, and this project serves as both a tool I use daily and a way to continuously improve my understanding of software design, WPF, and application architecture.

MyBoard is a reflection of my learning journey—each feature represents something new I challenged myself to build.

---

## Why I Built This

This project is not intended for public release—it was built because I couldn't find a reference board application that fit exactly what I wanted.

As both a **programmer** and a **character artist**, I constantly collect reference images for drawings, game development, UI inspiration, and worldbuilding. Applications like **PureRef** and **Milanote** are excellent, but their free versions impose limitations that interrupted my workflow.

Instead of searching for another alternative, I decided to build one myself.

What began as a simple image board gradually evolved into a complete whiteboard application with nested boards, rich text notes, customizable colors, editing tools, and an infinite canvas.

This project has become my playground for learning software architecture, WPF, UI/UX design, and modern C# development.

---

## Technologies

* C#
* .NET
* WPF (Windows Presentation Foundation)
* MVVM Architecture

---


## Features

### Infinite Canvas

* Infinite/scrollable freeform workspace
* Absolute positioning of items
* Zoom and canvas panning
* Multi-selection support
* Drag-and-drop item movement

### Boards

* Create multiple boards
* Nested boards (boards within boards)
* Dynamic breadcrumb navigation
* Board renaming
* Board color customization
* Responsive sidebar navigation

### Notes

* Rich text editing
* Multiple text styles
* Paragraph support
* Resizable note windows
* Floating text-style toolbar
* Improved Enter key behavior
* Consistent paragraph spacing

### Images

* Drag-and-drop images directly into the board
* Paste images from the Windows clipboard, including screenshots, copied files, browser images, and image URLs
* Image resizing
* Preserved aspect ratio
* Automatic removal of unnecessary white padding

### Editing Tools

* Undo / Redo
* Copy
* Cut
* Paste
* Duplicate
* Right-click context menu
* Delete items

### Design & Customization

* Integrated color picker
* Default colors
* Saved colors
* Recently used colors
* Custom embedded fonts
* Modern UI styling

### Quality of Life

* Fullscreen on launch
* Improved sidebar animations
* Responsive layouts
* Better text editing experience
* Numerous bug fixes and UI refinements

### Data Sync

Use **Data location…** in the top bar to choose the exact synced folder on each
computer. The selection is stored locally per PC, so different Windows usernames
and OneDrive locations are supported. Selecting an empty folder safely copies the
current board, palette, images, and backups. Selecting a folder that already has
`board.json` offers to load that board instead of overwriting it.

Image paths inside `board.json` are relative to the data folder. MyBoard also
keeps timestamped backups and detects when OneDrive changes the board while the
app is open, preventing a stale instance from overwriting newer synced data.

Mark the selected folder as **Always keep on this device**, let OneDrive finish
syncing before switching computers, and avoid editing the board on both PCs at
the same time because OneDrive cannot merge simultaneous edits to `board.json`.

---

## Current Features Roadmap

### ✅ Implemented

* Infinite canvas
* Nested boards
* Rich text notes
* Image support
* Zoom and panning
* Undo/Redo
* Multi-select
* Drag & Drop
* Board customization
* Context menus
* Color picker
* Responsive sidebar
* Modern UI styling

### 🚧 Planned

* Markdown support
* Better code block editing
* Keyboard shortcuts
* Search functionality
* File attachments
* Export / Import boards
* Performance optimizations for very large boards

---
