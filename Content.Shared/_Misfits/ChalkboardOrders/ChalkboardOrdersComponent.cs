using System;
using System.Collections.Generic;
using Content.Shared._Misfits.WastelandMap;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Misfits.ChalkboardOrders;

/// <summary>
/// UI key for the chalkboard orders interface.
/// </summary>
[Serializable, NetSerializable]
public enum ChalkboardOrdersUiKey : byte
{
    Key,
}

/// <summary>
/// BUI state for a writable chalkboard orders board.
/// </summary>
[Serializable, NetSerializable]
public sealed class ChalkboardOrdersBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly string BoardTitle;
    public readonly string BoardTexturePath;
    public readonly WastelandMapAnnotation[] SharedAnnotations;

    public ChalkboardOrdersBoundUserInterfaceState(
        string boardTitle,
        string boardTexturePath,
        WastelandMapAnnotation[]? sharedAnnotations = null)
    {
        BoardTitle = boardTitle;
        BoardTexturePath = boardTexturePath;
        SharedAnnotations = sharedAnnotations ?? [];
    }
}

/// <summary>
/// Message sent from client to add an order annotation to the board.
/// </summary>
[Serializable, NetSerializable]
public sealed class ChalkboardOrdersAddAnnotationMessage : BoundUserInterfaceMessage
{
    public readonly WastelandMapAnnotation Annotation;

    public ChalkboardOrdersAddAnnotationMessage(WastelandMapAnnotation annotation)
    {
        Annotation = annotation;
    }
}

/// <summary>
/// Message sent from client to remove one annotation index from the board.
/// </summary>
[Serializable, NetSerializable]
public sealed class ChalkboardOrdersRemoveAnnotationMessage : BoundUserInterfaceMessage
{
    public readonly int Index;

    public ChalkboardOrdersRemoveAnnotationMessage(int index)
    {
        Index = index;
    }
}

/// <summary>
/// Message sent from client to clear all order annotations.
/// </summary>
[Serializable, NetSerializable]
public sealed class ChalkboardOrdersClearAnnotationsMessage : BoundUserInterfaceMessage;

/// <summary>
/// Stores persistent, shared chalkboard order annotations on the entity.
/// Any mapped entity with this component can open the orders UI.
/// </summary>
[RegisterComponent]
public sealed partial class ChalkboardOrdersComponent : Component
{
    /// <summary>
    /// Texture shown behind annotations in the board UI, relative to /Textures.
    /// </summary>
    [DataField]
    public ResPath BoardTexturePath = new("_Nuclear14/Structures/Wallmounts/chalkboardwall.rsi/board_clean.png");

    /// <summary>
    /// Window title shown to users when opening the board.
    /// </summary>
    [DataField]
    public string BoardTitle = "Orders Board";

    /// <summary>
    /// Shared draw/text annotations for the board.
    /// </summary>
    [DataField]
    public List<WastelandMapAnnotation> SharedAnnotations = new();
}
