<div align="center">

<img src="Assets/icon.png" width="72" alt="LG AC Controller icon">

# LG AC Controller

**Control your LG air conditioner from the Windows system tray.**
**Controle seu ar-condicionado LG pela bandeja do Windows.**

![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?logo=windows)
![.NET](https://img.shields.io/badge/.NET-8-512BD4?logo=dotnet)
![WPF](https://img.shields.io/badge/UI-WPF-68217A)
![API](https://img.shields.io/badge/API-LG%20ThinQ%20Connect-A50034)

[English](#english) · [Português](#português)

</div>

> [!IMPORTANT]
> Unofficial project, not affiliated with or endorsed by LG Electronics. "LG" and "ThinQ" are trademarks of LG Electronics.
> Projeto não oficial, sem ligação com a LG Electronics. "LG" e "ThinQ" são marcas da LG Electronics.

---

## English

A lightweight tray app that talks to your air conditioner through the **official LG ThinQ Connect API**. No reverse engineering and no phone needed: click the snowflake next to the clock and adjust the room.

### Features

- **Power** on/off
- **Temperature** from 18 to 30 °C in 0.5 °C steps, with a slider or `–` / `+` buttons. Commands are sent once you stop adjusting, not on every step.
- **Modes**: Cool, Heat, Auto, Fan, Dry
- **Fan speed**: Auto, Low, Medium, High
- **Swing** (vertical and horizontal), **natural wind** and **energy saving**
- **Settings panel**: display light, sleep timer (30 min to 4 h), scheduled on/off times and filter usage hours
- **Live tray icon**: its color follows the current mode and turns gray when the AC is off. Hover it for a summary such as *On · Cool · 23 °C*.
- **Stays in sync**: polls the device every 30 s and right after each command, so changes from the remote or the ThinQ phone app show up too
- **Starts with Windows** (optional) straight into the tray, single instance
- **Demo mode** when no token is configured, so you can try the UI without a device

### Requirements

- Windows 10 or 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- An LG air conditioner with Wi-Fi, already working in the **LG ThinQ** phone app

### Setup

**1. Create a Personal Access Token (PAT)**

1. Go to **https://connect-pat.lgthinq.com** and sign in with the same account you use in the ThinQ app.
2. Click **Create New Token** and **select every permission**. Without the device permissions the API returns error `1309`.
3. Copy the token. It is shown only once.

**2. Store it as a user environment variable.** Never put it in the code.

```bash
setx LG_THINQ_PAT "your_token_here"
```

**3. Run**

```bash
git clone https://github.com/GuilhermeSoranzo/lg-ac-controller.git
cd lg-ac-controller
dotnet run
```

The status bar should change from *Conectando à LG ThinQ…* to *Conectado*. The UI is in Portuguese.

### Install and start with Windows

Publish a standalone build (PowerShell):

```bash
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o "$env:LOCALAPPDATA\Programs\LGClimaTray"
```

Open `LGClimaTray.exe` from that folder, then right-click the tray icon and check **Iniciar com o Windows** (*Start with Windows*). At login the app starts hidden in the tray.

### Region

The app currently targets **Brazil**: country `BR` on the Americas server `api-aic.lgthinq.com`. For other countries, change `x-country` and the base URL in [`Services/LgThinQAirConditionerService.cs`](Services/LgThinQAirConditionerService.cs). The three servers are `api-aic` (Americas), `api-eic` (Europe/Middle East/Africa) and `api-kic` (Asia/Pacific).

### How it works

| What | Endpoint |
|---|---|
| Find the AC | `GET /devices` |
| Read state | `GET /devices/{id}/state` |
| Send a command | `POST /devices/{id}/control` |

Commands are small JSON payloads, for example `{"operation":{"airConOperationMode":"POWER_ON"}}`. The UI depends only on `IAirConditionerService`, so the real service and the demo one are interchangeable.

```
Services/
  AirConditionerState.cs           state model, interface and demo service
  LgThinQAirConditionerService.cs  LG ThinQ Connect client
MainWindow.xaml(.cs)               control panel
TrayController.cs / TrayIcon.cs    tray icon and menu
StartupRegistration.cs             start with Windows (HKCU Run key)
```

### Troubleshooting

| Symptom | Fix |
|---|---|
| *Modo de demonstração* (demo mode) shown | `LG_THINQ_PAT` is not set. Run `setx` and reopen the app. |
| Error `1309`: *not allowed* | The token was created without device permissions. Create a new token with all permissions selected. |
| Error `1310`: *not supported domain* | Wrong region. See [Region](#region). |
| A command is rejected while the AC is off | Some models only accept mode changes while on. Turn the AC on first. |

### Security

- The token is read from the `LG_THINQ_PAT` environment variable and is never stored in the repository.
- Anyone holding your token can see and control your LG devices. If it leaks, revoke it at connect-pat.lgthinq.com.
- The `x-api-key` value in the code is a public constant from [LG's official SDK](https://github.com/thinq-connect/pythinqconnect). It is not a secret.

### Tested with

LG Split Hi Wall Dual Inverter Voice `S4NW12JA3XB` (ThinQ model `RAC_056905_WW`). Other LG ACs should work, as long as they support the features above.

---

## Português

Um app leve de bandeja que conversa com o seu ar-condicionado pela **API oficial LG ThinQ Connect**. Nada de engenharia reversa e sem precisar do celular: clique no floco de neve ao lado do relógio e ajuste o ambiente.

### Funcionalidades

- **Ligar/desligar**
- **Temperatura** de 18 a 30 °C, de 0,5 em 0,5 °C, pelo slider ou pelos botões `–` / `+`. O comando só é enviado quando você para de ajustar, e não a cada passo.
- **Modos**: Frio, Quente, Auto, Ventilar, Secar
- **Velocidade do ventilador**: Auto, Baixo, Médio, Alto
- **Oscilação** (vertical e lateral), **vento natural** e **economia de energia**
- **Painel de ajustes**: luz do display, sleep (30 min a 4 h), horário programado para ligar e desligar, e horas de uso do filtro
- **Ícone vivo na bandeja**: a cor acompanha o modo e fica cinza com o aparelho desligado. Passe o mouse para ver um resumo como *Ligado · Frio · 23°C*.
- **Sempre sincronizado**: lê o aparelho a cada 30 s e logo após cada comando, então mudanças feitas pelo controle remoto ou pelo app ThinQ também aparecem
- **Inicia com o Windows** (opcional) direto na bandeja, com instância única
- **Modo demonstração** quando não há token configurado, para testar a interface sem aparelho

### Requisitos

- Windows 10 ou 11
- [SDK do .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0)
- Ar-condicionado LG com Wi-Fi, já funcionando no app **LG ThinQ** do celular

### Configuração

**1. Gere um token pessoal (PAT)**

1. Acesse **https://connect-pat.lgthinq.com** e entre com a mesma conta do app ThinQ.
2. Clique em **Create New Token** e **marque todas as permissões**. Sem as permissões de aparelho, a API responde com o erro `1309`.
3. Copie o token. Ele aparece uma única vez.

**2. Salve como variável de ambiente do usuário.** Nunca coloque o token no código.

```bash
setx LG_THINQ_PAT "seu_token_aqui"
```

**3. Execute**

```bash
git clone https://github.com/GuilhermeSoranzo/lg-ac-controller.git
cd lg-ac-controller
dotnet run
```

A barra de status deve mudar de *Conectando à LG ThinQ…* para *Conectado*.

### Instalar e iniciar com o Windows

Publique uma versão para uso diário (PowerShell):

```bash
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o "$env:LOCALAPPDATA\Programs\LGClimaTray"
```

Abra o `LGClimaTray.exe` dessa pasta, clique com o botão direito no ícone da bandeja e marque **Iniciar com o Windows**. No login, o app abre escondido na bandeja.

### Região

Hoje o app está configurado para o **Brasil**: país `BR`, no servidor das Américas `api-aic.lgthinq.com`. Para outros países, altere o `x-country` e a URL base em [`Services/LgThinQAirConditionerService.cs`](Services/LgThinQAirConditionerService.cs). Os três servidores são `api-aic` (Américas), `api-eic` (Europa, Oriente Médio e África) e `api-kic` (Ásia e Pacífico).

### Como funciona

| O quê | Endpoint |
|---|---|
| Achar o ar-condicionado | `GET /devices` |
| Ler o estado | `GET /devices/{id}/state` |
| Enviar comando | `POST /devices/{id}/control` |

Os comandos são JSONs pequenos, por exemplo `{"operation":{"airConOperationMode":"POWER_ON"}}`. A interface depende só de `IAirConditionerService`, então o serviço real e o de demonstração são intercambiáveis.

### Problemas comuns

| Sintoma | Solução |
|---|---|
| Aparece *Modo de demonstração* | A variável `LG_THINQ_PAT` não está definida. Rode o `setx` e reabra o app. |
| Erro `1309`: *sem permissão* | O token foi criado sem as permissões de aparelho. Gere outro marcando todas. |
| Erro `1310`: *domínio não suportado* | Região errada. Veja [Região](#região). |
| Comando recusado com o aparelho desligado | Alguns modelos só aceitam troca de modo ligados. Ligue o aparelho antes. |

### Segurança

- O token é lido da variável de ambiente `LG_THINQ_PAT` e nunca fica no repositório.
- Quem tiver o seu token consegue ver e controlar seus aparelhos LG. Se ele vazar, revogue em connect-pat.lgthinq.com.
- O valor de `x-api-key` no código é uma constante pública do [SDK oficial da LG](https://github.com/thinq-connect/pythinqconnect). Não é segredo.

### Testado com

LG Split Hi Wall Dual Inverter Voice `S4NW12JA3XB` (modelo ThinQ `RAC_056905_WW`). Outros aparelhos LG devem funcionar, desde que tenham os recursos acima.
