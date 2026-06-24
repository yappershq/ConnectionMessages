<div align="center">
  <h1><strong>ConnectionMessages</strong></h1>
  <p>Customizable join/leave chat announcements for CS2 ModSharp servers, with a cross-plugin suppression API.</p>
</div>

<p align="center">
  <img src="https://img.shields.io/github/stars/yappershq/ConnectionMessages?style=flat&logo=github" alt="Stars">
</p>

---

ConnectionMessages replaces the engine's default join/disconnect text with your own templated chat messages. Templates support `{name}` (and `{reason}` on leave) plus color tokens, and the plugin can suppress the engine's built-in announcements so only yours show. It also publishes a small `.Shared` API so other plugins can silence announcements per-player (e.g. stealth/VIP joins).

## 🚀 Install

Copy the build output into your ModSharp install (`<sharp>` = your `sharp` directory):

| From | To |
|------|----|
| `.build/modules/ConnectionMessages/` | `<sharp>/modules/ConnectionMessages/` |
| `.build/shared/ConnectionMessages.Shared/` | `<sharp>/shared/ConnectionMessages.Shared/` |

Restart the server (or change map) to load. LocalizerManager and AdminManager are looked up optionally if present (both ship with ModSharp) — neither is required.

## ⚙️ Configuration

ConVars are written to `<sharp>/configs/connectionmessages.cfg`, auto-generated on first run:

| ConVar | Default | Meaning |
|--------|---------|---------|
| `cm_enabled` | `1` | Enable ConnectionMessages [0=off, 1=on] |
| `cm_show_join` | `1` | Show join announcements |
| `cm_show_leave` | `1` | Show leave announcements |
| `cm_join_template` | `" {green}{name}{default} joined the server."` | Join message template. `{name}` = player name; `{green}`/`{default}` etc. = color codes |
| `cm_leave_template` | `" {default}{name} left the server."` | Leave message template. Supports `{name}` and `{reason}` |
| `cm_suppress_engine` | `1` | Suppress the engine's built-in join/leave text [0=off, 1=on] |

Supported color tokens: `{default}`/`{white}`, `{darkred}`, `{purple}`, `{green}`, `{olive}`, `{lime}`, `{red}`, `{grey}`/`{gray}`, `{yellow}`, `{silver}`, `{blue}`, `{darkblue}`, `{pink}`, `{lightred}`.

## 🔧 How it works

Join messages fire on the `player_connect_full` game event (the moment the player is fully in-game); leave messages fire on `player_disconnect`, reading the name and reason straight off the event. When `cm_suppress_engine` is on, a pre-hook on the engine `TextMsg` net message drops the default `connected`/`disconnected` text so only your templated message is printed. Fake clients and HLTV are ignored.

## 🧩 Public API

Other plugins consume `IConnectionMessagesShared` (resolve in `OnAllModulesLoaded`):

```csharp
var api = sharpModuleManager
    .GetOptionalSharpModuleInterface<IConnectionMessagesShared>(IConnectionMessagesShared.Identity)?.Instance;

api?.SetSilent(slot, true);   // persistently mute this slot's announcements
api?.SuppressNextJoin(slot);  // one-shot: skip only the next join message
api?.SuppressNextLeave(slot); // one-shot: skip only the next leave message
bool muted = api?.IsSilent(slot) ?? false;
```

Per-slot state is cleared automatically on disconnect.

## 📦 Build

```bash
dotnet build -c Release
```

Outputs `.build/modules/ConnectionMessages/ConnectionMessages.dll` and `.build/shared/ConnectionMessages.Shared/ConnectionMessages.Shared.dll`.

---

<div align="center">
  <p>Made with ❤️ by <a href="https://github.com/yappershq">yappershq</a></p>
  <p>⭐ Star this repo if you find it useful!</p>
</div>
