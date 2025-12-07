# eStarter

A Windows 8/10 Metro-style desktop launcher platform built with .NET 8 and WPF.

## Features

### 🎨 Authentic Metro UI Design
- **Flat Design**: Pure Metro style with sharp corners, no shadows or gradients
- **Segoe UI Typography**: Using authentic Windows fonts with Light/SemiLight weights
- **Metro Color Palette**: 12 Windows accent colors including the iconic blue (#0078D7)
- **Metro Animations**: 
  - Slide-in entrance animation from right with exponential easing
  - Subtle hover overlay (8% white)
  - Press animation with scale-down and dark overlay
  - Smooth transitions throughout

### 📐 Flexible Tile Sizes
- **Small** (70×70): Compact tiles for quick access
- **Medium** (150×150): Standard tile size (default)
- **Wide** (310×150): Double-width tiles with extended description
- **Large** (310×310): Maximum size for prominent apps

### 🎯 Personalization
- **Right-click Context Menu** on any tile:
  - **Resize**: Cycle through Small → Medium → Wide → Large
  - **Change Color**: Cycle through 12 Windows accent colors
- **Persistent Settings**: Tile configurations automatically saved to `%LOCALAPPDATA%\eStarter\tiles.json`

### 🔔 Live Badges
- Red notification badges (Metro style with sharp corners)
- Auto-hide when count is 0
- Positioned at top-right corner of each tile

### 📦 App Management
- Install apps from ZIP packages (`sample.app.zip`)
- Apps stored in `%LOCALAPPDATA%\eStarter\apps`
- Simple launcher: finds and executes `.exe` files
- Process isolation for each launched app

## Getting Started

### Prerequisites
- .NET 8 SDK
- Windows 10/11 (or enable Windows targeting for cross-platform builds)

### Build
```bash
dotnet build
```

### Run
```bash
dotnet run
```

## Architecture

### Key Components
- **Models/AppEntry.cs**: Tile data model with INotifyPropertyChanged for live updates
- **ViewModels/MainViewModel.cs**: MVVM pattern with commands for all user actions
- **MainWindow.xaml**: Metro UI with authentic Windows 8/10 tile styling
- **Services/SettingsService.cs**: JSON-based persistence for tile configurations
- **Services/AppInstaller.cs**: ZIP extraction and app installation
- **Core/AppManager.cs**: App lifecycle management
- **Converters/**: XAML value converters for tile sizing and visibility

### Metro Design Principles Applied
1. **Content over chrome**: Minimal UI, focus on tiles
2. **Typography-based design**: Segoe UI Light for hierarchy
3. **Flat and clean**: No skeuomorphism, pure colors
4. **Fast and fluid**: Smooth, purposeful animations
5. **Authentically digital**: Sharp edges, grid layout

## Personalization

### Changing Tile Size
Right-click any tile → "Resize"
- Cycles: Small → Medium → Wide → Large → Small

### Changing Tile Color
Right-click any tile → "Change color"
- Cycles through 12 authentic Windows accent colors

### Settings Persistence
All tile customizations are automatically saved to:
```
%LOCALAPPDATA%\eStarter\tiles.json
```

## Demo Tiles

The app includes 8 demo tiles showcasing different configurations:
- Mail (Medium, Blue, Badge: 5)
- Calendar (Medium, Cyan)
- Photos (Wide, Red)
- Music (Medium, Orange)
- Store (Medium, Light Blue)
- News (Wide, Purple, Badge: 12)
- Weather (Medium, Teal)
- Settings (Small, Gray)

## Technical Stack
- **.NET 8**: Modern C# with nullable reference types
- **WPF**: Windows Presentation Foundation
- **MVVM**: Model-View-ViewModel architecture
- **System.Text.Json**: Lightweight serialization
- **INotifyPropertyChanged**: Reactive UI updates

## License
This project is provided as-is for demonstration purposes.
