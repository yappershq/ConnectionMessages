using Microsoft.Extensions.Logging;
using Sharp.Shared.Objects;

namespace ConnectionMessages.Configuration;

internal interface IConnectionMessagesConfig
{
    bool   Enabled        { get; }
    bool   ShowJoin       { get; }
    bool   ShowLeave      { get; }
    /// <summary>
    /// Template for join message. Supports {name} placeholder and {color} codes.
    /// Use {{green}} etc. in the cfg file so string.Format passes them through.
    /// </summary>
    string JoinTemplate   { get; }
    string LeaveTemplate  { get; }
    /// <summary>Suppress the engine's built-in join/leave TextMsg.</summary>
    bool   SuppressEngine { get; }
}

internal sealed class ConnectionMessagesConfig : IConnectionMessagesConfig
{
    private readonly IConVar? _cvEnabled;
    private readonly IConVar? _cvShowJoin;
    private readonly IConVar? _cvShowLeave;
    private readonly IConVar? _cvJoinTemplate;
    private readonly IConVar? _cvLeaveTemplate;
    private readonly IConVar? _cvSuppressEngine;

    public ConnectionMessagesConfig(InterfaceBridge bridge)
    {
        var cv = bridge.ConVarManager;

        _cvEnabled        = cv.CreateConVar("cm_enabled",         true,  "Enable ConnectionMessages [0=off, 1=on]");
        _cvShowJoin       = cv.CreateConVar("cm_show_join",       true,  "Show join announcements");
        _cvShowLeave      = cv.CreateConVar("cm_show_leave",      true,  "Show leave announcements");
        _cvJoinTemplate   = cv.CreateConVar("cm_join_template",   " {green}{name}{default} joined the server.",
                                                                          "Join message template. {name}=player name. Use {green}/{default} for color codes.");
        _cvLeaveTemplate  = cv.CreateConVar("cm_leave_template",  " {default}{name} left the server.",
                                                                          "Leave message template. {name}=player name.");
        _cvSuppressEngine = cv.CreateConVar("cm_suppress_engine", true,  "Suppress engine default join/leave text [0=off, 1=on]");

        var logger = bridge.LoggerFactory.CreateLogger("ConnectionMessages.Config");
        IConVar?[] all = [_cvEnabled, _cvShowJoin, _cvShowLeave, _cvJoinTemplate, _cvLeaveTemplate, _cvSuppressEngine];
        ConVarConfigFile.Sync(bridge.SharpPath, "connectionmessages.cfg", "ConnectionMessages", logger,
            System.Array.FindAll(all, c => c is not null)!);
    }

    public bool   Enabled        => _cvEnabled?.GetBool()         ?? true;
    public bool   ShowJoin       => _cvShowJoin?.GetBool()        ?? true;
    public bool   ShowLeave      => _cvShowLeave?.GetBool()       ?? true;
    public string JoinTemplate   => _cvJoinTemplate?.GetString()  ?? " {name} joined the server.";
    public string LeaveTemplate  => _cvLeaveTemplate?.GetString() ?? " {name} left the server.";
    public bool   SuppressEngine => _cvSuppressEngine?.GetBool()  ?? true;
}
