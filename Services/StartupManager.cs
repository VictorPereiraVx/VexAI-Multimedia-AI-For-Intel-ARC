using System.Diagnostics;
using System.Net.NetworkInformation;
using VexAI.Config;

namespace VexAI.Services
{
    public static class StartupManager
    {
        public static void StartDependencies(AppConfig config)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("[Sistema] Verificando dependências...");
            Console.ResetColor();

            if (config.AutoStartSdNext && !string.IsNullOrWhiteSpace(config.SdNextBatPath))
                StartSdNext(config.SdNextBatPath);
        }

        private static void StartSdNext(string batPath)
        {
            if (IsPortInUse(7860))
            {
                Console.WriteLine("[SD.Next] Servidor já está rodando na porta 7860.");
                return;
            }

            if (!File.Exists(batPath))
            {
                Console.WriteLine($"[SD.Next] Arquivo não encontrado: {batPath}");
                return;
            }

            string workDir = Path.GetDirectoryName(batPath);
            Console.WriteLine("[SD.Next] Iniciando servidor...");

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = batPath,
                    WorkingDirectory = workDir,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Minimized
                });
                Console.WriteLine("[SD.Next] Inicialização disparada.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SD.Next] Erro ao iniciar: {ex.Message}");
            }
        }

        private static bool IsPortInUse(int port)
        {
            var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            return listeners.Any(e => e.Port == port);
        }
    }
}
