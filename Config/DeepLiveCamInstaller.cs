using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace VexAI.Installers
{
    public class DeepLiveCamInstaller
    {
        private readonly string _installDirectory;
        private readonly string _deepLiveFolder;

        // URLs dos modelos obrigatórios (HuggingFace e GitHub Releases)
        private const string InswapperUrl = "https://huggingface.co/ezioruan/inswapper_128.onnx/resolve/main/inswapper_128.onnx";
        private const string GfpganUrl = "https://github.com/TencentARC/GFPGAN/releases/download/v1.3.0/GFPGANv1.4.pth";

        public DeepLiveCamInstaller(string targetDirectory)
        {
            _installDirectory = targetDirectory;
            _deepLiveFolder = Path.Combine(_installDirectory, "Deep-Live-Cam");
        }

        public async Task InstallAsync()
        {
            Console.WriteLine("\n=== Iniciando instalação do DeepLiveCam para Intel ARC ===");

            CloneRepository();
            await DownloadModelsAsync();
            CreateLaunchScripts();

            Console.WriteLine("Instalação do DeepLiveCam concluída!");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("IMPORTANTE: Para o primeiro uso, execute o arquivo '1_INSTALAR_DEPENDENCIAS.bat'");
            Console.WriteLine($"localizado em: {_deepLiveFolder}");
            Console.ResetColor();
        }

        private void CloneRepository()
        {
            Console.WriteLine("Clonando repositório do DeepLiveCam...");
            if (Directory.Exists(_deepLiveFolder))
            {
                Console.WriteLine("A pasta já existe. Pulando clonagem.");
                return;
            }

            Directory.CreateDirectory(_installDirectory);

            var processInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "clone https://github.com/hacksider/Deep-Live-Cam.git",
                WorkingDirectory = _installDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            process.WaitForExit();

            if (process.ExitCode != 0)
                throw new Exception("Falha ao clonar o DeepLiveCam. Verifique a sua conexão ou a instalação do Git.");
        }

        private async Task DownloadModelsAsync()
        {
            string modelsDir = Path.Combine(_deepLiveFolder, "models");
            Directory.CreateDirectory(modelsDir);

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(30); // Modelos podem ser pesados

            // 1. Baixar Inswapper (Modelo principal de troca de rosto)
            string inswapperPath = Path.Combine(modelsDir, "inswapper_128.onnx");
            if (!File.Exists(inswapperPath))
            {
                Console.WriteLine("Baixando modelo principal (inswapper_128.onnx)...");
                var response = await httpClient.GetAsync(InswapperUrl);
                response.EnsureSuccessStatusCode();
                using var fs = new FileStream(inswapperPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs);
            }
            else
            {
                Console.WriteLine("Modelo inswapper_128.onnx já existe.");
            }

            // 2. Baixar GFPGAN (Melhorador de rosto)
            string gfpganPath = Path.Combine(modelsDir, "GFPGANv1.4.pth");
            if (!File.Exists(gfpganPath))
            {
                Console.WriteLine("Baixando modelo de aprimoramento (GFPGAN)...");
                var response = await httpClient.GetAsync(GfpganUrl);
                response.EnsureSuccessStatusCode();
                using var fs = new FileStream(gfpganPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs);
            }
            else
            {
                Console.WriteLine("Modelo GFPGANv1.4.pth já existe.");
            }

            Console.WriteLine("Verificação de modelos concluída!");
        }

        private void CreateLaunchScripts()
        {
            Console.WriteLine("Criando scripts de inicialização para Intel ARC...");

            // Script 1: Instalador de dependências isolado (venv)
            string installBatPath = Path.Combine(_deepLiveFolder, "1_INSTALAR_DEPENDENCIAS.bat");
            string installContent = @"@echo off
echo Criando ambiente virtual Python...
python -m venv venv
call venv\Scripts\activate
echo Instalando requisitos padroes...
pip install -r requirements.txt
echo Instalando suporte OpenVINO para Intel ARC...
pip install onnxruntime-openvino
echo Instalacao concluida com sucesso!
pause";
            File.WriteAllText(installBatPath, installContent);

            // Script 2: Inicializador com OpenVINO (baseado no run.py do repositório)
            string runBatPath = Path.Combine(_deepLiveFolder, "2_INICIAR_INTEL_ARC.bat");
            string runContent = @"@echo off
call venv\Scripts\activate
python run.py --execution-provider openvino
pause";
            File.WriteAllText(runBatPath, runContent);
        }
    }
}