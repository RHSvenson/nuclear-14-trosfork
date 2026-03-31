using System;
using Content.Shared._Misfits.ChalkboardOrders;
using Content.Shared._Misfits.WastelandMap;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;

namespace Content.Server._Misfits.ChalkboardOrders;

/// <summary>
/// Handles synchronized write/draw orders on chalkboard entities.
/// </summary>
public sealed class ChalkboardOrdersSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;

    private const int MaxSharedAnnotations = 128;
    private const int MaxStrokePoints = 512;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChalkboardOrdersComponent, AfterActivatableUIOpenEvent>(OnAfterOpen);
        SubscribeLocalEvent<ChalkboardOrdersComponent, ChalkboardOrdersAddAnnotationMessage>(OnAddAnnotationMessage);
        SubscribeLocalEvent<ChalkboardOrdersComponent, ChalkboardOrdersRemoveAnnotationMessage>(OnRemoveAnnotationMessage);
        SubscribeLocalEvent<ChalkboardOrdersComponent, ChalkboardOrdersClearAnnotationsMessage>(OnClearAnnotationsMessage);
    }

    private void OnAfterOpen(EntityUid uid, ChalkboardOrdersComponent comp, AfterActivatableUIOpenEvent args)
    {
        _uiSystem.SetUiState(uid, ChalkboardOrdersUiKey.Key, BuildState(comp));
    }

    private void OnAddAnnotationMessage(EntityUid uid, ChalkboardOrdersComponent comp, ChalkboardOrdersAddAnnotationMessage args)
    {
        var sanitized = SanitizeAnnotation(args.Annotation);
        if (sanitized == null)
            return;

        comp.SharedAnnotations.Add(sanitized.Value);
        if (comp.SharedAnnotations.Count > MaxSharedAnnotations)
            comp.SharedAnnotations.RemoveAt(0);

        UpdateUi(uid, comp);
    }

    private void OnRemoveAnnotationMessage(EntityUid uid, ChalkboardOrdersComponent comp, ChalkboardOrdersRemoveAnnotationMessage args)
    {
        if (args.Index < 0 || args.Index >= comp.SharedAnnotations.Count)
            return;

        comp.SharedAnnotations.RemoveAt(args.Index);
        UpdateUi(uid, comp);
    }

    private void OnClearAnnotationsMessage(EntityUid uid, ChalkboardOrdersComponent comp, ChalkboardOrdersClearAnnotationsMessage args)
    {
        if (comp.SharedAnnotations.Count == 0)
            return;

        comp.SharedAnnotations.Clear();
        UpdateUi(uid, comp);
    }

    private void UpdateUi(EntityUid uid, ChalkboardOrdersComponent comp)
    {
        _uiSystem.SetUiState(uid, ChalkboardOrdersUiKey.Key, BuildState(comp));
    }

    private static ChalkboardOrdersBoundUserInterfaceState BuildState(ChalkboardOrdersComponent comp)
    {
        return new ChalkboardOrdersBoundUserInterfaceState(
            comp.BoardTitle,
            comp.BoardTexturePath.ToString(),
            comp.SharedAnnotations.ToArray());
    }

    private static WastelandMapAnnotation? SanitizeAnnotation(WastelandMapAnnotation annotation)
    {
        if (annotation.Type is not (WastelandMapAnnotationType.Marker
            or WastelandMapAnnotationType.Box
            or WastelandMapAnnotationType.Draw))
        {
            return null;
        }

        var label = annotation.Label.Trim();
        if (label.Length > 64)
            label = label[..64].TrimEnd();

        if (annotation.Type == WastelandMapAnnotationType.Draw)
        {
            var points = annotation.StrokePoints;
            if (points == null || points.Length < 4)
                return null;

            var count = Math.Min(points.Length & ~1, MaxStrokePoints);
            var sanitizedPoints = new float[count];
            for (var i = 0; i < count; i++)
                sanitizedPoints[i] = Math.Clamp(points[i], 0f, 1f);

            if (string.IsNullOrWhiteSpace(label))
                label = "Drawing";

            return new WastelandMapAnnotation(
                WastelandMapAnnotationType.Draw,
                0f,
                0f,
                0f,
                0f,
                label,
                annotation.PackedColor,
                Math.Clamp(annotation.StrokeWidth, 1f, 12f),
                sanitizedPoints);
        }

        var startX = Math.Clamp(annotation.StartX, 0f, 1f);
        var startY = Math.Clamp(annotation.StartY, 0f, 1f);
        var endX = Math.Clamp(annotation.EndX, 0f, 1f);
        var endY = Math.Clamp(annotation.EndY, 0f, 1f);

        if (string.IsNullOrWhiteSpace(label))
            label = annotation.Type == WastelandMapAnnotationType.Marker ? "Marker" : "Box";

        return new WastelandMapAnnotation(
            annotation.Type,
            startX,
            startY,
            endX,
            endY,
            label,
            annotation.PackedColor,
            Math.Clamp(annotation.StrokeWidth, 1f, 12f),
            null);
    }
}
