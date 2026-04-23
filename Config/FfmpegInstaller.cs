using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace VexAI.Installers
{
    public class FfmpegInstaller
    {
        private readonly string _installDirectory;
        private readonly string _ffmpegFolder;

        private const string FfmpegZipUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";

        public FfmpegInstaller(string targetDirectory)
        {
            _installDirectory = targetDirectory;
            _ffmpegFolder = Path.Combine(_installDirectory, "tools", "ffmpeg");
        }

        public async Task InstallAsync()
        {
            Console.WriteLine("\n=== Iniciando instalação do FFmpeg ===");

            string ffmpegExe = Path.Combine(_ffmpegFolder, "ffmpeg.exe");
            if (File.Exists(ffmpegExe))
            {
                Console.WriteLine("O FFmpeg já está instalado. Pulando download.");
                return;
            }

            Directory.CreateDirectory(_ffmpegFolder);
            string zipPath = Path.Combine(_ffmpegFolder, "ffmpeg_temp.zip");

            try
            {
                // 1. Descarregar o ZIP
                Console.WriteLine("A transferir o FFmpeg (Isto pode demorar um pouco)...");
                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromMinutes(10);
                    var response = await httpClient.GetAsync(FfmpegZipUrl);
                    response.EnsureSuccessStatusCode();

                    using var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await response.Content.CopyToAsync(fs);
                }

                // 2. Extrair apenas os executáveis (.exe)
                Console.WriteLine("A extrair ficheiros...");
                using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        // Queremos apenas o ffmpeg.exe e o ffprobe.exe que estão dentro da pasta "bin/"
                        if (entry.FullName.Contains("/bin/") && entry.Name.EndsWith(".exe"))
                        {
                            string destinationPath = Path.Combine(_ffmpegFolder, entry.Name);
                            // Se já existir, substitui
                            entry.ExtractToFile(destinationPath, true);
                        }
                    }
                }

                // 3. Limpeza
                Console.WriteLine("A limpar ficheiros temporários...");
                File.Delete(zipPath);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Instalação do FFmpeg concluída com sucesso!");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Erro ao instalar o FFmpeg: {ex.Message}");
                Console.ResetColor();

                // Limpa o zip corrompido se falhar
                if (File.Exists(zipPath)) File.Delete(zipPath);
            }
        }
    }
}