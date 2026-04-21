using System.Diagnostics;
using VexAI.Config;

namespace VexAI.Services
{
    public class VoiceService
    {
        private readonly AppConfig _config;
        private readonly string _outputFolder;

        public VoiceService(AppConfig config)
        {
            _config = config;
            _outputFolder = string.IsNullOrWhiteSpace(config.OutputFolder)
                ? Path.Combine(AppContext.BaseDirectory, "output", "audio")
                : Path.Combine(config.OutputFolder, "audio");

            Directory.CreateDirectory(_outputFolder);
        }

        public async Task<string> TrocarVozVideoAsync(string videoPath, string modelName, int pitch, Action<string> onProgress = null, bool applyWatermark = true)
        {
            string audioOriginal = Path.Combine(_outputFolder, $"full_{Guid.NewGuid()}.wav");
            string vozIsolada = "";
            string instrumental = "";
            string vozConvertida = "";
            string audioMixado = Path.Combine(_outputFolder, $"mix_{Guid.NewGuid()}.wav");
            string videoFinal = Path.Combine(_outputFolder, $"dubbed_{Guid.NewGuid()}.mp4");

            try
            {
                onProgress?.Invoke("Extraindo áudio original...");
                await ExtractAudioAsync(videoPath, audioOriginal);

                onProgress?.Invoke("Separando voz e instrumental...");
                var (voz, inst) = await SeparateVoiceAsync(audioOriginal);

                if (voz == null || inst == null)
                {
                    Console.WriteLine("[VoiceService] Falha na separação de áudio.");
                    return null;
                }

                vozIsolada = voz;
                instrumental = inst;

                onProgress?.Invoke($"Convertendo voz ({modelName})...");
                vozConvertida = Path.Combine(_outputFolder, $"rvc_{Guid.NewGuid()}.wav");

                string modelFile = Path.Combine(_config.RvcFolderPath, "assets", "weights", $"{modelName}.pth");
                if (!File.Exists(modelFile))
                {
                    Console.WriteLine($"[VoiceService] Modelo não encontrado: {modelFile}");
                    return null;
                }

                bool ok = await RunRvcInferenceAsync(vozIsolada, vozConvertida, modelName, pitch);
                if (!ok) return null;

                onProgress?.Invoke("Mixando áudio final...");
                await MixAudioAsync(vozConvertida, instrumental, audioMixado);

                onProgress?.Invoke("Renderizando vídeo final...");
                await MergeAudioVideoAsync(videoPath, audioMixado, videoFinal);

                if (applyWatermark)
                {
                    var imgService = new ImageService(_config);
                    return await imgService.ApplyWatermarkAsync(videoFinal);
                }
                return videoFinal;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VoiceService] Erro: {ex.Message}");
                return null;
            }
            finally
            {
                try
                {
                    if (File.Exists(audioOriginal)) File.Delete(audioOriginal);
                    if (File.Exists(vozIsolada)) File.Delete(vozIsolada);
                    if (File.Exists(instrumental)) File.Delete(instrumental);
                    if (File.Exists(vozConvertida)) File.Delete(vozConvertida);
                    if (File.Exists(audioMixado)) File.Delete(audioMixado);
                }
                catch { }
            }
        }

        public List<string> GetAvailableModels()
        {
            string modelsPath = Path.Combine(_config.RvcFolderPath, "assets", "weights");
            if (!Directory.Exists(modelsPath)) return new List<string>();
            return Directory.GetFiles(modelsPath, "*.pth")
                .Select(Path.GetFileNameWithoutExtension)
                .ToList();
        }

        private async Task<(string Voz, string Instrumental)> SeparateVoiceAsync(string inputAudio)
        {
            string separatorExe = Path.Combine(_config.RvcFolderPath, "venv", "Scripts", "audio-separator.exe");
            string model = "UVR-MDX-NET-Inst_HQ_3.onnx";
            string args = $"\"{inputAudio}\" --model_filename {model} --output_dir \"{_outputFolder}\" --output_format wav --log_level info";

            var psi = new ProcessStartInfo
            {
                FileName = separatorExe,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.EnvironmentVariables["PYTHONPATH"] = _config.RvcFolderPath;

            using var proc = Process.Start(psi);
            var tOut = Task.Run(async () => { while (!proc.StandardOutput.EndOfStream) await proc.StandardOutput.ReadLineAsync(); });
            var tErr = Task.Run(async () => { while (!proc.StandardError.EndOfStream) await proc.StandardError.ReadLineAsync(); });
            await Task.WhenAll(tOut, tErr, proc.WaitForExitAsync());

            string nameBase = Path.GetFileNameWithoutExtension(inputAudio);
            var files = Directory.GetFiles(_outputFolder, $"*{nameBase}*.wav");

            string fileVoz = files.Where(f => f.Contains("Vocals", StringComparison.OrdinalIgnoreCase))
                                  .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                                  .FirstOrDefault();

            string fileInst = files.Where(f => f.Contains("Instrumental", StringComparison.OrdinalIgnoreCase))
                                   .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                                   .FirstOrDefault();

            return (fileVoz, fileInst);
        }

        private async Task MixAudioAsync(string voice, string instrumental, string output)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-i \"{voice}\" -i \"{instrumental}\" -filter_complex \"amix=inputs=2:duration=longest:dropout_transition=2\" \"{output}\" -y",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var proc = Process.Start(psi);
            await proc.WaitForExitAsync();
        }

        private async Task ExtractAudioAsync(string video, string output)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-i \"{video}\" -vn -acodec pcm_s16le -ar 44100 -ac 2 \"{output}\" -y",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var proc = Process.Start(psi);
            await proc.WaitForExitAsync();
        }

        private async Task MergeAudioVideoAsync(string video, string audio, string output)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-i \"{video}\" -i \"{audio}\" -c:v copy -map 0:v -map 1:a -shortest \"{output}\" -y",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var proc = Process.Start(psi);
            await proc.WaitForExitAsync();
        }

        private async Task<bool> RunRvcInferenceAsync(string input, string output, string model, int pitch)
        {
            string python = Path.Combine(_config.RvcFolderPath, "venv", "Scripts", "python.exe");
            string script = Path.Combine(_config.RvcFolderPath, "tools", "infer_cli.py");

            string argsGpu = $"\"{script}\" --dml --f0up_key {pitch} --input_path \"{input}\" --opt_path \"{output}\" --model_name \"{model}.pth\" --is_half False --f0method rmvpe";
            await RunPythonAsync(python, argsGpu);

            if (File.Exists(output)) return true;

            string argsCpu = $"\"{script}\" --f0up_key {pitch} --input_path \"{input}\" --opt_path \"{output}\" --model_name \"{model}.pth\" --is_half False --f0method rmvpe";
            await RunPythonAsync(python, argsCpu);

            return File.Exists(output);
        }

        private async Task RunPythonAsync(string pythonExe, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = args,
                WorkingDirectory = _config.RvcFolderPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.EnvironmentVariables["PYTHONPATH"] = _config.RvcFolderPath;

            using var proc = Process.Start(psi);
            var tOut = Task.Run(async () => { while (!proc.StandardOutput.EndOfStream) await proc.StandardOutput.ReadLineAsync(); });
            var tErr = Task.Run(async () =>
            {
                while (!proc.StandardError.EndOfStream)
                {
                    string line = await proc.StandardError.ReadLineAsync();
                    if (line != null && (line.Contains("Error") || line.Contains("Fail")))
                        Console.WriteLine($"[RVC]: {line}");
                }
            });
            await Task.WhenAll(tOut, tErr, proc.WaitForExitAsync());
        }
    }
}
