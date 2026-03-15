# Meshy Rhino Plugin

A Rhino plugin that enables AI-powered 3D model generation from [Meshy](https://www.meshy.ai/) directly inside Rhinoceros 8. Generate 3D meshes from text prompts or reference images and place them into your Rhino document as mesh objects or block instances.

## Features

* **Text to 3D**: Generate 3D models from text descriptions (two-stage preview + refine workflow, or preview-only mode)
* **Image to 3D**: Create 3D models from a single reference image (.jpg, .png)
* **Multi-Image to 3D**: Provide 1-4 images of the same object from different angles for higher-quality results
* **Text to Texture**: Apply new textures to existing 3D models via text prompts or reference images (retexture workflow)
* **GLB Native Import (Default)**: Import models via Rhino's native GLB importer, preserving PBR materials and textures
* **OBJ Custom Parser**: Alternative import path with per-face-vertex UV splitting for correct texture seams
* **Mesh Placement**: Place generated meshes directly into the active Rhino document (OBJ mode)
* **Block Instance**: Save generated meshes as block definitions and place as instances (OBJ mode)
* **Unit-Aware**: Automatically converts from Meshy output (meters) to your document's unit system
* **Cancellable Generation**: Cancel button allows aborting any in-progress generation task
* **Configurable Generation**: Control topology, polycount, AI model version, model type, symmetry, PBR maps, and lighting removal
* **Thumbnail Preview**: Shows a thumbnail of the generated model before placement
* **Persistent Defaults**: Save your preferred generation options in settings -- they apply automatically on launch
* **Retry Logic**: Automatic retry with exponential backoff for transient network failures

## Requirements

* Rhinoceros 8 (Windows)
* [Meshy account](https://www.meshy.ai/) with an API key ([get one here](https://www.meshy.ai/settings/api))
* Visual Studio 2022 (for building from source)

## Installation

### Option 1: Build from Source

1. Clone this repository:
   ```
   git clone https://github.com/QROST/Meshy-Rhino.git
   ```
2. Open `Meshy-Rhino.sln` in Visual Studio 2022
3. Build the solution in Release configuration
4. In Rhino, run `_PlugInManager`, click **Install**, and select the built `MeshyRhino.dll`

### Option 2: Download Release

1. Download `MeshyRhino.zip` from the [Releases](https://github.com/QROST/Meshy-Rhino/releases) page
2. Extract into a folder (e.g. `%APPDATA%\McNeel\Rhinoceros\8.0\Plug-ins\MeshyRhino\`)
3. In Rhino, run `_PlugInManager`, click **Install**, and select `MeshyRhino.dll`
4. Restart Rhino

### Option 3: Yak Package Manager

A `manifest.yml` is included for Yak packaging. Build the `.yak` package with:
```
"C:\Program Files\Rhino 8\System\Yak.exe" build
```

## Usage

### Setting Up the API Key

1. In Rhino, type `Meshy3DGenerator` in the command line
2. On first launch, click **Settings** and enter your Meshy API key (`msy_...`)
3. The key is stored locally at `%APPDATA%\MeshyRhino\settings.json`

### Generating a 3D Model

1. Run the `Meshy3DGenerator` command
2. Choose a tab: **Text-to-3D**, **Image-to-3D**, **Multi-Image-to-3D**, or **Text-to-Texture**
3. Enter your prompt or select image(s)
4. Configure generation options:
   - **AI Model**: Meshy-6 (latest) or Meshy-5
   - **Model Type**: Standard or Low Poly
   - **Topology**: Triangle or Quad mesh
   - **Target Polycount**: 100 -- 300,000
   - **Symmetry**: Auto, On, or Off
   - **PBR Maps**: Enable metallic, roughness, and normal map generation
   - **Remove Lighting**: Strip highlights/shadows for cleaner results under custom lighting
5. Select output format:
   - **GLB (Native Import)** (default): Uses Rhino's built-in importer for full PBR material support
   - **OBJ (Custom Parser)**: Parses OBJ geometry and downloads textures; choose **Mesh Object** or **Block Instance** placement
6. Click **Generate** -- the plugin submits the task, shows real-time progress with a thumbnail preview, and places the result in Rhino
7. Click **Cancel** at any time to abort an in-progress generation

### Text-to-Texture (Retexture)

1. Switch to the **Text-to-Texture** tab
2. Provide a source model:
   - **From previous task ID**: Enter the ID of a completed Text-to-3D or Image-to-3D task
   - **From model file**: Browse for a .glb, .obj, .fbx, or .stl file
3. Choose a style source:
   - **Text prompt**: Describe the desired texture
   - **Reference image**: Select a .jpg or .png image
4. Optionally toggle "Preserve original UVs"
5. Click **Retexture**

### Settings

Click **Settings** in the generator window to configure:
- **API Key**: Your Meshy API key
- **Default AI Model, Model Type, Topology, Polycount, Symmetry, Format**: Saved and applied automatically on launch
- **Enable PBR**: Default PBR map generation toggle
- **Poll Interval**: How frequently to check task progress (ms)
- **API Retry Count**: Number of retries for transient failures

## How It Works

1. User input is collected in a **modeless WPF window** (Rhino remains interactive)
2. API calls and model download run on a **background thread** with configurable retry logic
3. **GLB mode**: The downloaded GLB file is imported via Rhino's native `_-Import` command, preserving PBR materials. The temp file is cleaned up after import.
4. **OBJ mode**: The downloaded OBJ is parsed with per-face-vertex UV splitting (no seam artifacts). Vertex coordinates are converted from meters to the document's unit system via `RhinoMath.UnitScale`. Textures are saved to `%APPDATA%\MeshyRhino\textures\` so material references persist.
5. The mesh is placed in the document on Rhino's main thread via `RhinoApp.InvokeOnUiThread`
6. A **thumbnail preview** is displayed after generation completes (loaded from the API's `thumbnail_url`)

## Build

```bash
# Build for Release
dotnet build Meshy-Rhino.sln --configuration Release

# Build for Debug
dotnet build Meshy-Rhino.sln --configuration Debug
```

## Troubleshooting

**Plugin does not load in Rhino:**
* Ensure you are running Rhino 8 on Windows
* Check that all DLLs from the release zip are in the same folder
* Try running `_PlugInManager` and verify the plugin is listed and enabled

**API key rejected:**
* Ensure your key starts with `msy_` and has not been revoked
* Check your internet connection
* Verify credits are available on your Meshy account

**Mesh not appearing:**
* Make sure a document is open in Rhino
* Check the Rhino command line for error messages
* Very high polycount meshes may need the target reduced

**Network timeouts:**
* Increase the retry count in Settings (default: 2)
* Check your internet stability
* Large GLB files may need up to 5 minutes to download

## API Reference

This plugin uses the [Meshy REST API](https://docs.meshy.ai/):

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/openapi/v2/text-to-3d` | POST | Create text-to-3D task (preview or refine) |
| `/openapi/v2/text-to-3d/:id` | GET | Retrieve task status and model URLs |
| `/openapi/v1/image-to-3d` | POST | Create image-to-3D task |
| `/openapi/v1/image-to-3d/:id` | GET | Retrieve task status and model URLs |
| `/openapi/v1/multi-image-to-3d` | POST | Create multi-image-to-3D task |
| `/openapi/v1/multi-image-to-3d/:id` | GET | Retrieve task status and model URLs |
| `/openapi/v1/retexture` | POST | Create text-to-texture retexture task |
| `/openapi/v1/retexture/:id` | GET | Retrieve retexture task status and model URLs |

Authentication: `Authorization: Bearer msy_...` header on all requests.

## Contributing

Contributions are welcome. Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/YourFeature`)
3. Commit your changes (`git commit -m 'Add YourFeature'`)
4. Push to the branch (`git push origin feature/YourFeature`)
5. Open a Pull Request

## Changelog

### Version 0.3.0 (Current)

* **Text-to-Texture (Retexture)**: New tab for applying textures to existing models via text prompts or reference images, using the `/openapi/v1/retexture` endpoint
* **Expanded settings window**: Configure all defaults (AI model, model type, topology, polycount, symmetry, format, PBR, poll interval, retry count) -- not just the API key
* **Persistent defaults**: Saved settings are automatically applied when the generator window opens; re-applied after editing settings
* **Preview-only mode**: Skip the refine step in Text-to-3D for faster/cheaper preview mesh generation
* **Thumbnail preview**: Shows the generated model thumbnail in the UI after task completion
* **New generation options**: Model type (standard/low poly), symmetry mode (auto/on/off), remove lighting toggle
* **Retry logic**: Automatic retry with exponential backoff for API calls and downloads (configurable retry count)
* **Yak package manifest**: `manifest.yml` included for Rhino package manager distribution
* Version bump to 0.3.0

### Version 0.2.0

* GLB native import as the default format with full PBR material preservation
* OBJ import with per-face-vertex UV splitting for correct texture seam rendering
* Cancel button for aborting in-progress generation tasks
* Temp file cleanup after GLB import; persistent texture storage for OBJ
* Stable assembly and plugin GUIDs for consistent Rhino plugin identity
* Filename sanitization for prompts containing special characters
* Download timeout configuration to handle large files reliably
* Settings window properly owned by Rhino on first launch

### Version 0.1.0

* Initial release
* Text-to-3D with preview + refine workflow
* Image-to-3D single image generation
* Multi-Image-to-3D (1-4 images)
* Mesh Object and Block Instance output modes
* Unit-aware placement (meters to document units)
* Configurable topology, polycount, AI model, and PBR options
* Modeless WPF UI with real-time progress tracking
* API key persistence to user AppData

## License

This project is licensed under the [GNU General Public License v3.0](LICENSE).

## Support

* Meshy Documentation: [docs.meshy.ai](https://docs.meshy.ai/)
* Issues: Report via the repository issue tracker

---

Made by QROST
