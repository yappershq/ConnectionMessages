namespace ConnectionMessages.Shared;

/// <summary>
/// Cross-plugin API for ConnectionMessages.
/// Consumers resolve this in OnAllModulesLoaded via ISharpModuleManager.
/// </summary>
public interface IConnectionMessagesShared
{
    public const string Identity = nameof(IConnectionMessagesShared);

    /// <summary>
    /// Permanently silence (or un-silence) join/leave announcements for the given slot.
    /// Transient — cleared on disconnect. Thread-safe (slot-indexed array).
    /// </summary>
    void SetSilent(int slot, bool silent);

    /// <summary>
    /// Whether the slot is currently silenced.
    /// </summary>
    bool IsSilent(int slot);

    /// <summary>
    /// Suppress only the next join announcement for this slot (one-shot, consumed on fire).
    /// Use SetSilent for persistent silence.
    /// </summary>
    void SuppressNextJoin(int slot);

    /// <summary>
    /// Suppress only the next leave announcement for this slot (one-shot, consumed on fire).
    /// </summary>
    void SuppressNextLeave(int slot);

    // === Extension seam for future VIP / fancy-message override ===
    // A future plugin registers a handler with priority; ConnectionMessages calls the highest-
    // priority non-null result instead of the default template.
    // Uncomment + implement when VIP plugin is built:
    // void RegisterJoinOverride(int priority, Func<int /*slot*/, string?> handler);
    // void UnregisterJoinOverride(int priority);
}
