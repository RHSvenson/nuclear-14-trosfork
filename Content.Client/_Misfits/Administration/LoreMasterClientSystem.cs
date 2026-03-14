// #Misfits Change /Add/ - Client-side system for the LoreMaster admin tab.
// Receives network responses from LoreMasterSystem (server) and exposes them as
// plain C# events so the UI tab can listen without touching the entity event bus at runtime.
using Content.Shared._Misfits.Administration;

namespace Content.Client._Misfits.Administration;

/// <summary>
/// Client-side EntitySystem that bridges LoreMaster network messages to the UI tab.
/// The entity event bus is fully locked after Initialize, so subscriptions must live here,
/// not inside the Control subclass.
/// </summary>
public sealed class LoreMasterClientSystem : EntitySystem
{
    /// <summary>Fired when the server sends back a faction info snapshot.</summary>
    public event Action<LoreMasterFactionInfoEvent>? OnFactionInfoReceived;

    /// <summary>Fired when the server responds to an objective-issuance request.</summary>
    public event Action<LoreMasterObjectiveResultEvent>? OnObjectiveResultReceived;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<LoreMasterFactionInfoEvent>(OnFactionInfo);
        SubscribeNetworkEvent<LoreMasterObjectiveResultEvent>(OnObjectiveResult);
    }

    /// <summary>Request an updated faction member/objective snapshot from the server.</summary>
    public void RequestFactionInfo(string factionId)
    {
        RaiseNetworkEvent(new RequestLoreMasterFactionInfoEvent { FactionId = factionId });
    }

    /// <summary>Ask the server to issue an objective to the highest-ranking online faction member.</summary>
    public void IssueObjective(string factionId, string objectivePrototype)
    {
        RaiseNetworkEvent(new IssueLoreMasterObjectiveEvent
        {
            FactionId = factionId,
            ObjectivePrototype = objectivePrototype,
        });
    }

    /// <summary>
    /// Ask the server to issue a fully custom (admin-typed) objective to the highest-ranking
    /// online faction member. No prototype is involved — title and description are freeform.
    /// </summary>
    // #Misfits Add - custom order support
    public void IssueCustomObjective(string factionId, string customTitle, string customDescription)
    {
        RaiseNetworkEvent(new IssueCustomLoreMasterObjectiveEvent
        {
            FactionId = factionId,
            CustomTitle = customTitle,
            CustomDescription = customDescription,
        });
    }

    private void OnFactionInfo(LoreMasterFactionInfoEvent msg)
    {
        OnFactionInfoReceived?.Invoke(msg);
    }

    private void OnObjectiveResult(LoreMasterObjectiveResultEvent msg)
    {
        OnObjectiveResultReceived?.Invoke(msg);
    }
}
