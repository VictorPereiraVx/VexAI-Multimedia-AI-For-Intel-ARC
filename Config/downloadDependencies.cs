using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VexAI.Config
{
    internal class downloadDependencies
    {
        private readonly string _installDirectory;
        private readonly string _sdNextFolder;

        // URL direta para o modelo Dreamshaper 8 no HuggingFace (formato safetensors)
        private const string ModelDownloadUrl = "https://huggingface.co/Lykon/DreamShaper/resolve/main/DreamShaper_8_pruned.safetensors";
        private const string ModelFileName = "dreamshaper_8.safetensors";

        public downloadDependencies(string targetDirectory)
        {
            _installDirectory = targetDirectory;
            _sdNextFolder = Path.Combine(_installDirectory, "automatic");
        }

        public async Task InstallAsync()
        {
            Console.WriteLine("Iniciando instalação do SD.Next para Intel ARC...");

            CloneRepository();

            ApplyCustomConfigurations();

            await DownloadDefaultModelAsync();

            Console.WriteLine("Instalação e configuração do SD.Next concluídas com sucesso!");
            Console.WriteLine("Para iniciar, execute o arquivo 'iniciar_intel.bat'.");
        }

        private void CloneRepository()
        {
            Console.WriteLine("Clonando repositório do SD.Next...");
            if (Directory.Exists(_sdNextFolder))
            {
                Console.WriteLine("A pasta já existe. Pulando clonagem.");
                return;
            }

            Directory.CreateDirectory(_installDirectory);

            var processInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "clone https://github.com/vladmandic/automatic.git",
                WorkingDirectory = _installDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            process.WaitForExit();

            if (process.ExitCode != 0)
                throw new Exception("Falha ao clonar o SD.Next. Verifique se o Git está instalado no sistema.");
        }

        private void ApplyCustomConfigurations()
        {
            Console.WriteLine("Aplicando configurações para Intel ARC...");

            string batPath = Path.Combine(_sdNextFolder, "iniciar_intel.bat");
            string batContent = @"@echo off
set COMMANDLINE_ARGS=--use-openvino --api --listen --autolaunch --insecure
call webui.bat";
            File.WriteAllText(batPath, batContent);

            string configPath = Path.Combine(_sdNextFolder, "config.json");
            string configContent = @"{
  ""sd_model_checkpoint"": ""dreamshaper_8 [879db523c3]"",
  ""outdir_txt2img_samples"": ""outputs\\text"",
  ""outdir_img2img_samples"": ""outputs\\image"",
  ""outdir_control_samples"": ""outputs\\control"",
  ""outdir_extras_samples"": ""outputs\\extras"",
  ""outdir_save"": ""outputs\\save"",
  ""outdir_video"": ""outputs\\video"",
  ""outdir_init_images"": ""outputs\\inputs"",
  ""outdir_txt2img_grids"": ""outputs\\grids"",
  ""outdir_img2img_grids"": ""outputs\\grids"",
  ""outdir_control_grids"": ""outputs\\grids"",
  ""gradio_theme"": ""Default"",
  ""diffusers_version"": ""f6b6a7181eb44f0120b29cd897c129275f366c2a"",
  ""sd_checkpoint_hash"": ""879db523c30d3b9017143d56705015e15a2cb5628762c11d086fed9538abd7fd"",
  ""disabled_extensions"": [
    ""sd-webui-faceswaplab""
  ]
}";
            File.WriteAllText(configPath, configContent);
        }

        private async Task DownloadDefaultModelAsync()
        {
            string modelsDir = Path.Combine(_sdNextFolder, "models", "Stable-diffusion");
            Directory.CreateDirectory(modelsDir); // Garante que a pasta existe

            string modelPath = Path.Combine(modelsDir, ModelFileName);

            if (File.Exists(modelPath))
            {
                Console.WriteLine("O modelo já está baixado.");
                return;
            }

            Console.WriteLine($"Baixando modelo {ModelFileName} (~2.1 GB — pode demorar bastante)...");

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromHours(2);

            using var response = await httpClient.GetAsync(ModelDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long? totalBytes = response.Content.Headers.ContentLength;
            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(modelPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[81920];
            long downloaded = 0; int read;
            while ((read = await contentStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read));
                downloaded += read;
                string progress = totalBytes.HasValue
                    ? $"{downloaded / 1024 / 1024} MB / {totalBytes.Value / 1024 / 1024} MB ({downloaded * 100 / totalBytes.Value}%)"
                    : $"{downloaded / 1024 / 1024} MB";
                Console.Write($"\r  Baixando... {progress}   ");
            }
            Console.WriteLine("\nDownload do modelo concluído!");
        }
    }
}
