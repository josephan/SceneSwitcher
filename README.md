# Scene Switcher for Unity

A Unity Editor extension to quickly open recent scenes with keyboard shortcuts and search functionality.

## Features

- **Recent Scenes List**: Automatically tracks and displays your most recently used scenes
- **Keyboard Shortcuts**: Use number keys 0-9 (and Shift+0-9) to quickly switch between scenes
- **Additive Scene Loading**: Ctrl/Cmd-click to load scenes additively
- **Play Mode Testing**: Alt-click to play a scene and automatically restore your previous scene when done
- **Project Window Integration**: Highlight or select scene assets directly from the switcher
- **Smart Scene Search**: Automatically includes build settings scenes and can search your entire project
- **Cross-Project Support**: Remembers recent scenes across multiple Unity projects

## Installation

### Install via Git URL (Recommended)

1. Open Unity Editor
2. Go to **Window > Package Manager**
3. Click the **+** button in the top-left corner
4. Select **Add package from git URL...**
5. Paste the following URL:
   ```
   https://github.com/josephan/SceneSwitcher.git
   ```
6. Click **Add**

### Install from Local Folder

1. Clone or download this repository
2. Open Unity Editor
3. Go to **Window > Package Manager**
4. Click the **+** button in the top-left corner
5. Select **Add package from disk...**
6. Navigate to the cloned repository and select `package.json`
7. Click **Open**

## Usage

### Opening the Scene Switcher

There are two ways to open the Scene Switcher window:

#### Windows:
- Press **Ctrl+Shift+O**
- Or go to **File > Open Recent Scene...**

#### macOS:
- Press **Cmd+Shift+O**
- Or go to **File > Open Recent Scene...**

### Basic Scene Loading

- **Click a scene button**: Load that scene
- **Press number keys 0-9**: Load scenes 0 through 9
- **Press Shift+0-9**: Load scenes 10 through 19
- **Press Enter**: Search for all scenes in your project (can be slow on large projects)

### Advanced Features

#### Additive Scene Loading
- **Ctrl-click (Windows)** or **Cmd-click (macOS)** on a scene button to load it additively into your current scene

#### Play Mode Testing
- **Alt-click** on a scene button to:
  1. Load that scene
  2. Enter Play Mode
  3. Automatically restore your previous scene when you exit Play Mode
- **Ctrl+Alt-click (Windows)** or **Cmd+Alt-click (macOS)**: Load additively, then play

#### Project Window Integration
- **Click the bullet (•) button**: Highlight the scene in the Project window
- **Alt-click the bullet button**: Select the scene asset
- **Ctrl-click (Windows)** or **Cmd-click (macOS)** **the bullet button**: Multi-select scene assets
- **Hover over the bullet button**: See the full path to the scene file

### Other Shortcuts

- **Spacebar**: Toggle "Close dialog after load" option
- **F1**: Display help information
- **Esc**: Close the Scene Switcher window

## Configuration

The Scene Switcher stores its preferences in Unity's EditorPrefs:
- Recently used scenes per project
- "Close dialog after load" setting
- Scene restore data for play mode testing

All preferences are automatically managed and persist between Unity sessions.

## Requirements

- Unity 2020.3 or later

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.