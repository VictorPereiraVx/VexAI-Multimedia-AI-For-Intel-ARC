using System.Diagnostics;

namespace VexAI.Config
{
    /// <summary>
    /// Baixa e instala o Python 3.10.11 silenciosamente quando a versão
    /// correta não está presente no sistema.
    /// </summary>
    internal static class PythonInstaller
    {
        private const string PythonInstallerUrl =
            "https://www.python.org/ftp/python/3.10.11/python-3.10.11-amd64.exe";
        private const string PythonVersion    = "3.10.11";
        private const string TempInstallerName = "python-3.10.11-amd64.exe";

        /// <summary>
        /// Garante que o Python 3.10.x está disponível.
        /// Se não estiver, oferece instalação automática ao usuário.
        /// Retorna true quando o Python 3.10.x está pronto para uso.
        /// </summary>
        public static async Task<bool> EnsurePython310Async()
        {
            if (IsPython310Available()) return true;

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║         Python 3.10.x não encontrado no sistema         ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║  O VexAI precisa do Python {PythonVersion} para funcionar.    ║");
            Console.WriteLine("║  Posso baixar e instalar automaticamente agora.          ║");
            Console.WriteLine("║  (~25 MB — instalação silenciosa, sem janelas extras)    ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.Write($"  Instalar Python {PythonVersion} automaticamente? (S/n): ");

            string input = Console.ReadLine()?.Trim().ToUpper() ?? "S";
            if (input == "N" || input == "NAO" || input == "NO")
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[Python] Instalação cancelada.");
                Console.WriteLine($"[Python] Baixe manualmente em: https://www.python.org/ftp/python/{PythonVersion}/{TempInstallerName}");
                Console.WriteLine("[Python] Marque 'Add Python to PATH' durante a instalação.");
                Console.ResetColor();
                return false;
            }

            return await DownloadAndInstallAsync();
        }

        /// <summary>
        /// Verifica se o Python 3.10.x está acessível pelo PATH atual.
        /// </summary>
        public static bool IsPython310Available()
        {
            foreach (var cmd in new[] { "python", "python3" })
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = cmd, Arguments = "--version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi)!;
                    string output = proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd();
                    proc.WaitForExit();
                    if (output.Contains("Python 3.10")) return true;
                }
                catch { }
            }
            return false;
        }

        // ---------------------------------------------------------------
        private static async Task<bool> DownloadAndInstallAsync()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), TempInstallerName);

            // ── Download ────────────────────────────────────────────────
            try
            {
                Console.WriteLine($"\n[Python] Baixando Python {PythonVersion} da python.org...");
                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
                using var response = await http.GetAsync(PythonInstallerUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long? total = response.Content.Headers.ContentLength;
                using var src  = await response.Content.ReadAsStreamAsync();
                using var dest = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                long downloaded = 0; int read;
                while ((read = await src.ReadAsync(buffer)) > 0)
                {
                    await dest.WriteAsync(buffer.AsMemory(0, read));
                    downloaded += read;
                    string progress = total.HasValue
                        ? $"{downloaded / 1024 / 1024} MB / {total.Value / 1024 / 1024} MB ({downloaded * 100 / total.Value}%)"
                        : $"{downloaded / 1024 / 1024} MB";
                    Console.Write($"\r[Python] Baixando... {progress}   ");
                }
                Console.WriteLine("\n[Python] Download concluído.");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[Python] Falha no download: {ex.Message}");
                Console.ResetColor();
                return false;
            }

            // ── Instalação silenciosa ───────────────────────────────────
            try
            {
                Console.WriteLine("[Python] Instalando... (pode demorar ~1 minuto)");

                var psi = new ProcessStartInfo
                {
                    FileName = tempPath,
                    // InstallAllUsers=0 → instala só para o usuário atual (não exige UAC)
                    // PrependPath=1     → adiciona ao PATH automaticamente
                    // Include_launcher=0 → pula o py launcher
                    Arguments      = "/quiet InstallAllUsers=0 PrependPath=1 Include_launcher=0",
                    UseShellExecute = true,
                    Verb            = "runas"   // solicita elevação se necessário
                };

                using var proc = Process.Start(psi)!;
                proc.WaitForExit();

                if (proc.ExitCode != 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[Python] Instalador retornou código {proc.ExitCode}.");
                    Console.ResetColor();
                    return false;
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[Python] Instalação concluída com sucesso!");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[Python] Erro ao instalar: {ex.Message}");
                Console.ResetColor();
                return false;
            }
            finally
            {
                try { File.Delete(tempPath); } catch { }
            }

            // ── Atualiza PATH do processo atual e re-verifica ───────────
            RefreshPathEnvironment();

            if (IsPython310Available())
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[Python] Python 3.10 detectado e pronto!");
                Console.ResetColor();
                return true;
            }

            // No Windows é comum o PATH só valer na próxima sessão
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[Python] Python instalado, mas o PATH só será atualizado na próxima execução.");
            Console.WriteLine("[Python] Feche e reabra o VexAI para continuar a instalação.");
            Console.ResetColor();
            return false;
        }

        /// <summary>
        /// Lê o PATH atualizado do registro do Windows e aplica ao processo atual.
        /// </summary>
        private static void RefreshPathEnvironment()
        {
            try
            {
                string? userPath   = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User);
                string? machinePath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine);
                string combined = string.Join(";", new[] { machinePath, userPath }
                    .Where(p => !string.IsNullOrWhiteSpace(p)));
                Environment.SetEnvironmentVariable("PATH", combined, EnvironmentVariableTarget.Process);
            }
            catch { }
        }
    }
}
