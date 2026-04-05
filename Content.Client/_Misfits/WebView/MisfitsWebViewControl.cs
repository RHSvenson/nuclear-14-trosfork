// #Misfits Add - Domain-locked WebView wrapper for Misfits wiki integration
using System;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.WebView;

namespace Content.Client._Misfits.WebView;

/// <summary>
/// Wraps <see cref="WebViewControl"/> and locks all navigation to ss14.misfitsystems.net.
/// Includes a Back button for in-page navigation history.
/// </summary>
public sealed class MisfitsWebViewControl : Control
{
    private const string AllowedHost = "ss14.misfitsystems.net";

    private readonly WebViewControl _wv;

    public MisfitsWebViewControl()
    {
        HorizontalExpand = true;
        VerticalExpand = true;

        // Navigation toolbar with Back button
        _wv = new WebViewControl
        {
            HorizontalExpand = true,
            VerticalExpand = true
        };

        // Cancel all navigation to hosts outside our allowed domain
        _wv.AddBeforeBrowseHandler(OnBeforeBrowse);

        // Navigation toolbar with Back button
        var toolbar = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 4,
            Margin = new Thickness(0, 0, 0, 4)
        };

        var backButton = new Button
        {
            Text = Loc.GetString("ui-webview-back"),
            MinWidth = 60
        };
        backButton.OnPressed += _ => _wv.GoBack();
        toolbar.AddChild(backButton);

        var layout = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true
        };
        layout.AddChild(toolbar);
        layout.AddChild(_wv);

        AddChild(layout);
    }

    /// <summary>Navigate to a URL. Off-domain URLs are silently ignored.</summary>
    public void NavigateTo(string url)
    {
        if (!IsAllowed(url))
            return;
        _wv.Url = url;
    }

    private static bool IsAllowed(string url)
    {
        // Allow internal CEF protocols used during page rendering
        if (url.StartsWith("about:", StringComparison.Ordinal)
            || url.StartsWith("data:", StringComparison.Ordinal)
            || url.StartsWith("res://", StringComparison.Ordinal))
            return true;

        // Extract host using simple string parsing — avoids System.Uri/UriKind (sandbox-blocked)
        var host = ExtractHost(url);
        if (host == null)
            return false;

        return host.Equals(AllowedHost, StringComparison.OrdinalIgnoreCase)
               || host.EndsWith("." + AllowedHost, StringComparison.OrdinalIgnoreCase);
    }

    private static void OnBeforeBrowse(IBeforeBrowseContext ctx)
    {
        // Permit internal CEF navigations (about:blank, data: URIs used during render)
        if (ctx.Url.StartsWith("about:", StringComparison.Ordinal)
            || ctx.Url.StartsWith("data:", StringComparison.Ordinal))
            return;

        if (!IsAllowed(ctx.Url))
            ctx.DoCancel();
    }

    /// <summary>
    /// Extracts the hostname from a URL string without using System.Uri (sandbox restriction).
    /// Returns null if the URL has no recognizable scheme separator.
    /// </summary>
    private static string? ExtractHost(string url)
    {
        // Find "://" to locate start of host
        var sep = url.IndexOf("://", StringComparison.Ordinal);
        if (sep < 0)
            return null;

        var hostStart = sep + 3;
        if (hostStart >= url.Length)
            return null;

        // Host ends at the first '/' (path) or ':' (port), or end of string
        var hostEnd = url.Length;
        for (var i = hostStart; i < url.Length; i++)
        {
            if (url[i] == '/' || url[i] == ':')
            {
                hostEnd = i;
                break;
            }
        }

        return url.Substring(hostStart, hostEnd - hostStart);
    }
}

