using System.Diagnostics;
using System.Net.NetworkInformation;
using VexAI.Config;

namespace VexAI.Services
{
    public static class StartupManager
    {
        public static async Task StartDependenciesAsync(AppConfig config)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("[Sistema] Verificando dependências...");
            Console.ResetColor();

            if (config.AutoStartSdNext && !string.IsNullOrWhiteSpace(config.SdNextBatPath))
                await StartSdNextAsync(config.SdNextBatPath, config.SdNextUrl);
        }

        private static async Task StartSdNextAsync(string batPath, string sdNextUrl)
        {
            if (IsPortInUse(7860))
            {
                Console.WriteLine("[SD.Next] Servidor já está rodando na porta 7860.");
                return;
            }

            batPath = ResolveBatPath(batPath);

            if (!File.Exists(batPath))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[SD.Next] Arquivo .bat não encontrado: {batPath}");
                Console.WriteLine("[SD.Next] Use o comando 'config' para corrigir o caminho.");
                Console.ResetColor();
                return;
            }

            string workDir = Path.GetDirectoryName(batPath)!;
            Console.WriteLine("[SD.Next] Iniciando servidor em segundo plano...");

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = batPath,
                    WorkingDirectory = workDir,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Minimized
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SD.Next] Erro ao iniciar: {ex.Message}");
                return;
            }

            await WaitForSdNextAsync(sdNextUrl);
        }

        public static string ResolveBatPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;

            if (File.Exists(path) && path.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
                return path;

            string dir = Directory.Exists(path) ? path : (Path.GetDirectoryName(path) ?? path);

            string[] candidates = { "iniciar_intel.bat", "webui.bat", "launch.bat" };
            foreach (var candidate in candidates)
            {
                string full = Path.Combine(dir, candidate);
                if (File.Exists(full))
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"[SD.Next] Caminho corrigido automaticamente para: {full}");
                    Console.ResetColor();
                    return full;
                }
            }

            return path;
        }

        private static async Task WaitForSdNextAsync(string baseUrl)
        {
            string healthUrl = baseUrl.TrimEnd('/') + "/sdapi/v1/progress";
            Console.Write("[SD.Next] Aguardando servidor ficar pronto (pode demorar no primeiro uso)");

            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            int dots = 0;
            var started = DateTime.UtcNow;

            while (true)
            {
                try
                {
                    var response = await http.GetAsync(healthUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[SD.Next] Servidor pronto! (levou {(int)(DateTime.UtcNow - started).TotalSeconds}s)");
                        Console.ResetColor();
                        return;
                    }
                }
                catch { }

                Console.Write(".");
                if (++dots % 30 == 0)
                    Console.Write($" ({(int)(DateTime.UtcNow - started).TotalSeconds}s)");

                await Task.Delay(2000);
            }
        }

        private static bool IsPortInUse(int port)
        {
            var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            return listeners.Any(e => e.Port == port);
        }
    }
}
