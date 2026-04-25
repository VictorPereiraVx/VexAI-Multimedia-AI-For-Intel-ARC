using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace VexAI.Installers
{
    public class DeepLiveCamInstaller
    {
        private readonly string _installDirectory;
        private readonly string _deepLiveFolder;

        private const string ModelsZipGoogleDriveFileId = "1evI9mzgd983KdGKlNvjE4eQdTe1ogojr";
        private const string GfpganUrl = "https://github.com/TencentARC/GFPGAN/releases/download/v1.3.0/GFPGANv1.4.pth";

        private const string InswapperUrl = "https://huggingface.co/ezioruan/inswapper_128.onnx/resolve/main/inswapper_128.onnx";

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
            RunDependencyInstaller();

            Console.WriteLine("Instalação do DeepLiveCam concluída!");
        }

        private void RunDependencyInstaller()
        {
            string installBat = Path.Combine(_deepLiveFolder, "1_INSTALAR_DEPENDENCIAS.bat");
            if (!File.Exists(installBat)) return;

            Console.WriteLine("\n[DeepLiveCam] Instalando dependências Python automaticamente...");
            Console.WriteLine("[DeepLiveCam] Uma janela de terminal será aberta. Aguarde ela fechar para continuar.");

            var psi = new ProcessStartInfo
            {
                FileName = installBat,
                WorkingDirectory = _deepLiveFolder,
                UseShellExecute = true,   // janela visível para o usuário acompanhar
                WindowStyle = ProcessWindowStyle.Normal
            };

            using var proc = Process.Start(psi)!;
            proc.WaitForExit();

            if (proc.ExitCode == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[DeepLiveCam] Dependências instaladas com sucesso!");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[DeepLiveCam] Instalador encerrou com código {proc.ExitCode}. Verifique a janela de log.");
                Console.ResetColor();
            }
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

            string inswapperPath = Path.Combine(modelsDir, "inswapper_128.onnx");
            if (File.Exists(inswapperPath))
            {
                Console.WriteLine("Modelos já existem. Pulando download.");
                return;
            }

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromHours(2);

            bool zipSuccess = await TryDownloadModelsZipAsync(httpClient, modelsDir);

            if (!zipSuccess)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[Aviso] Download via ZIP falhou. Tentando download individual dos modelos...");
                Console.ResetColor();
                await DownloadModelIndividualAsync(httpClient, InswapperUrl, inswapperPath, "inswapper_128.onnx");
            }

            string gfpganPath = Path.Combine(modelsDir, "GFPGANv1.4.pth");
            if (!File.Exists(gfpganPath))
            {
                await DownloadModelIndividualAsync(httpClient, GfpganUrl, gfpganPath, "GFPGANv1.4.pth");
            }
            else
            {
                Console.WriteLine("Modelo GFPGANv1.4.pth já existe.");
            }

            Console.WriteLine("Verificação de modelos concluída!");
        }

        /// <summary>
        /// Baixa o ZIP de modelos do Google Drive.
        /// 
        /// O Google Drive para arquivos grandes bloqueia downloads automáticos com
        /// uma página de confirmação de vírus. A abordagem mais confiável em 2025
        /// é usar a URL de export com confirm=t + cookie de bypass na mesma requisição.
        /// Se ainda assim retornar HTML, faz fallback para download individual.
        /// </summary>
        private async Task<bool> TryDownloadModelsZipAsync(HttpClient httpClient, string modelsDir)
        {
            string zipPath = Path.Combine(_installDirectory, "models_temp.zip");

            try
            {
                Console.WriteLine("Baixando pacote de modelos do Google Drive (~1GB)...");
                Console.WriteLine("Isso pode demorar dependendo da sua conexão. Aguarde...");

                // Método 1: URL direta com confirm=t (bypassa a tela de vírus)
                string downloadUrl = $"https://drive.google.com/uc?export=download&id={ModelsZipGoogleDriveFileId}&confirm=t";

                using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
                // Cookie necessário para o bypass funcionar
                request.Headers.Add("Cookie", $"download_warning_{ModelsZipGoogleDriveFileId}=t");

                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                string contentType = response.Content.Headers.ContentType?.MediaType ?? "";

                // Se retornou HTML, o Drive está exigindo outro formato de confirmação
                if (contentType.Contains("text/html"))
                {
                    Console.WriteLine("  [Drive] Resposta HTML recebida. Tentando método alternativo...");

                    string html = await response.Content.ReadAsStringAsync();

                    // Extrai o uuid do form de confirmação (formato atual do Google Drive)
                    string? uuid = ExtractGoogleDriveUuid(html);
                    if (string.IsNullOrEmpty(uuid))
                    {
                        Console.WriteLine("  [Drive] Não foi possível extrair token de confirmação do Drive.");
                        return false;
                    }

                    string confirmUrl = $"https://drive.google.com/uc?export=download&id={ModelsZipGoogleDriveFileId}&confirm=t&uuid={uuid}";
                    using var confirmRequest = new HttpRequestMessage(HttpMethod.Get, confirmUrl);
                    confirmRequest.Headers.Add("Cookie", $"download_warning_{ModelsZipGoogleDriveFileId}=t");

                    using var finalResponse = await httpClient.SendAsync(confirmRequest, HttpCompletionOption.ResponseHeadersRead);
                    finalResponse.EnsureSuccessStatusCode();

                    string finalContentType = finalResponse.Content.Headers.ContentType?.MediaType ?? "";
                    if (finalContentType.Contains("text/html"))
                    {
                        Console.WriteLine("  [Drive] Drive ainda retornou HTML após confirmação. Usando fallback individual.");
                        return false;
                    }

                    await StreamToFileWithProgressAsync(finalResponse, zipPath);
                }
                else
                {
                    await StreamToFileWithProgressAsync(response, zipPath);
                }

                // Valida que o arquivo baixado é um ZIP real (não HTML disfarçado)
                if (!IsValidZipFile(zipPath))
                {
                    Console.WriteLine("\n  [Drive] Arquivo baixado não é um ZIP válido. Usando fallback individual.");
                    File.Delete(zipPath);
                    return false;
                }

                Console.WriteLine("\nDownload concluído! Extraindo modelos...");
                ZipFile.ExtractToDirectory(zipPath, modelsDir, overwriteFiles: true);
                Console.WriteLine($"Modelos extraídos em: {modelsDir}");
                File.Delete(zipPath);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[Erro no download do ZIP]: {ex.Message}");
                if (File.Exists(zipPath)) File.Delete(zipPath);
                return false;
            }
        }

        private static string? ExtractGoogleDriveUuid(string html)
        {
            // Formato atual do Google Drive: &uuid=XXXX no form action
            var match = System.Text.RegularExpressions.Regex.Match(html, @"uuid=([0-9a-f\-]+)");
            if (match.Success) return match.Groups[1].Value;

            // Fallback: campo hidden no form
            match = System.Text.RegularExpressions.Regex.Match(html, @"name=""uuid""\s+value=""([^""]+)""");
            if (match.Success) return match.Groups[1].Value;

            return null;
        }

        /// <summary>
        /// Verifica se o arquivo começa com a assinatura de um ZIP (PK\x03\x04).
        /// Evita tentar extrair HTML que o Drive retornou por engano.
        /// </summary>
        private static bool IsValidZipFile(string path)
        {
            try
            {
                using var fs = File.OpenRead(path);
                if (fs.Length < 4) return false;
                var header = new byte[4];
                fs.ReadExactly(header);
                return header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04;
            }
            catch { return false; }
        }

        private static async Task StreamToFileWithProgressAsync(HttpResponseMessage response, string destPath)
        {
            long? totalBytes = response.Content.Headers.ContentLength;
            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            byte[] buffer = new byte[81920];
            long downloaded = 0;
            int read;
            int lastPercent = -1;

            while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, read);
                downloaded += read;

                if (totalBytes.HasValue && totalBytes.Value > 0)
                {
                    int percent = (int)(downloaded * 100 / totalBytes.Value);
                    if (percent != lastPercent && percent % 5 == 0)
                    {
                        Console.Write($"\r  Progresso: {percent}% ({downloaded / 1024 / 1024}MB / {totalBytes.Value / 1024 / 1024}MB)");
                        lastPercent = percent;
                    }
                }
                else
                {
                    if (downloaded % (10 * 1024 * 1024) == 0)
                        Console.Write($"\r  Baixado: {downloaded / 1024 / 1024}MB...");
                }
            }
        }

        private async Task DownloadModelIndividualAsync(HttpClient httpClient, string url, string destPath, string displayName)
        {
            if (File.Exists(destPath))
            {
                Console.WriteLine($"Modelo {displayName} já existe.");
                return;
            }

            Console.WriteLine($"Baixando {displayName}...");
            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            await contentStream.CopyToAsync(fileStream);
            Console.WriteLine($"[OK] {displayName} baixado.");
        }

        private void CreateLaunchScripts()
        {
            Console.WriteLine("Criando scripts de inicialização para Intel ARC...");

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

            string runBatPath = Path.Combine(_deepLiveFolder, "2_INICIAR_INTEL_ARC.bat");
            string runContent = @"@echo off
call venv\Scripts\activate
python run.py --execution-provider openvino
pause";
            File.WriteAllText(runBatPath, runContent);
        }
    }
}
