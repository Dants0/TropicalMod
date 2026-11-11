# TropicalMod (GTA V)

Mod single-player em C# (ScriptHookVDotNet 3 + LemonUI). Meta: lançar até 19/11/2026, documentado em devlog.

## Runtime no jogo (fazer uma vez, depois que o GTA V terminar de instalar)
Pasta do jogo: `D:\GTAV\GTAV` (Legacy 1.0.3889.0) (se for outra, ajuste `GtaPath` no .csproj).

1. **ScriptHookV** (dev-c.com) → copiar `ScriptHookV.dll` e `dinput8.dll` para a pasta do jogo.
   A versão precisa bater com a versão do GTA; após update do jogo, esperar update do ScriptHookV.
2. **ScriptHookVDotNet** (GitHub releases) → copiar `ScriptHookVDotNet.asi`, `ScriptHookVDotNet2.dll`,
   `ScriptHookVDotNet3.dll` e `ScriptHookVDotNet.ini` para a pasta do jogo.
3. Criar a pasta `scripts` dentro da pasta do jogo.
4. Nunca abrir o GTA Online com isso instalado.

## Desenvolvimento
```
dotnet build -c Release
```
O build copia `TropicalMod.dll` + `LemonUI.SHVDN3.dll` para `GTAV\scripts`.
Com o jogo aberto, **Insert** recarrega os scripts (sem reiniciar o GTA).

- **F5**: menu de módulos
- **F6**: recrutar parceiro (NPC mais próximo, ou chama um) / dispensar
- **Shift+F6**: debug — força lealdade 10 para testar a traição
- Log: `GTAV\scripts\TropicalMod.log` (e `ScriptHookVDotNet.log` na pasta do jogo)

## Estrutura
- `src/Main.cs` — entrada, menu e loop; registre módulos novos na lista `_modules`
- `src/Core/ModuleBase.cs` — base de cada mecânica (OnStart/OnTick/OnStop)
- `src/Modules/` — uma mecânica por arquivo
