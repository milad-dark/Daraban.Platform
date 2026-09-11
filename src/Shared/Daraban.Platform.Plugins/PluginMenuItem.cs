namespace Daraban.Platform.Plugins;

/// <summary>
/// One navigation entry a plugin adds to the Angular shell. Menu items are served by
/// the platform's plugin API and merged into the shell's navigation by the frontend
/// (registry of installed, enabled plugins).
/// </summary>
/// <param name="Title">Label shown in the sidebar (already localized by the plugin).</param>
/// <param name="Route">Frontend route the entry points at, e.g. <c>/plugins/sample/assets</c>.</param>
/// <param name="Icon">Material icon name used by the shell's icon directive.</param>
/// <param name="Order">Sort weight within the sidebar; lower renders first.</param>
/// <param name="RequiredPermission">
/// Optional permission code (e.g. <c>plugins.sample.read</c>). The shell hides entries
/// the current user lacks; the plugin API filters server-side too.
/// </param>
public sealed record PluginMenuItem(
    string Title,
    string Route,
    string Icon = "extension",
    int Order = 100,
    string? RequiredPermission = null);
