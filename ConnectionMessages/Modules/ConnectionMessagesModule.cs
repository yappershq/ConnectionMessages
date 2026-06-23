using System;
using System.Threading;
using ConnectionMessages.Configuration;
using ConnectionMessages.Shared;
using Sharp.Extensions.GameEventManager;
using Sharp.Shared.Enums;
using Sharp.Shared.HookParams;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using Sharp.Shared.Units;
using Microsoft.Extensions.Logging;

namespace ConnectionMessages.Modules;

internal sealed class ConnectionMessagesModule
    : IModule, IConnectionMessagesShared
{
    // Per-slot flags — slot = PlayerSlot byte (0–63)
    private readonly bool[] _silent            = new bool[64];
    private readonly bool[] _suppressNextJoin  = new bool[64];
    private readonly bool[] _suppressNextLeave = new bool[64];

    private readonly InterfaceBridge                   _bridge;
    private readonly ILogger<ConnectionMessagesModule> _logger;
    private readonly IConnectionMessagesConfig         _config;
    private readonly IGameEventManager                 _gameEventManager;

    // TextMsg hook delegate kept alive
    private Func<ITextMsgHookParams, HookReturnValue<NetworkReceiver>, HookReturnValue<NetworkReceiver>>? _textMsgHook;

    public ConnectionMessagesModule(
        InterfaceBridge                   bridge,
        ILogger<ConnectionMessagesModule> logger,
        IConnectionMessagesConfig         config,
        IGameEventManager                 gameEventManager)
    {
        _bridge           = bridge;
        _logger           = logger;
        _config           = config;
        _gameEventManager = gameEventManager;
    }

    // ===== IConnectionMessagesShared =====

    public void SetSilent(int slot, bool silent)
    {
        if ((uint)slot >= 64) return;
        Volatile.Write(ref _silent[slot], silent);
    }

    public bool IsSilent(int slot)
        => (uint)slot < 64 && Volatile.Read(ref _silent[slot]);

    public void SuppressNextJoin(int slot)
    {
        if ((uint)slot >= 64) return;
        Volatile.Write(ref _suppressNextJoin[slot], true);
    }

    public void SuppressNextLeave(int slot)
    {
        if ((uint)slot >= 64) return;
        Volatile.Write(ref _suppressNextLeave[slot], true);
    }

    // ===== IModule =====

    public bool Init()
    {
        // Trigger join/leave from game events instead of IClientListener.
        // player_connect_full fires when the player is fully in-game (correct "joined" moment).
        // player_disconnect carries the disconnect reason/name.
        _gameEventManager.ListenEvent("player_connect_full", OnPlayerConnectFull);
        _gameEventManager.ListenEvent("player_disconnect",   OnPlayerDisconnect);

        if (_config.SuppressEngine)
        {
            _textMsgHook = OnTextMsgPre;
            _bridge.HookManager.TextMsg.InstallHookPre(_textMsgHook);
        }

        return true;
    }

    public void OnPostInit()
    {
        // Publish own interface so consumers (e.g. Sleuth) can resolve it in OAM.
        // PostInit is the correct timing for publishers — ModSharp guarantees all PostInits
        // complete before any OnAllModulesLoaded fires.
        _bridge.SharpModuleManager.RegisterSharpModuleInterface<IConnectionMessagesShared>(
            _bridge.Plugin, IConnectionMessagesShared.Identity, this);
        _logger.LogInformation("[ConnectionMessages] Registered IConnectionMessagesShared ({Id})", IConnectionMessagesShared.Identity);
    }

    public void Shutdown()
    {
        if (_textMsgHook is not null)
        {
            _bridge.HookManager.TextMsg.RemoveHookPre(_textMsgHook);
            _textMsgHook = null;
        }
    }

    // ===== TextMsg hook — suppress engine default join/leave =====

    private HookReturnValue<NetworkReceiver> OnTextMsgPre(
        ITextMsgHookParams params_,
        HookReturnValue<NetworkReceiver> ret)
    {
        if (!_config.Enabled || !_config.SuppressEngine)
            return ret;

        // Engine join/leave text message tokens contain "onnected" (connected/disconnected).
        // Matching case-insensitively to catch all variants.
        var name = params_.Name;
        if (name.Contains("onnected", StringComparison.OrdinalIgnoreCase))
        {
            // Return an empty NetworkReceiver — suppresses delivery to all clients.
            return new HookReturnValue<NetworkReceiver>(EHookAction.SkipCallReturnOverride, default);
        }

        return ret;
    }

    // ===== Game event listeners — join / leave triggers =====

    private void OnPlayerConnectFull(IGameEvent e)
    {
        // Resolve client from the event's userid.
        var client = _bridge.ClientManager.GetGameClient(new UserID((ushort)e.GetInt("userid")));
        if (client is null || client.IsFakeClient || client.IsHltv)
            return;

        if (!_config.Enabled || !_config.ShowJoin)
            return;

        var slot = (int)(byte)client.Slot;

        // One-shot suppression
        if (Volatile.Read(ref _suppressNextJoin[slot]))
        {
            Volatile.Write(ref _suppressNextJoin[slot], false);
            return;
        }

        // Persistent silence
        if (Volatile.Read(ref _silent[slot]))
            return;

        var msg = BuildMessage(_config.JoinTemplate, client.Name ?? "Unknown", string.Empty);
        _bridge.ModSharp.PrintToChatAll(msg);
    }

    private void OnPlayerDisconnect(IGameEvent e)
    {
        // Controller may already be torn down at disconnect — resolve via userid through ClientManager.
        var client = _bridge.ClientManager.GetGameClient(new UserID((ushort)e.GetInt("userid")));

        // Event still carries name/reason even when the client is gone — read them straight off the event.
        var name   = e.GetString("name");
        var reason = ((NetworkDisconnectionReason)e.GetInt("reason")).ToString();

        var isFake = client?.IsFakeClient ?? false;
        var isHltv = client?.IsHltv ?? false;
        if (string.IsNullOrEmpty(name))
            name = client?.Name ?? "Unknown";

        if (!isFake && !isHltv && _config.Enabled && _config.ShowLeave)
        {
            // If we couldn't resolve the client we have no slot — fall back to printing
            // (no per-slot flags to consult/clear in that case).
            if (client is not null)
            {
                var slot = (int)(byte)client.Slot;

                if (Volatile.Read(ref _suppressNextLeave[slot]))
                {
                    Volatile.Write(ref _suppressNextLeave[slot], false);
                }
                else if (!Volatile.Read(ref _silent[slot]))
                {
                    var msg = BuildMessage(_config.LeaveTemplate, name, reason);
                    _bridge.ModSharp.PrintToChatAll(msg);
                }

                // Always clear state on disconnect
                _silent[slot]            = false;
                _suppressNextJoin[slot]  = false;
                _suppressNextLeave[slot] = false;
            }
            else
            {
                var msg = BuildMessage(_config.LeaveTemplate, name, reason);
                _bridge.ModSharp.PrintToChatAll(msg);
            }
        }
        else if (client is not null)
        {
            // Even when not printing, clear per-slot state for the freed slot.
            var slot = (int)(byte)client.Slot;
            _silent[slot]            = false;
            _suppressNextJoin[slot]  = false;
            _suppressNextLeave[slot] = false;
        }
    }

    // ===== Helpers =====

    private static string BuildMessage(string template, string playerName, string reason)
    {
        // Replace placeholders BEFORE color processing so braces don't interfere.
        var raw = template
            .Replace("{name}", playerName, StringComparison.Ordinal)
            .Replace("{reason}", reason, StringComparison.Ordinal);
        return ProcessColorCodes(raw);
    }

    /// <summary>
    /// Converts {color} tokens to engine chat escape sequences.
    /// </summary>
    private static string ProcessColorCodes(string input)
    {
        return input
            .Replace("{default}", "\x01", StringComparison.Ordinal)
            .Replace("{white}",   "\x01", StringComparison.Ordinal)
            .Replace("{darkred}", "\x02", StringComparison.Ordinal)
            .Replace("{purple}",  "\x03", StringComparison.Ordinal)
            .Replace("{green}",   "\x04", StringComparison.Ordinal)
            .Replace("{olive}",   "\x05", StringComparison.Ordinal)
            .Replace("{lime}",    "\x06", StringComparison.Ordinal)
            .Replace("{red}",     "\x07", StringComparison.Ordinal)
            .Replace("{grey}",    "\x08", StringComparison.Ordinal)
            .Replace("{gray}",    "\x08", StringComparison.Ordinal)
            .Replace("{yellow}",  "\x09", StringComparison.Ordinal)
            .Replace("{silver}",  "\x0A", StringComparison.Ordinal)
            .Replace("{blue}",    "\x0B", StringComparison.Ordinal)
            .Replace("{darkblue}", "\x0C", StringComparison.Ordinal)
            .Replace("{pink}",    "\x0E", StringComparison.Ordinal)
            .Replace("{lightred}", "\x0F", StringComparison.Ordinal);
    }
}
