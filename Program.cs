using System.Diagnostics;
using VexAI.Config;
using VexAI.Core;
using VexAI.Installers;
using VexAI.Services;

namespace VexAI
{
    class Program
    {
        static AppConfig _config;
        static ImageService _imageService;
        static VoiceService _voiceService;
        static ProcessingQueue _queue;

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            PrintBanner();

            _config = AppConfig.Load();

            if (!_config.IsConfigured())
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[Config] Primeira execução detectada. Vamos configurar o VexAI.\n");
                Console.ResetColor();
                await RunSetupWizard();
            }

            StartupManager.StartDependencies(_config);

            _imageService = new ImageService(_config);
            _voiceService = new VoiceService(_config);
            _queue = new ProcessingQueue(_imageService, _voiceService);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n[VexAI] Pronto! Digite 'ajuda' para ver os comandos disponíveis.\n");
            Console.ResetColor();

            await RunInteractiveLoop();
        }
        static bool VerificarRequisitosSistema()
        {
            Console.WriteLine("\nVerificando pré-requisitos do sistema...");

            bool gitInstalado = VerificarGit();
            bool pythonInstalado = VerificarPython();

            if (!gitInstalado)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[X] Erro: Git não foi encontrado.");
                Console.WriteLine("    Por favor, instale o Git (https://git-scm.com/downloads).");
                Console.ResetColor();
            }

            if (!pythonInstalado)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[X] Erro: Python 3.10.x não foi encontrado ou não está no PATH.");
                Console.WriteLine("    Por favor, instale o Python 3.10.6 e MARQUE A OPÇÃO 'Add Python to PATH' na instalação.");
                Console.ResetColor();
            }

            return gitInstalado && pythonInstalado;
        }

        static bool VerificarGit()
        {
            try
            {
                string output = ExecutarComandoTerminal("git", "--version");
                if (output.ToLower().Contains("git version"))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[OK] Git detectado: {output.Trim()}");
                    Console.ResetColor();
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        static bool VerificarPython()
        {
            try
            {
                string output = ExecutarComandoTerminal("python", "--version");

                // Verifica se a saída contém "Python 3.10" (Aceita 3.10.6, 3.10.11, etc)
                if (output.Contains("Python 3.10"))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[OK] Python detectado: {output.Trim()}");
                    Console.ResetColor();
                    return true;
                }
                else if (!string.IsNullOrWhiteSpace(output))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[!] Versão incorreta do Python detectada: {output.Trim()}");
                    Console.WriteLine("    O VexAI necessita estritamente da versão 3.10.x para compatibilidade.");
                    Console.ResetColor();
                    return false;
                }
                return false;
            }
            catch
            {
                // Tenta usar "python3" caso o usuário esteja em um ambiente diferente
                try
                {
                    string output2 = ExecutarComandoTerminal("python3", "--version");
                    if (output2.Contains("Python 3.10"))
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[OK] Python3 detectado: {output2.Trim()}");
                        Console.ResetColor();
                        return true;
                    }
                    return false;
                }
                catch
                {
                    return false;
                }
            }
        }

        static string ExecutarComandoTerminal(string fileName, string arguments)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            return string.IsNullOrWhiteSpace(output) ? error : output;
        }

        static async Task Installer(string pastaInstalacao)
        {
            Console.WriteLine("=== VexAI Multimedia AI Installer ===");

            var sdInstaller = new downloadDependencies(pastaInstalacao);
            var deepLiveInstaller = new DeepLiveCamInstaller(pastaInstalacao);
            var ffmpegInstaller = new FfmpegInstaller(pastaInstalacao);
            var rvcInstaller = new RvcInstaller(pastaInstalacao);

            try
            {
                if (!VerificarRequisitosSistema())
                {
                    Console.WriteLine("Erro: Python 3.10.6 ou Git não encontrados.");
                    return;
                }

                await sdInstaller.InstallAsync();
                await deepLiveInstaller.InstallAsync();
                await ffmpegInstaller.InstallAsync();
                await rvcInstaller.InstallAsync();

                _config.SdNextBatPath = Path.Combine(pastaInstalacao, "automatic", "iniciar_intel.bat");
                string deepLiveFolder = Path.Combine(pastaInstalacao, "Deep-Live-Cam");
                _config.DeepLiveFastPath = deepLiveFolder;
                _config.DeepLiveEnhancedPath = deepLiveFolder;
                _config.RvcFolderPath = Path.Combine(pastaInstalacao, "RVC-BETA");

                Console.WriteLine("\nInstalação concluída com sucesso!");
                Console.WriteLine("Pressione qualquer tecla para sair...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro crítico durante a instalação: {ex.Message}");
            }
        }

        static async Task RunSetupWizard()
        {
            Console.WriteLine("=== CONFIGURAÇÃO INICIAL ===\n");

            string option = AskString("Deseja baixar as dependências? (Y/N)", "Y").Trim().ToUpperInvariant();
            bool downloadDependencies = option == "Y" || option == "SIM";
            bool skipManualSdNextPath = false;
            bool skipManualDeepLivePath = false;
            bool skipManualRvcPath = false;

            if (downloadDependencies)
            {
                string installFolder = AskPath(
                    "Pasta para instalar o SD.Next e o DeepLiveCam",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VexAI_SDNext"),
                    isDirectory: true
                );

                await Installer(installFolder);

                _config.SdNextBatPath = Path.Combine(installFolder, "automatic", "iniciar_intel.bat");
                skipManualSdNextPath = true;
                skipManualDeepLivePath = true;
                skipManualRvcPath = true;
            }
            else if (option == "N" || option == "NAO" || option == "NO")
            {
                _config.SdNextBatPath = AskPath(
                    "Caminho do .bat de inicialização do SD.Next [Enter para pular]",
                    "",
                    isDirectory: false,
                    optional: true
                );
            }
            else
            {
                Console.WriteLine("Opção inválida");
                return;
            }

            _config.OutputFolder = AskPath(
                "Pasta de saída para arquivos gerados",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VexAI_Output"),
                isDirectory: true
            );

            if (!skipManualRvcPath)
            {
                _config.RvcFolderPath = AskPath(
                    "Pasta raiz do RVC (contém venv/ e assets/weights/)",
                    "",
                    isDirectory: true
                );
            }

            if (!skipManualDeepLivePath)
            {
                _config.DeepLiveFastPath = AskPath(
                    "Pasta do Deep-Live-Cam (modo Rápido/DirectML)",
                    "",
                    isDirectory: true
                );

                _config.DeepLiveEnhancedPath = AskPath(
                    "Pasta do Deep-Live-Cam (modo Melhorado/OpenVINO) [Enter para pular]",
                    _config.DeepLiveFastPath,
                    isDirectory: true,
                    optional: true
                );
            }

            if (!skipManualSdNextPath)
            {
                _config.SdNextBatPath = AskPath(
                    "Caminho do .bat de inicialização do SD.Next [Enter para pular]",
                    _config.SdNextBatPath,
                    isDirectory: false,
                    optional: true
                );
            }

            _config.SdNextUrl = AskString(
                "URL da API do SD.Next",
                "http://127.0.0.1:7860"
            );

            _config.WatermarkImagePath = AskPath(
                "Caminho da imagem de marca d'água (.png) [Enter para pular]",
                "",
                isDirectory: false,
                optional: true
            );

            _config.AutoStartSdNext = !string.IsNullOrWhiteSpace(_config.SdNextBatPath)
                && AskYesNo("Iniciar SD.Next automaticamente ao abrir o programa?", true);

            Directory.CreateDirectory(_config.OutputFolder);

            _config.Save();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n[Config] Configuração salva em config.json.\n");
            Console.ResetColor();
        }

        static async Task RunInteractiveLoop()
        {
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("VexAI> ");
                Console.ResetColor();

                string input = Console.ReadLine()?.Trim();

                if (string.IsNullOrWhiteSpace(input)) continue;

                string[] parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                string command = parts[0].ToLower();

                switch (command)
                {
                    case "ajuda":
                    case "help":
                        PrintHelp();
                        break;

                    case "config":
                        await RunSetupWizard();
                        _imageService = new ImageService(_config);
                        _voiceService = new VoiceService(_config);
                        _queue = new ProcessingQueue(_imageService, _voiceService);
                        break;

                    case "modelos":
                        ListVoiceModels();
                        break;

                    case "gerar":
                        await HandleGenerateImage();
                        break;

                    case "reimaginar":
                        await HandleReimagine();
                        break;

                    case "clonar":
                        await HandleFaceClone();
                        break;

                    case "faceswap":
                        await HandleFaceSwap();
                        break;

                    case "videoart":
                        await HandleVideoArt();
                        break;

                    case "voz":
                        await HandleVoiceSwap();
                        break;

                    case "fila":
                        Console.WriteLine($"[Fila] Jobs pendentes: {_queue.QueueLength} | Processando: {_queue.IsRunning}");
                        break;

                    case "sair":
                    case "exit":
                        Console.WriteLine("Saindo...");
                        return;

                    default:
                        Console.WriteLine($"Comando desconhecido: '{command}'. Digite 'ajuda' para ver os comandos.");
                        break;
                }
            }
        }

        static async Task HandleGenerateImage()
        {
            string prompt = AskString("Prompt (descreva a imagem)", "");
            if (string.IsNullOrWhiteSpace(prompt)) return;

            string qualidade = AskChoice("Qualidade", new[] { "sd (512x512)", "hd (720p)", "fullhd (1080p)" }, 0);
            bool upscale = qualidade == "fullhd (1080p)";
            bool nsfw = AskYesNo("Conteúdo +18?", false);

            int w = qualidade.StartsWith("sd") ? 512 : qualidade.StartsWith("hd") ? 1280 : 1920;
            int h = qualidade.StartsWith("sd") ? 512 : qualidade.StartsWith("hd") ? 720 : 1080;

            Console.WriteLine("[Gerar] Adicionando à fila...");

            var tcs = new TaskCompletionSource<string>();
            _queue.Enqueue(new ImageJob
            {
                Type = "txt2img",
                Prompt = prompt,
                Width = w,
                Height = h,
                Upscale = upscale,
                Nsfw = nsfw,
                OnProgress = msg => Console.WriteLine($"  {msg}"),
                OnComplete = path => { Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine($"\n[✓] Salvo em: {path}"); Console.ResetColor(); tcs.SetResult(path); },
                OnError = err => { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine($"\n[✗] Erro: {err}"); Console.ResetColor(); tcs.SetResult(null); }
            });
            await tcs.Task;
        }

        static async Task HandleReimagine()
        {
            string imagePath = AskPath("Caminho da imagem de entrada", "", isDirectory: false);
            if (!File.Exists(imagePath)) { Console.WriteLine("Arquivo não encontrado."); return; }

            string prompt = AskString("Prompt (como reimaginar?)", "");
            float denoise = AskFloat("Força da mudança (0.1 a 1.0)", 0.6f);
            bool nsfw = AskYesNo("Conteúdo +18?", false);

            byte[] bytes = await File.ReadAllBytesAsync(imagePath);
            string b64 = Convert.ToBase64String(bytes);

            var tcs = new TaskCompletionSource<string>();
            _queue.Enqueue(new ImageJob
            {
                Type = "img2img",
                Prompt = prompt,
                InputImageBase64 = b64,
                DenoisingStrength = denoise,
                Width = 512,
                Height = 512,
                Nsfw = nsfw,
                OnProgress = msg => Console.WriteLine($"  {msg}"),
                OnComplete = path => { Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine($"\n[✓] Salvo em: {path}"); Console.ResetColor(); tcs.SetResult(path); },
                OnError = err => { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine($"\n[✗] Erro: {err}"); Console.ResetColor(); tcs.SetResult(null); }
            });
            await tcs.Task;
        }

        static async Task HandleFaceClone()
        {
            string prompt = AskString("Prompt (cena/estilo)", "");
            string facePath = AskPath("Caminho da foto do rosto", "", isDirectory: false);
            if (!File.Exists(facePath)) { Console.WriteLine("Arquivo não encontrado."); return; }

            byte[] bytes = await File.ReadAllBytesAsync(facePath);
            string b64 = Convert.ToBase64String(bytes);

            var tcs = new TaskCompletionSource<string>();
            _queue.Enqueue(new ImageJob
            {
                Type = "face-clone",
                Prompt = prompt,
                FaceImageBase64 = b64,
                OnComplete = path => { Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine($"\n[✓] Salvo em: {path}"); Console.ResetColor(); tcs.SetResult(path); },
                OnError = err => { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine($"\n[✗] Erro: {err}"); Console.ResetColor(); tcs.SetResult(null); }
            });
            await tcs.Task;
        }

        static async Task HandleFaceSwap()
        {
            string videoPath = AskPath("Caminho do vídeo alvo", "", isDirectory: false);
            if (!File.Exists(videoPath)) { Console.WriteLine("Arquivo não encontrado."); return; }

            string facePath = AskPath("Caminho da foto do rosto", "", isDirectory: false);
            if (!File.Exists(facePath)) { Console.WriteLine("Arquivo não encontrado."); return; }

            string modo = AskChoice("Modo", new[] { "rápido (DirectML)", "melhorado (OpenVINO)" }, 0);

            byte[] videoBytes = await File.ReadAllBytesAsync(videoPath);
            byte[] faceBytes = await File.ReadAllBytesAsync(facePath);
            string b64Face = Convert.ToBase64String(faceBytes);

            string type = modo.StartsWith("rápido") ? "faceswap-fast" : "faceswap-enhanced";

            var tcs = new TaskCompletionSource<string>();
            _queue.Enqueue(new ImageJob
            {
                Type = type,
                InputVideoBytes = videoBytes,
                FaceImageBase64 = b64Face,
                OnProgress = msg => Console.WriteLine($"  {msg}"),
                OnComplete = path => { Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine($"\n[✓] Salvo em: {path}"); Console.ResetColor(); tcs.SetResult(path); },
                OnError = err => { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine($"\n[✗] Erro: {err}"); Console.ResetColor(); tcs.SetResult(null); }
            });
            await tcs.Task;
        }

        static async Task HandleVideoArt()
        {
            string videoPath = AskPath("Caminho do vídeo (máx 10s recomendado)", "", isDirectory: false);
            if (!File.Exists(videoPath)) { Console.WriteLine("Arquivo não encontrado."); return; }

            string prompt = AskString("Prompt/estilo (ex: cyberpunk, vhs, anime...)", "");
            float denoise = AskFloat("Força da mudança (0.1 = sutil, 0.75 = caótico)", 0.4f);

            var tcs = new TaskCompletionSource<string>();
            _queue.Enqueue(new ImageJob
            {
                Type = "videoart",
                InputVideoPath = videoPath,
                Prompt = prompt,
                DenoisingStrength = denoise,
                OnProgress = msg => Console.WriteLine($"  {msg}"),
                OnComplete = path => { Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine($"\n[✓] Salvo em: {path}"); Console.ResetColor(); tcs.SetResult(path); },
                OnError = err => { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine($"\n[✗] Erro: {err}"); Console.ResetColor(); tcs.SetResult(null); }
            });
            await tcs.Task;
        }

        static async Task HandleVoiceSwap()
        {
            string videoPath = AskPath("Caminho do vídeo (com fala clara)", "", isDirectory: false);
            if (!File.Exists(videoPath)) { Console.WriteLine("Arquivo não encontrado."); return; }

            var models = _voiceService.GetAvailableModels();
            if (models.Count == 0)
            {
                Console.WriteLine("[Erro] Nenhum modelo RVC encontrado. Verifique a pasta assets/weights/ do RVC.");
                return;
            }

            Console.WriteLine("Modelos disponíveis:");
            for (int i = 0; i < models.Count; i++)
                Console.WriteLine($"  [{i}] {models[i]}");

            int idx = AskInt("Número do modelo", 0);
            if (idx < 0 || idx >= models.Count) { Console.WriteLine("Índice inválido."); return; }

            int pitch = AskInt("Tom (+12 fem / -12 masc / 0 neutro)", 0);

            var tcs = new TaskCompletionSource<string>();
            _queue.Enqueue(new ImageJob
            {
                Type = "voice-swap",
                InputVideoPath = videoPath,
                VoiceModel = models[idx],
                VoicePitch = pitch,
                OnProgress = msg => Console.WriteLine($"  {msg}"),
                OnComplete = path => { Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine($"\n[✓] Salvo em: {path}"); Console.ResetColor(); tcs.SetResult(path); },
                OnError = err => { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine($"\n[✗] Erro: {err}"); Console.ResetColor(); tcs.SetResult(null); }
            });
            await tcs.Task;
        }

        static void ListVoiceModels()
        {
            var models = _voiceService.GetAvailableModels();
            if (models.Count == 0)
            {
                Console.WriteLine("Nenhum modelo .pth encontrado em assets/weights/.");
                return;
            }
            Console.WriteLine($"Modelos RVC disponíveis ({models.Count}):");
            foreach (var m in models)
                Console.WriteLine($"  - {m}");
        }

        static void PrintBanner()
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine(@"
 __   __         _    ___ 
 \ \ / /__ __   /_\  |_ _|
  \ V / -_) \ // _ \  | | 
   \_/\___/_\_\/_/ \_\|___|
                            
  AI Media Processing Tool - Intel Arc Edition
");
            Console.ResetColor();
        }

        static void PrintHelp()
        {
            Console.WriteLine(@"
Comandos disponíveis:
  gerar        Gera imagem por texto (txt2img)
  reimaginar   Reimagina uma imagem existente (img2img)
  clonar       Clona rosto em uma cena (IP-Adapter)
  faceswap     Troca rosto em vídeo (DirectML ou OpenVINO)
  videoart     Aplica estilo artístico em vídeo frame a frame
  voz          Troca voz em vídeo usando RVC
  modelos      Lista modelos RVC disponíveis
  fila         Mostra status da fila de processamento
  config       Reconfigurar caminhos e opções
  sair         Fecha o programa
");
        }

        // ============================
        // HELPERS DE INPUT
        // ============================

        static string AskString(string label, string defaultValue)
        {
            string def = string.IsNullOrWhiteSpace(defaultValue) ? "" : $" [{defaultValue}]";
            Console.Write($"  {label}{def}: ");
            string input = Console.ReadLine()?.Trim();
            return string.IsNullOrWhiteSpace(input) ? defaultValue : input;
        }

        static string AskPath(string label, string defaultValue, bool isDirectory, bool optional = false)
        {
            while (true)
            {
                string def = string.IsNullOrWhiteSpace(defaultValue) ? "" : $" [{defaultValue}]";
                string opt = optional ? " (opcional)" : "";
                Console.Write($"  {label}{opt}{def}: ");
                string input = Console.ReadLine()?.Trim();

                if (string.IsNullOrWhiteSpace(input))
                {
                    if (optional || !string.IsNullOrWhiteSpace(defaultValue)) return defaultValue;
                    Console.WriteLine("  [!] Caminho obrigatório.");
                    continue;
                }

                if (!optional && !isDirectory && !File.Exists(input))
                {
                    Console.Write($"  [!] Arquivo não encontrado. Usar mesmo assim? (s/n): ");
                    if (Console.ReadLine()?.Trim().ToLower() != "s") continue;
                }

                return input;
            }
        }

        static bool AskYesNo(string label, bool defaultValue)
        {
            string def = defaultValue ? "S/n" : "s/N";
            Console.Write($"  {label} ({def}): ");
            string input = Console.ReadLine()?.Trim().ToLower();
            if (string.IsNullOrWhiteSpace(input)) return defaultValue;
            return input == "s" || input == "sim" || input == "y" || input == "yes";
        }

        static float AskFloat(string label, float defaultValue)
        {
            Console.Write($"  {label} [{defaultValue}]: ");
            string input = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(input)) return defaultValue;
            return float.TryParse(input, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float val) ? val : defaultValue;
        }

        static int AskInt(string label, int defaultValue)
        {
            Console.Write($"  {label} [{defaultValue}]: ");
            string input = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(input)) return defaultValue;
            return int.TryParse(input, out int val) ? val : defaultValue;
        }

        static string AskChoice(string label, string[] options, int defaultIndex)
        {
            Console.WriteLine($"  {label}:");
            for (int i = 0; i < options.Length; i++)
                Console.WriteLine($"    [{i}] {options[i]}");
            Console.Write($"  Escolha [{defaultIndex}]: ");
            string input = Console.ReadLine()?.Trim();
            if (int.TryParse(input, out int idx) && idx >= 0 && idx < options.Length)
                return options[idx];
            return options[defaultIndex];
        }
    }
}
