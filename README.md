# VexAI — AI Media Processing Tool (Intel Arc Edition)

> Ferramenta CLI para Windows com geração de imagens por IA, troca de rostos em vídeo, arte em vídeo e conversão de voz — otimizada para **GPUs Intel Arc** via DirectML e OpenVINO.

---

## Índice

- [Visão Geral](#visão-geral)
- [Requisitos do Sistema](#requisitos-do-sistema)
- [Instalação das Dependências](#instalação-das-dependências)
  - [1. .NET 8 SDK](#1-net-8-sdk)
  - [2. FFmpeg](#2-ffmpeg)
  - [3. SD.Next (geração de imagens)](#3-sdnext-geração-de-imagens)
  - [4. Deep-Live-Cam (troca de rosto)](#4-deep-live-cam-troca-de-rosto)
  - [5. RVC (conversão de voz)](#5-rvc-conversão-de-voz)
- [Instalação do VexAI](#instalação-do-vexai)
- [Configuração Inicial](#configuração-inicial)
- [Comandos Disponíveis](#comandos-disponíveis)
  - [gerar — Gerar imagem por texto](#gerar--gerar-imagem-por-texto)
  - [reimaginar — Reimaginar imagem existente](#reimaginar--reimaginar-imagem-existente)
  - [clonar — Clonar rosto em cena](#clonar--clonar-rosto-em-cena)
  - [faceswap — Trocar rosto em vídeo](#faceswap--trocar-rosto-em-vídeo)
  - [videoart — Arte em vídeo](#videoart--arte-em-vídeo)
  - [voz — Trocar voz em vídeo](#voz--trocar-voz-em-vídeo)
  - [modelos — Listar modelos RVC](#modelos--listar-modelos-rvc)
  - [fila — Status da fila](#fila--status-da-fila)
  - [config — Reconfigurar](#config--reconfigurar)
- [Modelos de Voz (RVC)](#modelos-de-voz-rvc)
- [Estrutura do config.json](#estrutura-do-configjson)
- [Perguntas Frequentes](#perguntas-frequentes)

---

## Visão Geral

O VexAI é uma ferramenta de linha de comando (CLI) que une várias ferramentas de IA em uma interface unificada. Com ele você pode:

| Funcionalidade | O que faz |
|---|---|
| 🎨 **Gerar imagem** | Cria imagens a partir de texto (txt2img) via SD.Next |
| ✨ **Reimaginar** | Transforma uma imagem existente com um novo prompt (img2img) |
| 🎭 **Clonar rosto** | Gera cenas preservando a identidade de um rosto (IP-Adapter) |
| 🔄 **Face Swap Rápido** | Troca rosto em vídeo usando DirectML (nativo Intel Arc) |
| 💎 **Face Swap Melhorado** | Troca de rosto de maior qualidade usando OpenVINO |
| 🎬 **Video Art** | Aplica estilo artístico em vídeo frame a frame via SD.Next |
| 🎙️ **Voice Swap** | Substitui a voz em um vídeo usando RVC |

---

## Requisitos do Sistema

- **Sistema Operacional:** Windows 10 ou Windows 11 (64-bit)
- **GPU:** Intel Arc recomendado (compatível com DirectML e OpenVINO). GPUs NVIDIA também funcionam via SD.Next.
- **RAM:** mínimo 16 GB recomendado
- **Armazenamento:** mínimo 30–50 GB livres (modelos de IA ocupam bastante espaço)

---

## Instalação das Dependências

Antes de usar o VexAI, você precisa instalar e configurar todas as ferramentas externas abaixo. Siga cada passo com atenção.

---

### 1. .NET 8 SDK

O VexAI é desenvolvido em C# e precisa do .NET 8 para compilar e rodar.

1. Acesse: [https://dotnet.microsoft.com/en-us/download/dotnet/8](https://dotnet.microsoft.com/en-us/download/dotnet/8)
2. Baixe o **SDK** (não o Runtime) para Windows x64.
3. Execute o instalador e siga as instruções.
4. Verifique a instalação abrindo um terminal e digitando:
   ```
   dotnet --version
   ```
   Deve retornar algo como `8.0.x`.

---

### 2. FFmpeg

O FFmpeg é usado internamente pelo VexAI para extrair áudio, mixar faixas e renderizar vídeos finais.

1. Acesse: [https://ffmpeg.org/download.html](https://ffmpeg.org/download.html)
2. Em **Windows**, clique em **Windows builds by BtbN** ou **gyan.dev**.
3. Baixe o arquivo `ffmpeg-release-essentials.zip` (ou similar).
4. Extraia o conteúdo para uma pasta, por exemplo: `C:\ffmpeg`
5. **Adicione ao PATH do sistema:**
   - Abra **Painel de Controle → Sistema → Configurações avançadas do sistema → Variáveis de Ambiente**
   - Em "Variáveis do sistema", selecione `Path` e clique em **Editar**
   - Adicione o caminho da pasta `bin` do FFmpeg, por exemplo: `C:\ffmpeg\bin`
   - Clique em OK em todas as janelas
6. Verifique abrindo um novo terminal:
   ```
   ffmpeg -version
   ```

---

### 3. SD.Next (geração de imagens)

O SD.Next é o servidor local de geração de imagens (Stable Diffusion). Ele precisa estar **rodando** sempre que você usar os comandos `gerar`, `reimaginar`, `clonar` ou `videoart`.

1. Acesse: [https://github.com/vladmandic/automatic](https://github.com/vladmandic/automatic)
2. Siga as instruções de instalação do repositório. Resumidamente:
   ```bash
   git clone https://github.com/vladmandic/automatic
   cd automatic
   ```
3. Na primeira execução, o SD.Next baixa automaticamente os modelos necessários.
4. Para GPUs Intel Arc, use o modo **DirectML**. Edite `webui-user.bat` e adicione a flag:
   ```
   set COMMANDLINE_ARGS=--use-directml
   ```
5. Inicie o servidor executando `webui.bat` (ou `webui-user.bat`). Aguarde até aparecer:
   ```
   Running on local URL:  http://127.0.0.1:7860
   ```
6. Guarde o caminho completo do arquivo `.bat` de inicialização — você vai precisar dele na configuração do VexAI.

> **Dica:** Se o seu PC tem GPU NVIDIA, você pode usar a instalação padrão do SD.Next sem a flag `--use-directml`.

#### Baixando um modelo de imagem (opcional, mas recomendado)

O SD.Next funciona com qualquer modelo `.safetensors` ou `.ckpt` compatível com Stable Diffusion. Para resultados melhores:

1. Acesse [https://civitai.com](https://civitai.com) ou [https://huggingface.co](https://huggingface.co)
2. Baixe um modelo de sua preferência (ex: **Realistic Vision**, **DreamShaper**, etc.)
3. Coloque o arquivo na pasta: `<sdnext>/models/Stable-diffusion/`
4. Reinicie o SD.Next e selecione o modelo na interface web (http://127.0.0.1:7860)

---

### 4. Deep-Live-Cam (troca de rosto)

O Deep-Live-Cam é usado pelos comandos `faceswap`. O VexAI suporta **dois modos** com instalações separadas:

#### Modo Rápido (DirectML — Intel Arc nativo)

1. Acesse: [https://github.com/hacksider/Deep-Live-Cam](https://github.com/hacksider/Deep-Live-Cam)
2. Siga as instruções de instalação para **DirectML** (Intel Arc / AMD):
   ```bash
   git clone https://github.com/hacksider/Deep-Live-Cam
   cd Deep-Live-Cam
   pip install -r requirements.txt
   pip install onnxruntime-directml
   ```
3. Baixe os modelos necessários conforme indicado no repositório (geralmente há um script ou link para download automático).
4. Guarde o caminho da pasta raiz do Deep-Live-Cam (ex: `C:\Deep-Live-Cam`).

#### Modo Melhorado (OpenVINO — maior qualidade)

1. No mesmo repositório, siga as instruções para instalar com **OpenVINO**:
   ```bash
   pip install openvino
   pip install onnxruntime-openvino
   ```
2. Pode ser uma pasta separada para evitar conflitos entre as dependências.
3. Guarde o caminho da pasta desta instalação também.

> **Nota:** O modo melhorado é opcional. Se não precisar de qualidade máxima, pode usar apenas o modo rápido.

---

### 5. RVC (conversão de voz)

O RVC (Retrieval-based Voice Conversion) é usado pelo comando `voz` para trocar a voz em vídeos.

1. Acesse: [https://github.com/RVC-Project/Retrieval-based-Voice-Conversion-WebUI](https://github.com/RVC-Project/Retrieval-based-Voice-Conversion-WebUI)
2. Siga o guia de instalação do repositório:
   ```bash
   git clone https://github.com/RVC-Project/Retrieval-based-Voice-Conversion-WebUI
   cd Retrieval-based-Voice-Conversion-WebUI
   pip install -r requirements.txt
   ```
3. A estrutura de pastas do RVC deve conter:
   ```
   <rvc-folder>/
   ├── venv/               ← ambiente virtual Python
   └── assets/
       └── weights/        ← onde ficam os modelos .pth
   ```
4. Baixe os modelos base necessários conforme as instruções do repositório (normalmente disponíveis na aba Releases ou via scripts de download).
5. Guarde o caminho da pasta raiz do RVC (ex: `C:\RVC-WebUI`).

#### Adicionando modelos de voz

Para usar uma voz específica, você precisa de um arquivo `.pth` treinado com aquela voz:

1. Procure modelos no repositório da comunidade RVC ou treine o seu próprio.
2. Coloque o arquivo `.pth` em:
   ```
   <rvc-folder>/assets/weights/NomeDaVoz.pth
   ```
3. O VexAI vai listar automaticamente todos os modelos presentes nessa pasta.

---

## Instalação do VexAI

Com todas as dependências instaladas, agora instale o próprio VexAI:

```bash
git clone https://github.com/seu-usuario/VexAI.git
cd VexAI
dotnet build
```

Para executar diretamente:

```bash
dotnet run
```

Ou para criar um executável compilado:

```bash
dotnet publish -c Release -r win-x64 --self-contained
```

O executável será gerado em `bin/Release/net8.0/win-x64/publish/VexAI.exe`.

---

## Configuração Inicial

Na **primeira execução**, o VexAI detecta automaticamente que não está configurado e abre o **assistente de configuração**. Você também pode rodar ele a qualquer momento com o comando `config`.

O assistente vai perguntar passo a passo:

| Pergunta | O que informar |
|---|---|
| **Pasta de saída** | Onde os arquivos gerados serão salvos. Ex: `C:\Users\Seu Nome\Documents\VexAI_Output` |
| **Pasta raiz do RVC** | Caminho da instalação do RVC. Ex: `C:\RVC-WebUI` |
| **Pasta do Deep-Live-Cam (Rápido)** | Caminho da instalação com DirectML. Ex: `C:\Deep-Live-Cam` |
| **Pasta do Deep-Live-Cam (Melhorado)** | Caminho da instalação com OpenVINO. *(Opcional — pressione Enter para pular)* |
| **Caminho do .bat do SD.Next** | Caminho completo do arquivo `.bat` que inicia o SD.Next. Ex: `C:\automatic\webui-user.bat` *(Opcional)* |
| **URL da API do SD.Next** | Deixe o padrão `http://127.0.0.1:7860` a menos que você use outra porta |
| **Imagem de marca d'água** | Caminho de um arquivo `.png` para sobrepor nas saídas. *(Opcional)* |
| **Auto-iniciar SD.Next?** | `s` para iniciar o SD.Next automaticamente ao abrir o VexAI |

Ao finalizar, as configurações são salvas no arquivo `config.json` na mesma pasta do executável.

---

## Comandos Disponíveis

Após a configuração, o VexAI entra no modo interativo. Digite um comando e pressione Enter.

```
VexAI> ajuda
```

---

### `gerar` — Gerar imagem por texto

Gera uma imagem a partir de uma descrição de texto (txt2img) usando o SD.Next.

**Pré-requisito:** SD.Next deve estar rodando em `http://127.0.0.1:7860`.

**Passos interativos:**
1. Digite o prompt descrevendo a imagem desejada
2. Escolha a qualidade: `sd (512x512)`, `hd (720p)` ou `fullhd (1080p)`
3. Responda se o conteúdo é +18 (libera/bloqueia filtros de conteúdo)

**Exemplo:**
```
VexAI> gerar
Prompt: a futuristic city at night, neon lights, cyberpunk
Qualidade: hd (720p)
Conteúdo +18? n
```

---

### `reimaginar` — Reimaginar imagem existente

Transforma uma imagem existente com base em um novo prompt (img2img).

**Pré-requisito:** SD.Next deve estar rodando.

**Passos interativos:**
1. Informe o caminho da imagem de entrada (`.jpg`, `.png`, etc.)
2. Digite o prompt de como quer reimaginar
3. Defina a força da mudança (0.1 = mudança leve, 1.0 = mudança total)
4. Responda se o conteúdo é +18

**Exemplo:**
```
VexAI> reimaginar
Caminho da imagem: C:\fotos\retrato.jpg
Prompt: oil painting, renaissance style
Força da mudança: 0.6
```

---

### `clonar` — Clonar rosto em cena

Gera imagens novas preservando a identidade do rosto de uma foto de referência, usando IP-Adapter.

**Pré-requisito:** SD.Next deve estar rodando com suporte a IP-Adapter.

**Passos interativos:**
1. Digite o prompt da cena/estilo desejado
2. Informe o caminho da foto com o rosto de referência

**Exemplo:**
```
VexAI> clonar
Prompt: astronaut in space, cinematic lighting
Caminho da foto do rosto: C:\fotos\meu_rosto.jpg
```

---

### `faceswap` — Trocar rosto em vídeo

Substitui o rosto em um vídeo pelo rosto de uma foto de referência.

**Pré-requisito:** Deep-Live-Cam instalado (modo rápido e/ou melhorado).

**Passos interativos:**
1. Informe o caminho do vídeo alvo
2. Informe o caminho da foto com o rosto que será inserido
3. Escolha o modo: `rápido (DirectML)` ou `melhorado (OpenVINO)`

**Exemplo:**
```
VexAI> faceswap
Caminho do vídeo: C:\videos\clipe.mp4
Caminho da foto do rosto: C:\fotos\rosto.jpg
Modo: rápido (DirectML)
```

---

### `videoart` — Arte em vídeo

Aplica um estilo artístico em cada frame do vídeo usando o SD.Next (img2img frame a frame).

**Pré-requisito:** SD.Next deve estar rodando. **Atenção:** vídeos longos podem demorar muito.

**Passos interativos:**
1. Informe o caminho do vídeo
2. Digite o prompt de estilo artístico desejado
3. Defina a força da mudança (0.1 a 1.0)

**Exemplo:**
```
VexAI> videoart
Caminho do vídeo: C:\videos\passeio.mp4
Prompt: watercolor painting, soft colors
Força: 0.5
```

---

### `voz` — Trocar voz em vídeo

Substitui a voz humana em um vídeo usando um modelo RVC. O processo é:
1. Extrai o áudio do vídeo
2. Separa a voz do instrumental usando UVR-MDX
3. Converte a voz com o modelo RVC selecionado
4. Mixa o resultado e renderiza o vídeo final

**Pré-requisito:** RVC instalado com ao menos um modelo `.pth` em `assets/weights/`.

**Passos interativos:**
1. Informe o caminho do vídeo
2. Escolha o modelo de voz disponível (listado automaticamente)
3. Defina o pitch (tom) — valores positivos aumentam, negativos diminuem

**Exemplo:**
```
VexAI> voz
Caminho do vídeo: C:\videos\entrevista.mp4
Modelo: MeuModelo
Pitch: 0
```

---

### `modelos` — Listar modelos RVC

Lista todos os modelos de voz `.pth` disponíveis na pasta `assets/weights/` do RVC.

```
VexAI> modelos
```

---

### `fila` — Status da fila

Exibe quantos jobs estão aguardando processamento e se há algum em execução no momento.

```
VexAI> fila
[Fila] Jobs pendentes: 2 | Processando: True
```

---

### `config` — Reconfigurar

Abre novamente o assistente de configuração para alterar qualquer caminho ou opção. As novas configurações são salvas imediatamente no `config.json`.

```
VexAI> config
```

---

## Modelos de Voz (RVC)

Para usar o comando `voz`, você precisa de modelos RVC treinados (arquivos `.pth`).

**Onde encontrar modelos:**
- [https://huggingface.co](https://huggingface.co) — pesquise por `rvc voice model`
- Comunidades no Discord e Reddit dedicadas ao RVC
- Você pode treinar seu próprio modelo com o RVC WebUI

**Como instalar um modelo:**
1. Baixe o arquivo `.pth` do modelo desejado
2. Copie para: `<rvc-folder>/assets/weights/NomeDaVoz.pth`
3. O VexAI detecta automaticamente na próxima vez que você rodar `modelos` ou `voz`

---

## Estrutura do config.json

O arquivo `config.json` é criado automaticamente na pasta do executável. Você pode editá-lo manualmente se preferir:

```json
{
  "SdNextUrl": "http://127.0.0.1:7860",
  "SdNextBatPath": "C:\\automatic\\webui-user.bat",
  "RvcFolderPath": "C:\\RVC-WebUI",
  "DeepLiveFastPath": "C:\\Deep-Live-Cam",
  "DeepLiveEnhancedPath": "C:\\Deep-Live-Cam-OpenVINO",
  "WatermarkImagePath": "C:\\logos\\marca.png",
  "OutputFolder": "C:\\Users\\Seu Nome\\Documents\\VexAI_Output",
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
| `WatermarkImagePath` | Imagem `.png` de marca d'água (opcional) |
| `OutputFolder` | Pasta onde todos os arquivos gerados são salvos |
| `AutoStartSdNext` | `true` para iniciar o SD.Next automaticamente ao abrir o VexAI |

---

## Perguntas Frequentes

**O SD.Next precisa estar aberto para usar o VexAI?**  
Sim, para os comandos `gerar`, `reimaginar`, `clonar` e `videoart`. Para `faceswap` e `voz`, o SD.Next não é necessário. Se você configurou o campo `SdNextBatPath` e ativou `AutoStartSdNext`, o VexAI tenta iniciar o SD.Next automaticamente.

**O VexAI funciona com GPU NVIDIA ou AMD?**  
Sim. A GPU Intel Arc é recomendada por usar DirectML e OpenVINO nativamente, mas o SD.Next e o Deep-Live-Cam têm suporte a CUDA (NVIDIA) e ROCm (AMD). Adapte as instalações de acordo com o guia de cada ferramenta.

**Onde ficam os arquivos gerados?**  
Tudo é salvo na pasta definida em `OutputFolder`. Dentro dela, o VexAI organiza em subpastas:
- `output/images/` — imagens geradas
- `output/audio/` — áudios intermediários de voz
- `output/video/` — vídeos finais de faceswap e voiceswap

**Como reconfiguro o VexAI após a instalação?**  
Use o comando `config` dentro do programa, ou edite manualmente o arquivo `config.json`.

**O processamento de vídeo está demorando muito.**  
O comando `videoart` processa frame a frame via SD.Next — é esperado que vídeos longos demorem bastante. Prefira vídeos curtos (menos de 30 segundos) para testes iniciais.

---

## Licença

MIT
