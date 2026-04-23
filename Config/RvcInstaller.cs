using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace VexAI.Installers
{
    public class RvcInstaller
    {
        private readonly string _installDirectory;
        private readonly string _rvcFolder;
        private readonly string _projectRoot;

        public RvcInstaller(string targetDirectory)
        {
            _installDirectory = targetDirectory;
            _rvcFolder = Path.Combine(_installDirectory, "RVC-BETA");
            _projectRoot = AppDomain.CurrentDomain.BaseDirectory;
        }

        public async Task InstallAsync()
        {
            Console.WriteLine("\n=== Iniciando instalação do RVC-BETA (Voice Conversion) ===");

            CloneRepository();
            InstallDefaultModel();
            CreateScripts();

            Console.WriteLine("Instalação do RVC-BETA concluída!");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("IMPORTANTE: Execute '1_INSTALAR_RVC.bat' na pasta do RVC para configurar as dependências.");
            Console.ResetColor();
        }

        private void CloneRepository()
        {
            Console.WriteLine("Clonando repositório do RVC-BETA...");
            if (Directory.Exists(_rvcFolder))
            {
                Console.WriteLine("A pasta já existe. Pulando clonagem.");
                return;
            }

            var processInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "clone https://github.com/RVC-Project/Retrieval-based-Voice-Conversion-WebUI.git RVC-BETA",
                WorkingDirectory = _installDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            process.WaitForExit();

            if (process.ExitCode != 0)
                throw new Exception("Falha ao clonar o RVC-BETA.");
        }

        private void InstallDefaultModel()
        {
            Console.WriteLine("Configurando modelo padrão (Lula)...");
            
            string sourceFile = Path.Combine(_projectRoot, "lula.pth");
            
            string targetDir = Path.Combine(_rvcFolder, "assets", "weights");
            string targetFile = Path.Combine(targetDir, "lula.pth");

            if (!File.Exists(sourceFile))
            {
                Console.WriteLine("[!] Aviso: arquivo 'lula.pth' não encontrado na pasta do projeto. Pulei a cópia.");
                return;
            }

            try
            {
                Directory.CreateDirectory(targetDir);
                File.Copy(sourceFile, targetFile, true);
                Console.WriteLine("[OK] Modelo 'lula.pth' instalado em assets/weights.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] Erro ao copiar modelo: {ex.Message}");
            }
        }

        private void CreateScripts()
        {
            Console.WriteLine("Criando scripts de inicialização...");

            string installBat = Path.Combine(_rvcFolder, "1_INSTALAR_RVC.bat");
            string installContent = @"@echo off
echo Criando ambiente virtual...
python -m venv venv
call venv\Scripts\activate
echo Instalando dependencias (isso pode demorar)...
pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cpu
pip install openvino onnxruntime-openvino
pip install -r requirements.txt
echo Instalando audio-separator...
pip install audio-separator
echo Instalacao concluida!
pause";
            File.WriteAllText(installBat, installContent);

            string runBat = Path.Combine(_rvcFolder, "2_INICIAR_RVC.bat");
            string runContent = @"@echo off
call venv\Scripts\activate
python infer-web.py
pause";
            File.WriteAllText(runBat, runContent);
        }
    }
}