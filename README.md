# VexAI — AI Media Processing Tool (Intel Arc Edition)

A standalone Windows CLI tool for AI-powered image generation, face swapping, video art, and voice conversion. Optimized for **Intel Arc GPUs** using DirectML and OpenVINO.

## Features

| Feature | Description |
|---|---|
| 🎨 **Image Generation** | Text-to-image via SD.Next (txt2img) |
| ✨ **Reimagine** | Transform an existing image (img2img) |
| 🎭 **Face Clone** | Generate scenes preserving a face identity (IP-Adapter) |
| 🔄 **Face Swap (Fast)** | Video face swap using DirectML (Intel Arc native) |
| 💎 **Face Swap (Enhanced)** | Higher quality swap using OpenVINO |
| 🎬 **Video Art** | Apply artistic styles to video frame by frame |
| 🎙️ **Voice Swap** | Replace voice in video using RVC |

## Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8)
- [FFmpeg](https://ffmpeg.org/download.html) (in PATH)
- [SD.Next](https://github.com/vladmandic/automatic) running locally
- [Deep-Live-Cam](https://github.com/hacksider/Deep-Live-Cam) (for face swap)
- [RVC](https://github.com/RVC-Project/Retrieval-based-Voice-Conversion-WebUI) (for voice swap)
- Intel Arc GPU recommended (DirectML / OpenVINO)

## Quick Start

```bash
git clone https://github.com/your-username/VexAI.git
cd VexAI
dotnet run
```

On first launch, the setup wizard will guide you through all configuration paths.

## Configuration

The setup wizard creates a `config.json` file in the application directory. You can re-run it anytime with the `config` command.

Required paths:
- **Output folder** — where generated files are saved
- **RVC folder** — root of your RVC installation (must contain `venv/` and `assets/weights/`)
- **Deep-Live-Cam Fast** — folder with DirectML version

Optional:
- **Deep-Live-Cam Enhanced** — folder with OpenVINO version
- **SD.Next .bat path** — to auto-start the image server
- **Watermark image** — a `.png` to overlay on outputs

## Commands

```
gerar        Generate image from text (txt2img)
reimaginar   Reimagine an existing image (img2img)
clonar       Clone a face into a scene (IP-Adapter)
faceswap     Swap face in video (DirectML or OpenVINO)
videoart     Apply artistic style to video frame by frame
voz          Swap voice in video using RVC
modelos      List available RVC voice models
fila         Show processing queue status
config       Reconfigure settings
sair         Exit
```

## Voice Models

Place `.pth` model files in your RVC installation under:
```
<rvc-folder>/assets/weights/ModelName.pth
```

They will be automatically listed when running the `modelos` or `voz` command.

## Notes

- SD.Next must be running before generating images. Set `SdNextBatPath` in config to auto-start it.
- Face swap uses Deep-Live-Cam. Fast mode uses DirectML (Intel Arc), Enhanced uses OpenVINO.
- Video Art processes video frame-by-frame through SD.Next — long videos take significant time.
- Voice swap separates vocals using UVR-MDX before applying RVC inference.

## License

MIT
