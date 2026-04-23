# VexAI — AI Media Processing Tool (Intel Arc Edition)

> Ferramenta CLI para Windows que integra geração de imagem, troca de rosto, arte em vídeo e conversão de voz — otimizada para **GPUs Intel Arc** via DirectML e OpenVINO.

---

## Índice

- [Visão Geral](#visão-geral)
- [Requisitos do Sistema](#requisitos-do-sistema)
- [Instalação Automática](#instalação-automática)
- [Instalação Manual das Dependências](#instalação-manual-das-dependências)
  - [1. .NET 8 SDK](#1-net-8-sdk)
  - [2. FFmpeg](#2-ffmpeg)
  - [3. SD.Next](#3-sdnext-geração-de-imagem)
  - [4. Deep-Live-Cam](#4-deep-live-cam-troca-de-rosto)
  - [5. RVC](#5-rvc-conversão-de-voz)
- [Configuração Inicial](#configuração-inicial)
- [Comandos Disponíveis](#comandos-disponíveis)
- [Modelos de Voz (RVC)](#modelos-de-voz-rvc)
- [Estrutura do config.json](#estrutura-do-configjson)
- [FAQ](#faq)
- [Changelog](#changelog)

---

## Visão Geral

VexAI unifica várias ferramentas de IA em uma única interface de linha de comando. Tudo configurado e pronto com um único instalador.

| Funcionalidade | Descrição |
|---|---|
| 🎨 **Gerar imagem** | Cria imagens a partir de texto (txt2img) via SD.Next |
| ✨ **Reimaginar** | Transforma uma imagem existente com novo prompt (img2img) |
| 🎭 **Clonar rosto** | Gera cenas preservando a identidade de um rosto (IP-Adapter) |
| 🔄 **Face Swap (Rápido)** | Troca rosto em vídeo usando DirectML (nativo Intel Arc) |
| 💎 **Face Swap (Aprimorado)** | Troca de rosto em qualidade superior usando OpenVINO |
| 🎬 **Video Art** | Aplica estilos artísticos em vídeo frame a frame via SD.Next |
| 🎙️ **Troca de Voz** | Substitui a voz em um vídeo usando RVC |

---

## Requisitos do Sistema

- **Sistema Operacional:** Windows 10 ou Windows 11 (64-bit)
- **GPU:** Intel Arc recomendada (compatível com DirectML e OpenVINO). GPUs NVIDIA também funcionam via SD.Next.
- **RAM:** 16 GB mínimo recomendado
- **Armazenamento:** 30–50 GB livres mínimo (modelos de IA ocupam bastante espaço)
- **Git:** instalado e disponível no PATH ([git-scm.com](https://git-scm.com))
- **Python:** 3.10 ou superior instalado e disponível no PATH

---

## Instalação Automática

O VexAI vem com um instalador embutido que cuida de tudo automaticamente:

```
dotnet run
```

Na **primeira execução**, o programa detecta que não está configurado e abre o **assistente de instalação**, que irá:

1. Clonar o repositório do **Deep-Live-Cam**
2. Baixar o **pacote de modelos** diretamente do Google Drive (~1 GB) e extrair automaticamente na pasta `models/`
3. Criar os scripts de inicialização para Intel Arc (`1_INSTALAR_DEPENDENCIAS.bat` e `2_INICIAR_INTEL_ARC.bat`)
4. Clonar o **RVC-BETA** e criar os scripts de instalação corrigidos para CPU/OpenVINO
5. Copiar o modelo de voz padrão (`lula.pth`) para `assets/weights/`
6. Baixar e instalar o **FFmpeg** automaticamente, registrando o caminho completo no `config.json`

> Você só precisa ter o Git e o Python instalados. O resto é feito pelo VexAI.

### Sobre o download dos modelos do Deep-Live-Cam

O pacote de modelos (~1 GB) está hospedado no Google Drive. Arquivos grandes no Drive exigem uma confirmação de verificação antes do download — o instalador lida com isso automaticamente:

- Faz a requisição inicial e detecta o aviso de verificação do Drive
- Extrai o token de confirmação da resposta
- Captura o cookie de sessão necessário
- Realiza o download real com token + cookie
- Exibe progresso em % durante o download
- Se algo falhar, tenta baixar os modelos individualmente como fallback

---

## Instalação Manual das Dependências

Se preferir instalar tudo manualmente em vez de usar o instalador automático, siga os passos abaixo.

---

### 1. .NET 8 SDK

O VexAI é escrito em C# e requer o .NET 8 para compilar e executar.

1. Acesse: [https://dotnet.microsoft.com/en-us/download/dotnet/8](https://dotnet.microsoft.com/en-us/download/dotnet/8)
2. Baixe o **SDK** (não o Runtime) para Windows x64
3. Execute o instalador
4. Verifique:
   ```
   dotnet --version
   ```

---

### 2. FFmpeg

Usado internamente para extrair áudio, mixar trilhas e renderizar vídeos finais.

O instalador automático do VexAI baixa e configura o FFmpeg sozinho. Se quiser instalar manualmente:

1. Acesse: [https://ffmpeg.org/download.html](https://ffmpeg.org/download.html)
2. Baixe a versão **Windows builds by BtbN**
3. Extraia para uma pasta, por exemplo: `C:\ffmpeg`
4. Adicione `C:\ffmpeg\bin` ao PATH do sistema
5. Verifique:
   ```
   ffmpeg -version
   ```

> **Importante:** Se instalado manualmente, defina o caminho completo do `ffmpeg.exe` no campo `FfmpegPath` do `config.json`. O VexAI usa o caminho completo configurado — não depende do PATH do sistema para funcionar.

---

### 3. SD.Next (geração de imagem)

Servidor local de geração de imagem (Stable Diffusion). Precisa estar **em execução** para os comandos `gerar`, `reimaginar`, `clonar` e `videoart`.

1. Acesse: [https://github.com/vladmandic/automatic](https://github.com/vladmandic/automatic)
2. Clone e entre na pasta:
   ```bash
   git clone https://github.com/vladmandic/automatic
   cd automatic
   ```
3. Para Intel Arc, edite o `webui-user.bat` e adicione:
   ```
   set COMMANDLINE_ARGS=--use-openvino --api --listen --autolaunch --insecure
   ```
4. Execute `webui.bat` e aguarde:
   ```
   Running on local URL:  http://127.0.0.1:7860
   ```

#### Modelos de imagem

Coloque arquivos `.safetensors` ou `.ckpt` em `<sdnext>/models/Stable-diffusion/`. O modelo padrão configurado pelo VexAI é o **DreamShaper 8**, baixado de:
```
https://huggingface.co/Lykon/dreamshaper-8
```

---

### 4. Deep-Live-Cam (troca de rosto)

Usado pelo comando `faceswap`. O VexAI suporta dois modos:

#### Modo Rápido (DirectML — nativo Intel Arc)

```bash
git clone https://github.com/hacksider/Deep-Live-Cam
cd Deep-Live-Cam
python -m venv venv
call venv\Scripts\activate
pip install -r requirements.txt
pip install onnxruntime-directml
```

Os modelos (`inswapper_128.onnx` e `GFPGANv1.4.pth`) são baixados automaticamente pelo instalador do VexAI via pacote do Google Drive. Se preferir baixar manualmente, coloque-os em `Deep-Live-Cam/models/`.

#### Modo Aprimorado (OpenVINO — qualidade superior)

```bash
pip install openvino
pip install onnxruntime-openvino
```

---

### 5. RVC (conversão de voz)

Usado pelo comando `voz`.

```bash
git clone https://github.com/RVC-Project/Retrieval-based-Voice-Conversion-WebUI RVC-BETA
cd RVC-BETA
python -m venv venv
call venv\Scripts\activate
pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cpu
pip install openvino onnxruntime-openvino
pip install -r requirements.txt
pip install audio-separator
```

> **Atenção:** O VexAI instala PyTorch com suporte a **CPU/OpenVINO**, não CUDA. Isso é intencional — Intel Arc não usa CUDA (NVIDIA). Instalar a versão CUDA causaria erros ou fallback para CPU sem aceleração.

A estrutura esperada do RVC:

```
<rvc-folder>/
├── venv/
│   └── Scripts/
│       ├── python.exe
│       └── audio-separator.exe   ← instalado via pip install audio-separator
└── assets/
    └── weights/
        └── lula.pth              ← copiado automaticamente pelo VexAI
```

O script de inferência usado pelo VexAI é o `Config/ExtraConfig/infer_cli.py`, incluído no próprio projeto — não depende do `tools/infer_cli.py` do repositório RVC, que nem sempre existe no clone padrão.

---

## Configuração Inicial

Na primeira execução, o VexAI abre o assistente de configuração automaticamente. Você também pode acessá-lo a qualquer momento com o comando `config`.

| Campo | O que informar |
|---|---|
| **Pasta de saída** | Onde os arquivos gerados serão salvos. Ex: `C:\Users\Seu Nome\Documents\VexAI_Output` |
| **Pasta raiz do RVC** | Caminho da instalação do RVC. Ex: `C:\RVC-BETA` |
| **Pasta do Deep-Live-Cam (Rápido)** | Instalação com DirectML. Ex: `C:\Deep-Live-Cam` |
| **Pasta do Deep-Live-Cam (Aprimorado)** | Instalação com OpenVINO. *(Opcional — Enter para pular)* |
| **Caminho do .bat do SD.Next** | Caminho completo do arquivo `.bat` de inicialização. *(Opcional)* |
| **URL da API do SD.Next** | Deixe o padrão `http://127.0.0.1:7860` |
| **Imagem de marca d'água** | Caminho de um `.png` para sobrepor nas saídas. *(Opcional)* |
| **Iniciar SD.Next automaticamente?** | `y` para iniciar junto com o VexAI |

As configurações são salvas em `config.json` na mesma pasta do executável.

---

## Comandos Disponíveis

Após a configuração, o VexAI entra em modo interativo:

```
VexAI> ajuda
```

---

### `gerar` — Gerar imagem a partir de texto

Gera uma imagem a partir de uma descrição textual (txt2img) usando SD.Next.

**Pré-requisito:** SD.Next em execução em `http://127.0.0.1:7860`.

```
VexAI> gerar
Prompt: cidade futurista à noite, neon, cyberpunk
Qualidade: hd (720p)
Conteúdo 18+? n
```

---

### `reimaginar` — Reimaginar uma imagem existente

Transforma uma imagem existente com base em um novo prompt (img2img).

```
VexAI> reimaginar
Caminho da imagem: C:\fotos\retrato.jpg
Prompt: pintura a óleo, estilo renascentista
Força da mudança: 0.6
```

---

### `clonar` — Clonar rosto em uma cena

Gera novas imagens preservando a identidade de um rosto via IP-Adapter.

```
VexAI> clonar
Prompt: astronauta no espaço, iluminação cinematográfica
Caminho da foto do rosto: C:\fotos\meu_rosto.jpg
```

---

### `faceswap` — Trocar rosto em vídeo

Substitui o rosto em um vídeo pelo rosto de uma foto de referência.

```
VexAI> faceswap
Caminho do vídeo: C:\videos\clipe.mp4
Foto do rosto: C:\fotos\rosto.jpg
Modo: fast (DirectML)
```

---

### `videoart` — Aplicar estilo artístico em vídeo

Aplica um estilo artístico a cada frame do vídeo via SD.Next (img2img frame a frame).

> **Atenção:** vídeos longos demoram muito. Use vídeos curtos (menos de 30 segundos) para testes.

```
VexAI> videoart
Caminho do vídeo: C:\videos\caminhada.mp4
Prompt: aquarela, cores suaves
Força: 0.5
```

---

### `voz` — Trocar voz em vídeo

Substitui a voz humana em um vídeo usando um modelo RVC. O pipeline completo é:

1. Extrai o áudio do vídeo
2. Separa voz e instrumental com UVR-MDX (via `audio-separator`)
3. Converte a voz com o modelo RVC selecionado (inferência via `infer_cli.py`)
4. Mixa o resultado e renderiza o vídeo final com FFmpeg

```
VexAI> voz
Caminho do vídeo: C:\videos\entrevista.mp4
Modelo: lula
Pitch: 0
```

---

### `modelos` — Listar modelos de voz

Lista todos os arquivos `.pth` disponíveis em `assets/weights/`.

```
VexAI> modelos
```

---

### `fila` — Status da fila

Mostra quantos jobs estão aguardando e se há um em execução.

```
VexAI> fila
[Fila] Jobs pendentes: 2 | Em execução: Sim
```

---

### `config` — Reconfigurar

Abre o assistente de configuração novamente para alterar qualquer caminho ou opção.

```
VexAI> config
```

---

## Modelos de Voz (RVC)

Para usar o comando `voz`, você precisa de modelos RVC treinados (arquivos `.pth`).

**Onde encontrar:**
- [https://huggingface.co](https://huggingface.co) — pesquise por `rvc voice model`
- Comunidades RVC no Discord e Reddit
- Treine o seu próprio modelo pelo RVC WebUI

**Como instalar:**
1. Baixe o arquivo `.pth` desejado
2. Copie para: `<rvc-folder>/assets/weights/NomeDaVoz.pth`
3. O VexAI detecta automaticamente na próxima vez que você usar `modelos` ou `voz`

O modelo **lula.pth** já vem instalado por padrão pelo VexAI.

---

## Estrutura do config.json

O arquivo `config.json` é criado automaticamente na pasta do executável. Você pode editá-lo manualmente se preferir:

```json
{
  "SdNextUrl": "http://127.0.0.1:7860",
  "SdNextBatPath": "C:\\automatic\\webui-user.bat",
  "RvcFolderPath": "C:\\RVC-BETA",
  "DeepLiveFastPath": "C:\\Deep-Live-Cam",
  "DeepLiveEnhancedPath": "C:\\Deep-Live-Cam-OpenVINO",
  "WatermarkImagePath": "C:\\logos\\watermark.png",
  "OutputFolder": "C:\\Users\\Seu Nome\\Documents\\VexAI_Output",
  "FfmpegPath": "C:\\VexAI\\tools\\ffmpeg\\ffmpeg.exe",
  "AutoStartSdNext": true
}
```

| Campo | Descrição |
|---|---|
| `SdNextUrl` | URL da API do SD.Next (padrão: `http://127.0.0.1:7860`) |
| `SdNextBatPath` | Caminho do `.bat` de inicialização do SD.Next (opcional) |
| `RvcFolderPath` | Pasta raiz do RVC (deve conter `venv/` e `assets/weights/`) |
| `DeepLiveFastPath` | Pasta do Deep-Live-Cam com DirectML |
| `DeepLiveEnhancedPath` | Pasta do Deep-Live-Cam com OpenVINO (opcional) |
| `WatermarkImagePath` | Caminho da imagem `.png` de marca d'água (opcional) |
| `OutputFolder` | Pasta onde todos os arquivos gerados são salvos |
| `FfmpegPath` | Caminho completo do `ffmpeg.exe` — definido automaticamente pelo instalador |
| `AutoStartSdNext` | `true` para iniciar SD.Next automaticamente junto com o VexAI |

---

## FAQ

**O SD.Next precisa estar em execução para usar o VexAI?**
Sim, para `gerar`, `reimaginar`, `clonar` e `videoart`. Para `faceswap` e `voz` não é necessário. Se você configurou `SdNextBatPath` e ativou `AutoStartSdNext`, o VexAI tenta iniciá-lo automaticamente.

**O VexAI funciona com GPU NVIDIA ou AMD?**
Sim. Intel Arc é recomendada por ter suporte nativo a DirectML e OpenVINO, mas SD.Next e Deep-Live-Cam suportam CUDA (NVIDIA) e ROCm (AMD). Ajuste as instalações conforme o guia de cada ferramenta.

**Onde ficam os arquivos gerados?**
Tudo é salvo na pasta definida em `OutputFolder`, organizado em subpastas:
- `output/images/` — imagens geradas
- `output/audio/` — arquivos de áudio intermediários do pipeline de voz
- `output/video/` — vídeos finais de faceswap e voiceswap

**Como reconfigurar o VexAI após a instalação?**
Use o comando `config` dentro do programa, ou edite manualmente o `config.json`.

**O processamento de vídeo está demorando demais.**
O comando `videoart` processa frame a frame pelo SD.Next — vídeos longos são lentos por design. Use vídeos curtos (menos de 30 segundos) para testes iniciais.

**O download dos modelos falhou.**
O instalador tem fallback automático: se o pacote ZIP do Google Drive falhar, tenta baixar os modelos individualmente. Se ainda assim falhar, baixe manualmente e coloque em `Deep-Live-Cam/models/`.

---

## Changelog

### Correções recentes

| O que mudou | Antes | Agora |
|---|---|---|
| **Download dos modelos Deep-Live-Cam** | Cada modelo baixado individualmente de HuggingFace/GitHub | Pacote ZIP único (~1 GB) baixado do Google Drive com suporte ao aviso de verificação de vírus do Drive (token + cookie), com fallback individual automático |
| **URL do DreamShaper** | `Lykon/DreamShaper` — repositório errado, arquivo inexistente | `Lykon/dreamshaper-8` — repositório correto |
| **PyTorch no RVC** | Instalava versão CUDA (`--index-url .../cu118`) — incompatível com Intel Arc | Instala versão CPU (`--index-url .../cpu`) + `openvino` + `onnxruntime-openvino` |
| **audio-separator** | Não era instalado no `.bat` do RVC — pipeline de voz falhava silenciosamente | Adicionado `pip install audio-separator` ao script de instalação |
| **infer_cli.py** | Buscava `tools/infer_cli.py` no clone do RVC — arquivo que não existe no branch main | Usa `Config/ExtraConfig/infer_cli.py` incluído no próprio projeto VexAI |
| **lula.pth no build** | Não era copiado para a pasta de build — ausente ao publicar o executável | Diretiva `<CopyToOutputDirectory>Always</CopyToOutputDirectory>` adicionada ao `.csproj` |
| **FFmpeg** | Chamado pelo nome `ffmpeg` assumindo PATH do sistema — falhava se não estivesse no PATH | Instalador salva o caminho completo no `config.json`; serviços usam o caminho completo |
