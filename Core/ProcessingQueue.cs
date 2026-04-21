using System.Collections.Concurrent;
using VexAI.Config;

namespace VexAI.Core
{
    public class ImageJob
    {
        public string JobId { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; }
        public string Prompt { get; set; }
        public int Width { get; set; } = 512;
        public int Height { get; set; } = 512;
        public bool Upscale { get; set; }
        public bool Nsfw { get; set; }
        public float DenoisingStrength { get; set; } = 0.6f;
        public string InputImageBase64 { get; set; }
        public byte[] InputVideoBytes { get; set; }
        public string InputVideoPath { get; set; }
        public string FaceImageBase64 { get; set; }
        public string VoiceModel { get; set; }
        public int VoicePitch { get; set; } = 0;
        public Action<string> OnProgress { get; set; }
        public Action<string> OnComplete { get; set; }
        public Action<string> OnError { get; set; }
    }

    public class ProcessingQueue
    {
        private readonly ConcurrentQueue<ImageJob> _queue = new();
        private readonly Services.ImageService _imageService;
        private readonly Services.VoiceService _voiceService;
        private bool _running = false;
        private readonly object _lock = new();

        public int QueueLength => _queue.Count;
        public bool IsRunning => _running;

        public ProcessingQueue(Services.ImageService imageService, Services.VoiceService voiceService)
        {
            _imageService = imageService;
            _voiceService = voiceService;
        }

        public void Enqueue(ImageJob job)
        {
            _queue.Enqueue(job);
            Console.WriteLine($"[Fila] Job '{job.Type}' adicionado. Total na fila: {_queue.Count}");
            TriggerProcessing();
        }

        private void TriggerProcessing()
        {
            lock (_lock) { if (_running) return; _running = true; }
            Task.Run(ProcessLoopAsync);
        }

        private async Task ProcessLoopAsync()
        {
            while (_queue.TryDequeue(out var job))
            {
                try
                {
                    Console.WriteLine($"[Fila] Processando job: {job.Type} ({job.JobId})");
                    string result = await DispatchJobAsync(job);

                    if (result != null)
                        job.OnComplete?.Invoke(result);
                    else
                        job.OnError?.Invoke("Processamento falhou. Verifique os logs.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Fila] Erro no job {job.JobId}: {ex.Message}");
                    job.OnError?.Invoke($"Erro interno: {ex.Message}");
                }
            }
            lock (_lock) { _running = false; }
        }

        private async Task<string> DispatchJobAsync(ImageJob job)
        {
            return job.Type switch
            {
                "txt2img" => await _imageService.GenerateImageAsync(job.Prompt, job.Width, job.Height, job.Upscale, job.Nsfw),
                "img2img" => await _imageService.ReimagineImageAsync(job.Prompt, job.InputImageBase64, job.DenoisingStrength, job.Width, job.Height, job.Nsfw),
                "face-clone" => await _imageService.GenerateWithFaceAsync(job.Prompt, job.FaceImageBase64),
                "faceswap-fast" => await _imageService.GenerateFastFaceSwapAsync(job.InputVideoBytes, job.FaceImageBase64, job.OnProgress),
                "faceswap-enhanced" => await _imageService.GenerateEnhancedFaceSwapAsync(job.InputVideoBytes, job.FaceImageBase64, job.OnProgress),
                "videoart" => await _imageService.ReimagineVideoAsync(job.InputVideoPath, job.Prompt, job.DenoisingStrength, job.OnProgress),
                "voice-swap" => await _voiceService.TrocarVozVideoAsync(job.InputVideoPath, job.VoiceModel, job.VoicePitch, job.OnProgress),
                _ => null
            };
        }
    }
}
