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

        // ZIP com todos os modelos (~1GB) hospedado no Google Drive
        // Substitua FILE_ID pelo ID real do seu arquivo no Drive
        private const string ModelsZipGoogleDriveFileId = "1evI9mzgd983KdGKlNvjE4eQdTe1ogojr";
        private const string GfpganUrl = "https://github.com/TencentARC/GFPGAN/releases/download/v1.3.0/GFPGANv1.4.pth";

        // URL corrigida do inswapper (fallback caso o ZIP não funcione)
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

            // Verifica se os modelos já existem (inswapper é o mais importante)
            string inswapperPath = Path.Combine(modelsDir, "inswapper_128.onnx");
            if (File.Exists(inswapperPath))
            {
                Console.WriteLine("Modelos já existem. Pulando download.");
                return;
            }

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromHours(2);

            // Tenta baixar o ZIP de modelos do Google Drive (todos de uma vez)
            bool zipSuccess = await TryDownloadModelsZipAsync(httpClient, modelsDir);

            if (!zipSuccess)
            {
                // Fallback: baixar modelos individualmente
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[Aviso] Download via ZIP falhou. Tentando download individual dos modelos...");
                Console.ResetColor();
                await DownloadModelIndividualAsync(httpClient, InswapperUrl, inswapperPath, "inswapper_128.onnx");
            }

            // GFPGAN sempre baixado individualmente (está no GitHub Releases)
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
        /// Baixa o ZIP de modelos do Google Drive e extrai na pasta models/.
        /// 
        /// Arquivos >100MB no Google Drive exigem tratamento especial:
        /// A primeira requisição retorna uma página HTML de aviso de vírus.
        /// É necessário extrair o token de confirmação dessa resposta e
        /// fazer uma segunda requisição com esse token para obter o arquivo real.
        /// </summary>
        private async Task<bool> TryDownloadModelsZipAsync(HttpClient httpClient, string modelsDir)
        {
            string zipPath = Path.Combine(_installDirectory, "models_temp.zip");

            try
            {
                Console.WriteLine("Baixando pacote de modelos do Google Drive (~1GB)...");
                Console.WriteLine("Isso pode demorar dependendo da sua conexão. Aguarde...");

                // Passo 1: Requisição inicial — para arquivos grandes o Drive retorna
                // uma página HTML de aviso de vírus em vez do arquivo direto.
                string firstUrl = $"https://drive.google.com/uc?export=download&id={ModelsZipGoogleDriveFileId}";

                using var firstResponse = await httpClient.GetAsync(firstUrl, HttpCompletionOption.ResponseHeadersRead);
                firstResponse.EnsureSuccessStatusCode();

                string contentType = firstResponse.Content.Headers.ContentType?.MediaType ?? "";
                string finalUrl;

                if (contentType.Contains("text/html"))
                {
                    // Drive retornou página de aviso — precisamos extrair o token de confirmação
                    Console.WriteLine("  [Drive] Aviso de verificação detectado. Extraindo token de confirmação...");

                    string html = await firstResponse.Content.ReadAsStringAsync();

                    // O token está em um campo oculto ou na URL do formulário de confirmação
                    // Padrão atual: &confirm=XXXXX ou value="XXXXX" próximo a "confirm"
                    string confirmToken = ExtractGoogleDriveConfirmToken(html);

                    if (string.IsNullOrEmpty(confirmToken))
                    {
                        // Fallback: usar confirm=t que força o download em alguns casos
                        confirmToken = "t";
                        Console.WriteLine("  [Drive] Token não encontrado, usando confirm=t como fallback.");
                    }
                    else
                    {
                        Console.WriteLine($"  [Drive] Token extraído com sucesso.");
                    }

                    // Também captura o cookie de sessão retornado pelo Drive (obrigatório)
                    string cookies = "";
                    if (firstResponse.Headers.TryGetValues("Set-Cookie", out var cookieValues))
                        cookies = string.Join("; ", cookieValues.Select(c => c.Split(';')[0]));

                    finalUrl = $"https://drive.google.com/uc?export=download&id={ModelsZipGoogleDriveFileId}&confirm={confirmToken}";

                    // Passo 2: Requisição real com token + cookie de sessão
                    using var request = new HttpRequestMessage(HttpMethod.Get, finalUrl);
                    if (!string.IsNullOrEmpty(cookies))
                        request.Headers.Add("Cookie", cookies);

                    using var finalResponse = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    finalResponse.EnsureSuccessStatusCode();

                    await StreamToFileWithProgressAsync(finalResponse, zipPath);
                }
                else
                {
                    // Arquivo pequeno ou Drive entregou direto sem aviso
                    await StreamToFileWithProgressAsync(firstResponse, zipPath);
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
                if (File.Exists(zipPath))
                    File.Delete(zipPath);
                return false;
            }
        }

        /// <summary>
        /// Extrai o token de confirmação da página HTML de aviso do Google Drive.
        /// O Drive usa diferentes padrões dependendo da versão — tentamos todos.
        /// </summary>
        private static string ExtractGoogleDriveConfirmToken(string html)
        {
            // Padrão 1: &amp;confirm=XXXXX na URL do formulário
            var match = System.Text.RegularExpressions.Regex.Match(html, @"confirm=([0-9A-Za-z_\-]+)");
            if (match.Success)
                return match.Groups[1].Value;

            // Padrão 2: value="XXXXX" em campo hidden chamado "confirm"
            match = System.Text.RegularExpressions.Regex.Match(html, @"name=""confirm""\s+value=""([^""]+)""");
            if (match.Success)
                return match.Groups[1].Value;

            // Padrão 3: invertido — value antes do name
            match = System.Text.RegularExpressions.Regex.Match(html, @"value=""([^""]+)""\s+name=""confirm""");
            if (match.Success)
                return match.Groups[1].Value;

            return null;
        }

        /// <summary>
        /// Lê o stream de uma resposta HTTP e salva em arquivo, exibindo progresso.
        /// </summary>
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
                    // Tamanho desconhecido — mostra só o MB baixado
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
