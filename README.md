# VexAI — AI Media Processing Tool (Intel Arc Edition)

> A Windows CLI tool for AI-powered image generation, face swapping, video art, and voice conversion — optimized for **Intel Arc GPUs** via DirectML and OpenVINO.

---

## Table of Contents

- [Overview](#overview)
- [System Requirements](#system-requirements)
- [Installing Dependencies](#installing-dependencies)
  - [1. .NET 8 SDK](#1-net-8-sdk)
  - [2. FFmpeg](#2-ffmpeg)
  - [3. SD.Next (image generation)](#3-sdnext-image-generation)
  - [4. Deep-Live-Cam (face swap)](#4-deep-live-cam-face-swap)
  - [5. RVC (voice conversion)](#5-rvc-voice-conversion)
- [Installing VexAI](#installing-vexai)
- [Initial Setup](#initial-setup)
- [Available Commands](#available-commands)
  - [gerar — Generate image from text](#gerar--generate-image-from-text)
  - [reimaginar — Reimagine an existing image](#reimaginar--reimagine-an-existing-image)
  - [clonar — Clone a face into a scene](#clonar--clone-a-face-into-a-scene)
  - [faceswap — Swap face in video](#faceswap--swap-face-in-video)
  - [videoart — Apply art style to video](#videoart--apply-art-style-to-video)
  - [voz — Swap voice in video](#voz--swap-voice-in-video)
  - [modelos — List RVC models](#modelos--list-rvc-models)
  - [fila — Queue status](#fila--queue-status)
  - [config — Reconfigure](#config--reconfigure)
- [Voice Models (RVC)](#voice-models-rvc)
- [config.json Structure](#configjson-structure)
- [FAQ](#faq)

---

## Overview

VexAI is a command-line interface (CLI) that brings multiple AI tools together into a single unified interface. With it you can:

| Feature | Description |
|---|---|
| 🎨 **Generate image** | Create images from text (txt2img) via SD.Next |
| ✨ **Reimagine** | Transform an existing image with a new prompt (img2img) |
| 🎭 **Face Clone** | Generate scenes preserving a face identity (IP-Adapter) |
| 🔄 **Face Swap (Fast)** | Swap face in video using DirectML (Intel Arc native) |
| 💎 **Face Swap (Enhanced)** | Higher quality face swap using OpenVINO |
| 🎬 **Video Art** | Apply artistic styles to video frame by frame via SD.Next |
| 🎙️ **Voice Swap** | Replace the voice in a video using RVC |

---

## System Requirements

- **Operating System:** Windows 10 or Windows 11 (64-bit)
- **GPU:** Intel Arc recommended (compatible with DirectML and OpenVINO). NVIDIA GPUs also work via SD.Next.
- **RAM:** 16 GB minimum recommended
- **Storage:** 30–50 GB free minimum (AI models take up significant space)

---

## Installing Dependencies

Before using VexAI, you need to install and configure all the external tools listed below. Follow each step carefully.

---

### 1. .NET 8 SDK

VexAI is built in C# and requires .NET 8 to compile and run.

1. Go to: [https://dotnet.microsoft.com/en-us/download/dotnet/8](https://dotnet.microsoft.com/en-us/download/dotnet/8)
2. Download the **SDK** (not the Runtime) for Windows x64.
3. Run the installer and follow the instructions.
4. Verify the installation by opening a terminal and typing:
   ```
   dotnet --version
   ```
   It should return something like `8.0.x`.

---

### 2. FFmpeg

FFmpeg is used internally by VexAI to extract audio, mix tracks, and render final videos.

1. Go to: [https://ffmpeg.org/download.html](https://ffmpeg.org/download.html)
2. Under **Windows**, click on **Windows builds by BtbN** or **gyan.dev**.
3. Download the `ffmpeg-release-essentials.zip` file (or similar).
4. Extract the contents to a folder, for example: `C:\ffmpeg`
5. **Add FFmpeg to the system PATH:**
   - Open **Control Panel → System → Advanced system settings → Environment Variables**
   - Under "System variables", select `Path` and click **Edit**
   - Add the path to the FFmpeg `bin` folder, for example: `C:\ffmpeg\bin`
   - Click OK on all windows
6. Verify by opening a new terminal:
   ```
   ffmpeg -version
   ```

---

### 3. SD.Next (image generation)

SD.Next is the local image generation server (Stable Diffusion). It must be **running** whenever you use the `gerar`, `reimaginar`, `clonar`, or `videoart` commands.

1. Go to: [https://github.com/vladmandic/automatic](https://github.com/vladmandic/automatic)
2. Follow the installation instructions from the repository. In short:
   ```bash
   git clone https://github.com/vladmandic/automatic
   cd automatic
   ```
3. On first launch, SD.Next will automatically download the necessary models.
4. For Intel Arc GPUs, use **DirectML** mode. Edit `webui-user.bat` and add the flag:
   ```
   set COMMANDLINE_ARGS=--use-directml
   ```
5. Start the server by running `webui.bat` (or `webui-user.bat`). Wait until you see:
   ```
   Running on local URL:  http://127.0.0.1:7860
   ```
6. Save the full path to the `.bat` startup file — you will need it during VexAI configuration.

> **Tip:** If your PC has an NVIDIA GPU, you can use the standard SD.Next installation without the `--use-directml` flag.

#### Downloading an image model (optional but recommended)

SD.Next works with any `.safetensors` or `.ckpt` model compatible with Stable Diffusion. For better results:

1. Visit [https://civitai.com](https://civitai.com) or [https://huggingface.co](https://huggingface.co)
2. Download a model of your choice (e.g., **Realistic Vision**, **DreamShaper**, etc.)
3. Place the file in: `<sdnext>/models/Stable-diffusion/`
4. Restart SD.Next and select the model in the web interface (http://127.0.0.1:7860)

---

### 4. Deep-Live-Cam (face swap)

Deep-Live-Cam is used by the `faceswap` command. VexAI supports **two modes** with separate installations:

#### Fast Mode (DirectML — Intel Arc native)

1. Go to: [https://github.com/hacksider/Deep-Live-Cam](https://github.com/hacksider/Deep-Live-Cam)
2. Follow the installation instructions for **DirectML** (Intel Arc / AMD):
   ```bash
   git clone https://github.com/hacksider/Deep-Live-Cam
   cd Deep-Live-Cam
   pip install -r requirements.txt
   pip install onnxruntime-directml
   ```
3. Download the required models as indicated in the repository (usually there is a download script or automatic download link).
4. Save the path to the Deep-Live-Cam root folder (e.g., `C:\Deep-Live-Cam`).

#### Enhanced Mode (OpenVINO — higher quality)

1. In the same repository, follow the instructions to install with **OpenVINO**:
   ```bash
   pip install openvino
   pip install onnxruntime-openvino
   ```
2. Consider using a separate folder to avoid dependency conflicts.
3. Save the path to this installation folder as well.

> **Note:** Enhanced mode is optional. If you don't need maximum quality, fast mode alone is sufficient.

---

### 5. RVC (voice conversion)

RVC (Retrieval-based Voice Conversion) is used by the `voz` command to swap the voice in videos.

1. Go to: [https://github.com/RVC-Project/Retrieval-based-Voice-Conversion-WebUI](https://github.com/RVC-Project/Retrieval-based-Voice-Conversion-WebUI)
2. Follow the installation guide from the repository:
   ```bash
   git clone https://github.com/RVC-Project/Retrieval-based-Voice-Conversion-WebUI
   cd Retrieval-based-Voice-Conversion-WebUI
   pip install -r requirements.txt
   ```
3. The RVC folder structure must contain:
   ```
   <rvc-folder>/
   ├── venv/               ← Python virtual environment
   └── assets/
       └── weights/        ← where .pth voice models are placed
   ```
4. Download the required base models as indicated in the repository (usually available on the Releases tab or via download scripts).
5. Save the path to the RVC root folder (e.g., `C:\RVC-WebUI`).

#### Adding voice models

To use a specific voice, you need a `.pth` file trained with that voice:

1. Find models in the RVC community repository or train your own.
2. Place the `.pth` file in:
   ```
   <rvc-folder>/assets/weights/VoiceName.pth
   ```
3. VexAI will automatically list all models found in that folder.

---

## Installing VexAI

With all dependencies installed, now install VexAI itself:

```bash
git clone https://github.com/your-username/VexAI.git
cd VexAI
dotnet build
```

To run directly:

```bash
dotnet run
```

Or to create a compiled standalone executable:

```bash
dotnet publish -c Release -r win-x64 --self-contained
```

The executable will be generated at `bin/Release/net8.0/win-x64/publish/VexAI.exe`.

---

## Initial Setup

On **first launch**, VexAI automatically detects that it has not been configured and opens the **setup wizard**. You can also run it at any time with the `config` command.

The wizard will ask step by step:

| Prompt | What to enter |
|---|---|
| **Output folder** | Where generated files will be saved. E.g.: `C:\Users\Your Name\Documents\VexAI_Output` |
| **RVC root folder** | Path to the RVC installation. E.g.: `C:\RVC-WebUI` |
| **Deep-Live-Cam folder (Fast)** | Path to the DirectML installation. E.g.: `C:\Deep-Live-Cam` |
| **Deep-Live-Cam folder (Enhanced)** | Path to the OpenVINO installation. *(Optional — press Enter to skip)* |
| **SD.Next .bat path** | Full path to the SD.Next startup `.bat` file. E.g.: `C:\automatic\webui-user.bat` *(Optional)* |
| **SD.Next API URL** | Leave the default `http://127.0.0.1:7860` unless you use a different port |
| **Watermark image** | Path to a `.png` file to overlay on outputs. *(Optional)* |
| **Auto-start SD.Next?** | `y` to start SD.Next automatically when VexAI opens |

When finished, the settings are saved to a `config.json` file in the same folder as the executable.

---

## Available Commands

After setup, VexAI enters interactive mode. Type a command and press Enter.

```
VexAI> ajuda
```

---

### `gerar` — Generate image from text

Generates an image from a text description (txt2img) using SD.Next.

**Prerequisite:** SD.Next must be running at `http://127.0.0.1:7860`.

**Interactive steps:**
1. Enter the prompt describing the desired image
2. Choose the quality: `sd (512x512)`, `hd (720p)`, or `fullhd (1080p)`
3. Answer whether the content is 18+ (enables/disables content filters)

**Example:**
```
VexAI> gerar
Prompt: a futuristic city at night, neon lights, cyberpunk
Quality: hd (720p)
18+ content? n
```

---

### `reimaginar` — Reimagine an existing image

Transforms an existing image based on a new prompt (img2img).

**Prerequisite:** SD.Next must be running.

**Interactive steps:**
1. Provide the path to the input image (`.jpg`, `.png`, etc.)
2. Enter the prompt describing how to reimagine it
3. Set the change strength (0.1 = subtle change, 1.0 = full transformation)
4. Answer whether the content is 18+

**Example:**
```
VexAI> reimaginar
Image path: C:\photos\portrait.jpg
Prompt: oil painting, renaissance style
Change strength: 0.6
```

---

### `clonar` — Clone a face into a scene

Generates new images while preserving the identity of a face from a reference photo, using IP-Adapter.

**Prerequisite:** SD.Next must be running with IP-Adapter support.

**Interactive steps:**
1. Enter the prompt for the desired scene/style
2. Provide the path to the reference face photo

**Example:**
```
VexAI> clonar
Prompt: astronaut in space, cinematic lighting
Face photo path: C:\photos\my_face.jpg
```

---

### `faceswap` — Swap face in video

Replaces the face in a video with the face from a reference photo.

**Prerequisite:** Deep-Live-Cam installed (fast mode and/or enhanced mode).

**Interactive steps:**
1. Provide the path to the target video
2. Provide the path to the photo of the face to insert
3. Choose the mode: `fast (DirectML)` or `enhanced (OpenVINO)`

**Example:**
```
VexAI> faceswap
Video path: C:\videos\clip.mp4
Face photo path: C:\photos\face.jpg
Mode: fast (DirectML)
```

---

### `videoart` — Apply art style to video

Applies an artistic style to each frame of a video using SD.Next (frame-by-frame img2img).

**Prerequisite:** SD.Next must be running. **Warning:** long videos can take a very long time.

**Interactive steps:**
1. Provide the path to the video
2. Enter the desired artistic style prompt
3. Set the change strength (0.1 to 1.0)

**Example:**
```
VexAI> videoart
Video path: C:\videos\walk.mp4
Prompt: watercolor painting, soft colors
Strength: 0.5
```

---

### `voz` — Swap voice in video

Replaces the human voice in a video using an RVC model. The pipeline is:
1. Extracts audio from the video
2. Separates vocals from instrumentals using UVR-MDX
3. Converts the voice with the selected RVC model
4. Mixes the result and renders the final video

**Prerequisite:** RVC installed with at least one `.pth` model in `assets/weights/`.

**Interactive steps:**
1. Provide the path to the video
2. Choose the available voice model (listed automatically)
3. Set the pitch — positive values raise pitch, negative values lower it

**Example:**
```
VexAI> voz
Video path: C:\videos\interview.mp4
Model: MyVoiceModel
Pitch: 0
```

---

### `modelos` — List RVC models

Lists all `.pth` voice model files available in the RVC `assets/weights/` folder.

```
VexAI> modelos
```

---

### `fila` — Queue status

Shows how many jobs are waiting to be processed and whether one is currently running.

```
VexAI> fila
[Queue] Pending jobs: 2 | Running: True
```

---

### `config` — Reconfigure

Opens the setup wizard again to change any path or option. New settings are saved immediately to `config.json`.

```
VexAI> config
```

---

## Voice Models (RVC)

To use the `voz` command, you need trained RVC models (`.pth` files).

**Where to find models:**
- [https://huggingface.co](https://huggingface.co) — search for `rvc voice model`
- RVC-dedicated communities on Discord and Reddit
- You can train your own model using the RVC WebUI

**How to install a model:**
1. Download the desired `.pth` model file
2. Copy it to: `<rvc-folder>/assets/weights/VoiceName.pth`
3. VexAI will automatically detect it the next time you run `modelos` or `voz`

---

## config.json Structure

The `config.json` file is created automatically in the executable's folder. You can edit it manually if you prefer:

```json
{
  "SdNextUrl": "http://127.0.0.1:7860",
  "SdNextBatPath": "C:\\automatic\\webui-user.bat",
  "RvcFolderPath": "C:\\RVC-WebUI",
  "DeepLiveFastPath": "C:\\Deep-Live-Cam",
  "DeepLiveEnhancedPath": "C:\\Deep-Live-Cam-OpenVINO",
  "WatermarkImagePath": "C:\\logos\\watermark.png",
  "OutputFolder": "C:\\Users\\Your Name\\Documents\\VexAI_Output",
  "AutoStartSdNext": true
}
```

| Field | Description |
|---|---|
| `SdNextUrl` | SD.Next API URL (default: `http://127.0.0.1:7860`) |
| `SdNextBatPath` | Path to the SD.Next startup `.bat` file (optional) |
| `RvcFolderPath` | RVC root folder (must contain `venv/` and `assets/weights/`) |
| `DeepLiveFastPath` | Deep-Live-Cam folder with DirectML |
| `DeepLiveEnhancedPath` | Deep-Live-Cam folder with OpenVINO (optional) |
| `WatermarkImagePath` | `.png` watermark image path (optional) |
| `OutputFolder` | Folder where all generated files are saved |
| `AutoStartSdNext` | `true` to start SD.Next automatically when VexAI opens |

---

## FAQ

**Does SD.Next need to be running to use VexAI?**  
Yes, for the `gerar`, `reimaginar`, `clonar`, and `videoart` commands. For `faceswap` and `voz`, SD.Next is not required. If you set `SdNextBatPath` and enabled `AutoStartSdNext`, VexAI will attempt to start SD.Next automatically.

**Does VexAI work with NVIDIA or AMD GPUs?**  
Yes. Intel Arc is recommended because it natively supports DirectML and OpenVINO, but SD.Next and Deep-Live-Cam support CUDA (NVIDIA) and ROCm (AMD). Adjust the installations according to each tool's own guide.

**Where are the generated files saved?**  
Everything is saved in the folder defined by `OutputFolder`. Inside it, VexAI organizes files into subfolders:
- `output/images/` — generated images
- `output/audio/` — intermediate voice audio files
- `output/video/` — final faceswap and voiceswap videos

**How do I reconfigure VexAI after setup?**  
Use the `config` command inside the program, or manually edit the `config.json` file.

**Video processing is taking too long.**  
The `videoart` command processes frame by frame through SD.Next — long videos are expected to take a long time. Use short videos (under 30 seconds) for initial testing.

---

## License

MIT
