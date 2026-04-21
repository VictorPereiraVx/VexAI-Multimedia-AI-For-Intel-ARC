using Newtonsoft.Json;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using VexAI.Config;

namespace VexAI.Services
{
    public class ImageService
    {
        private readonly HttpClient _http;
        private readonly string _outputFolder;
        private readonly AppConfig _config;

        public ImageService(AppConfig config)
        {
            _config = config;
            _outputFolder = string.IsNullOrWhiteSpace(config.OutputFolder)
                ? Path.Combine(AppContext.BaseDirectory, "output", "images")
                : Path.Combine(config.OutputFolder, "images");

            Directory.CreateDirectory(_outputFolder);

            _http = new HttpClient { BaseAddress = new Uri(config.SdNextUrl) };
            _http.Timeout = TimeSpan.FromMinutes(10);
        }

        public async Task<string> GenerateImageAsync(string prompt, int width, int height, bool upscale, bool nsfw)
        {
            string negativePrompt = nsfw
                ? "low quality, ugly, blurry, watermark, bad anatomy, deformed, disfigured"
                : "nsfw, nude, naked, porn, uncensored, nipples, sex, violence, blood, gore, low quality, ugly, blurry, watermark, bad anatomy, deformed";

            var payload = new
            {
                prompt,
                negative_prompt = negativePrompt,
                steps = 30,
                width,
                height,
                cfg_scale = 7,
                sampler_name = "Euler a",
                batch_size = 1
            };

            try
            {
                Console.WriteLine($"[Image] Gerando {width}x{height}: {prompt}");

                var response = await _http.PostAsync("/sdapi/v1/txt2img",
                    new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));

                response.EnsureSuccessStatusCode();

                dynamic json = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());
                string base64 = json.images[0];

                if (upscale)
                    base64 = await ApplyUpscaleAsync(base64) ?? base64;

                return await SaveImageAsync(base64, "img");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Image] Erro: {ex.Message}");
                return null;
            }
        }

        public async Task<string> ReimagineImageAsync(string prompt, string base64Input, float denoise, int width, int height, bool nsfw)
        {
            string negativePrompt = nsfw
                ? "low quality, ugly, blurry, watermark, bad anatomy"
                : "nsfw, nude, naked, low quality, ugly, blurry, watermark, bad anatomy";

            var payload = new
            {
                init_images = new[] { base64Input },
                prompt,
                negative_prompt = negativePrompt,
                denoising_strength = denoise,
                steps = 30,
                width,
                height,
                cfg_scale = 7,
                sampler_name = "Euler a",
                batch_size = 1
            };

            try
            {
                var response = await _http.PostAsync("/sdapi/v1/img2img",
                    new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));

                if (!response.IsSuccessStatusCode) return null;

                dynamic json = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());
                return await SaveImageAsync((string)json.images[0], "reimagine");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Reimagine] Erro: {ex.Message}");
                return null;
            }
        }

        public async Task<string> GenerateWithFaceAsync(string prompt, string base64Face)
        {
            var adapterConfig = new
            {
                enabled = true,
                model = "ip-adapter-plus-face_sd15.bin",
                image = base64Face,
                weight = 0.7,
                begin_step = 0.0,
                end_step = 1.0,
                resize_mode = 1
            };

            var payload = new
            {
                prompt = prompt + ", (detailed face:1.2), high quality, 8k, realistic texture",
                negative_prompt = "blurry, ugly, distorted face, bad anatomy, cartoon, low quality",
                steps = 25,
                width = 512,
                height = 768,
                cfg_scale = 6,
                sampler_name = "Euler a",
                alwayson_scripts = new Dictionary<string, object>
                {
                    { "ip adapter", new { args = new[] { adapterConfig } } }
                }
            };

            try
            {
                var response = await _http.PostAsync("/sdapi/v1/txt2img",
                    new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));

                if (!response.IsSuccessStatusCode) return null;

                dynamic json = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());
                if (json.images == null) return null;

                return await ApplyWatermarkAsync(await SaveImageAsync((string)json.images[0], "clone"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FaceClone] Erro: {ex.Message}");
                return null;
            }
        }

        public async Task<string> GenerateFastFaceSwapAsync(byte[] videoBytes, string base64Face, Action<string> onProgress = null, bool applyWatermark = true)
        {
            string deepLivePath = _config.DeepLiveFastPath;
            string pythonExe = Path.Combine(deepLivePath, "venv", "Scripts", "python.exe");

            string tempVideo = Path.Combine(Path.GetTempPath(), $"target_{Guid.NewGuid()}.mp4");
            string tempFace = Path.Combine(Path.GetTempPath(), $"source_{Guid.NewGuid()}.jpg");
            string outputVideo = Path.Combine(_outputFolder, $"faceswap_fast_{Guid.NewGuid()}.mp4");

            try
            {
                await File.WriteAllBytesAsync(tempVideo, videoBytes);
                await File.WriteAllBytesAsync(tempFace, Convert.FromBase64String(base64Face));

                onProgress?.Invoke("Iniciando Face Swap (DirectML)...");

                string args = $"/c cd /d \"{deepLivePath}\" && \"{pythonExe}\" run.py --execution-provider dml --execution-threads 1 --frame-processor face_swapper --source \"{tempFace}\" --target \"{tempVideo}\" --output \"{outputVideo}\" --keep-fps --video-encoder libx264 --many-faces";

                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = new Process { StartInfo = psi };
                proc.Start();

                var tOut = Task.Run(async () => { while (!proc.StandardOutput.EndOfStream) await proc.StandardOutput.ReadLineAsync(); });
                var tErr = Task.Run(async () =>
                {
                    while (!proc.StandardError.EndOfStream)
                    {
                        string line = await proc.StandardError.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var match = Regex.Match(line, @"Processing:\s+(\d+)%");
                        if (match.Success)
                            onProgress?.Invoke($"Processando: {match.Groups[1].Value}%");
                    }
                });

                await Task.WhenAll(tOut, tErr, proc.WaitForExitAsync());

                try { File.Delete(tempVideo); File.Delete(tempFace); } catch { }

                if (File.Exists(outputVideo) && new FileInfo(outputVideo).Length > 0)
                    return applyWatermark ? await ApplyWatermarkAsync(outputVideo) : outputVideo;

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FastSwap] Erro: {ex.Message}");
                return null;
            }
        }

        public async Task<string> GenerateEnhancedFaceSwapAsync(byte[] videoBytes, string base64Face, Action<string> onProgress = null)
        {
            string deepLivePath = _config.DeepLiveEnhancedPath;
            string pythonExe = Path.Combine(deepLivePath, "venv", "Scripts", "python.exe");

            string tempVideo = Path.Combine(Path.GetTempPath(), $"target_enh_{Guid.NewGuid()}.mp4");
            string tempFace = Path.Combine(Path.GetTempPath(), $"source_enh_{Guid.NewGuid()}.jpg");
            string outputVideo = Path.Combine(_outputFolder, $"faceswap_enh_{Guid.NewGuid()}.mp4");

            try
            {
                await File.WriteAllBytesAsync(tempVideo, videoBytes);
                await File.WriteAllBytesAsync(tempFace, Convert.FromBase64String(base64Face));

                onProgress?.Invoke("Iniciando Face Swap (OpenVINO)...");

                string args = $"/c cd /d \"{deepLivePath}\" && \"{pythonExe}\" run.py --execution-provider openvino --execution-threads 2 --frame-processor face_swapper face_enhancer --source \"{tempFace}\" --target \"{tempVideo}\" --output \"{outputVideo}\" --keep-fps --video-encoder libx264 --many-faces";

                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = new Process { StartInfo = psi };
                proc.Start();

                var tOut = Task.Run(async () => { while (!proc.StandardOutput.EndOfStream) await proc.StandardOutput.ReadLineAsync(); });
                var tErr = Task.Run(async () =>
                {
                    while (!proc.StandardError.EndOfStream)
                    {
                        string line = await proc.StandardError.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var match = Regex.Match(line, @"Processing:\s+(\d+)%");
                        if (match.Success)
                            onProgress?.Invoke($"Processando: {match.Groups[1].Value}%");
                    }
                });

                await Task.WhenAll(tOut, tErr, proc.WaitForExitAsync());

                try { File.Delete(tempVideo); File.Delete(tempFace); } catch { }

                if (File.Exists(outputVideo) && new FileInfo(outputVideo).Length > 0)
                    return await ApplyWatermarkAsync(outputVideo);

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EnhancedSwap] Erro: {ex.Message}");
                return null;
            }
        }

        public async Task<string> ReimagineVideoAsync(string videoPath, string prompt, float denoise, Action<string> onProgress = null)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"videoart_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                onProgress?.Invoke("Extraindo frames...");

                string framePattern = Path.Combine(tempDir, "frame_%04d.jpg").Replace("\\", "/");
                var psiExtract = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-i \"{videoPath}\" -t 10 -vf \"scale=-2:480,fps=10\" -q:v 2 \"{framePattern}\" -y",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using (var p = Process.Start(psiExtract)) await p.WaitForExitAsync();

                var frames = Directory.GetFiles(tempDir, "*.jpg").OrderBy(f => f).ToList();
                if (frames.Count == 0) return null;

                onProgress?.Invoke($"Reimaginando {frames.Count} frames...");

                for (int i = 0; i < frames.Count; i++)
                {
                    byte[] bytes = await File.ReadAllBytesAsync(frames[i]);
                    string processed = await ProcessSingleFrameAsync(Convert.ToBase64String(bytes), prompt, denoise);
                    if (processed != null && File.Exists(processed))
                        File.Move(processed, frames[i], overwrite: true);

                    if (i % 5 == 0)
                        onProgress?.Invoke($"Processando: {i}/{frames.Count} frames ({(int)((float)i / frames.Count * 100)}%)");
                }

                onProgress?.Invoke("Renderizando vídeo final...");

                string outputVideo = Path.Combine(_outputFolder, $"videoart_{Guid.NewGuid()}.mp4");
                string inputPattern = framePattern;

                var psiStitch = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-r 10 -f image2 -i \"{inputPattern}\" -i \"{videoPath}\" -map 0:v -map 1:a? -c:v libx264 -pix_fmt yuv420p -shortest -y \"{outputVideo}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true
                };

                using (var p = Process.Start(psiStitch)) await p.WaitForExitAsync();

                if (!File.Exists(outputVideo) || new FileInfo(outputVideo).Length == 0) return null;

                return await ApplyWatermarkAsync(outputVideo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VideoArt] Erro: {ex.Message}");
                return null;
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        public async Task<string> CompressVideoAsync(string inputPath)
        {
            long size = new FileInfo(inputPath).Length;
            long limit = 9 * 1024 * 1024;

            if (size <= limit) return inputPath;

            var meta = await GetVideoMetadataAsync(inputPath);
            double duration = Math.Max(meta.Duration.TotalSeconds, 1);

            double targetBits = 8.0 * 8 * 1024 * 1024;
            double videoBitrate = Math.Max((targetBits / duration) - 128000, 100000);

            string output = inputPath.Replace(".mp4", "_compressed.mp4");
            string args = $"-i \"{inputPath}\" -c:v libx264 -b:v {videoBitrate:F0} -maxrate {videoBitrate:F0} -bufsize {(videoBitrate * 1.5):F0} -vf \"scale='min(1280,iw)':-2\" -preset superfast -c:a aac -b:a 128k -y \"{output}\"";

            var psi = new ProcessStartInfo { FileName = "ffmpeg", Arguments = args, CreateNoWindow = true, UseShellExecute = false };
            using var proc = Process.Start(psi);
            await proc.WaitForExitAsync();

            if (File.Exists(output) && new FileInfo(output).Length < size)
            {
                try { File.Delete(inputPath); } catch { }
                return output;
            }
            return inputPath;
        }

        public async Task<(TimeSpan Duration, int Width, int Height)> GetVideoMetadataAsync(string path)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffprobe",
                Arguments = $"-v error -select_streams v:0 -show_entries stream=width,height,duration -of csv=p=0 \"{path}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            string output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();

            var parts = output.Trim().Split(',');
            if (parts.Length >= 3
                && int.TryParse(parts[0], out int width)
                && int.TryParse(parts[1], out int height)
                && double.TryParse(parts[2], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double seconds))
            {
                return (TimeSpan.FromSeconds(seconds), width, height);
            }
            return (TimeSpan.FromSeconds(30), 1280, 720);
        }

        public async Task<string> ApplyWatermarkAsync(string inputPath)
        {
            string watermarkPath = _config.WatermarkImagePath;
            if (string.IsNullOrWhiteSpace(watermarkPath) || !File.Exists(watermarkPath))
                return inputPath;

            if (Path.GetFileName(inputPath).Contains("_wm"))
                return inputPath;

            string outputPath = inputPath.Replace(Path.GetExtension(inputPath), "_wm" + Path.GetExtension(inputPath));
            bool isVideo = inputPath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);

            string filter = "[1][0]scale2ref=w=oh*mdar:h=ih*0.15[wm][base];[base][wm]overlay=W-w-15:H-h-15";
            string codec = isVideo ? "-c:v libx264 -preset ultrafast -c:a copy" : "";

            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-i \"{inputPath}\" -i \"{watermarkPath}\" -filter_complex \"{filter}\" {codec} -y \"{outputPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };

            try
            {
                using var proc = Process.Start(psi);
                await proc.WaitForExitAsync();
                if (File.Exists(outputPath))
                {
                    try { File.Delete(inputPath); } catch { }
                    return outputPath;
                }
                return inputPath;
            }
            catch { return inputPath; }
        }

        private async Task<string> ProcessSingleFrameAsync(string base64Input, string prompt, float denoise)
        {
            int steps = denoise < 0.3f ? 30 : 15;

            var payload = new
            {
                init_images = new[] { base64Input },
                prompt,
                negative_prompt = "nsfw, text, watermark, blurry, low quality, bad anatomy, deformed, ugly",
                denoising_strength = denoise,
                steps,
                width = 512,
                height = 512,
                cfg_scale = 6,
                sampler_name = "Euler a",
                batch_size = 1,
                resize_mode = 1
            };

            try
            {
                var response = await _http.PostAsync("/sdapi/v1/img2img",
                    new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));

                if (!response.IsSuccessStatusCode) return null;

                dynamic json = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());
                if (json.images == null) return null;

                string tempFile = Path.Combine(Path.GetTempPath(), $"frame_{Guid.NewGuid()}.jpg");
                await File.WriteAllBytesAsync(tempFile, Convert.FromBase64String((string)json.images[0]));
                return tempFile;
            }
            catch { return null; }
        }

        private async Task<string> ApplyUpscaleAsync(string base64Image)
        {
            var payload = new
            {
                resize_mode = 1,
                show_extras_results = false,
                upscaling_resize_w = 1920,
                upscaling_resize_h = 1080,
                upscaling_crop = true,
                upscaler_1 = "RealESRGAN 4x+",
                image = base64Image
            };

            try
            {
                var response = await _http.PostAsync("/sdapi/v1/extra-single-image",
                    new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"));

                if (!response.IsSuccessStatusCode) return null;

                dynamic json = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());
                return json.image;
            }
            catch { return null; }
        }

        private async Task<string> SaveImageAsync(string base64, string prefix)
        {
            string path = Path.Combine(_outputFolder, $"{prefix}_{Guid.NewGuid()}.png");
            await File.WriteAllBytesAsync(path, Convert.FromBase64String(base64));
            return path;
        }
    }
}
