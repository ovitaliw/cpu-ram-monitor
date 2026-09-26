# CPU/RAM Monitor

Widget translúcido, sempre no topo, para acompanhar CPU e RAM no Windows 11 e matar quem está pesando.

- **Compacto (100×100):** barras de CPU e RAM (ficam vermelhas acima de 85%).
- **Duplo clique** expande/comprime · **clique e arraste** move · `—` comprime · `✕` fecha.
- **Expandido:** anéis de CPU/RAM + top 5 processos por CPU e por RAM, agrupados por nome (`node (12)`).
  - `✕` numa linha: 1º clique arma (fica vermelho por 3 s), 2º clique encerra **todos** os processos com esse nome (e seus filhos).
  - `vmmemWSL` (WSL2, Docker Desktop, k3d): o `✕` executa `wsl --shutdown`.
  - Clique direito numa linha → **Ocultar da lista**.
- Slider de opacidade embaixo.

## Processos ocultos

`%LOCALAPPDATA%\CpuRamMonitor\exclusions.txt` (botão `⚙` abre o arquivo). Um nome por linha, sem diferenciar maiúsculas, `.exe` opcional, `*` é curinga, `#` é comentário. O widget recarrega sozinho ao salvar.

```
# Navegador padrão (Opera GX)
opera
docker*
```

Posição, modo e opacidade ficam em `%LOCALAPPDATA%\CpuRamMonitor\settings.json`.

## Build

Requer .NET 8 SDK.

```bash
dotnet test
dotnet run --project src/CpuRamMonitor
dotnet publish src/CpuRamMonitor -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

## Iniciar com o Windows

Ligado por padrão: checkbox **Iniciar com o Windows** no modo expandido (ou `"startWithWindows": false` no `settings.json`).
Usa a entrada `CpuRamMonitor` em `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (não precisa de admin; aparece em Gerenciador de Tarefas → Aplicativos de inicialização).
O caminho é regravado a cada execução, então vale o **último exe aberto**. Rode o `publish\CpuRamMonitor.exe` uma vez depois de testar com `dotnet run`.

## Observações

- A amostragem usa uma única chamada `NtQuerySystemInformation` a cada 1,5 s (sem abrir handle por processo), e o widget roda com prioridade *AboveNormal* para continuar respondendo com a máquina engasgada.
- A RAM de `vmmemWSL` mostrada é o working set privado do processo; a memória real da VM do WSL pode ser maior.
- Processos críticos do Windows (`explorer`, `dwm`, `csrss`, `svchost`…) e processos de outras sessões nunca aparecem na lista.
