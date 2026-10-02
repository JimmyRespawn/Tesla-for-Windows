# Archived Model 3 WebView page

The former `Views/Model3Page.xaml` / `.xaml.cs` and the Three.js assets in
`Assets/Model3/` are retained for reference and recovery. Their original paths
remain stable, and the shared GLB is still used by the native Helix page.

The former navigation item is hidden and its route is removed. The native
page's comparison button is removed. `Model3HelixPage` is now the active
**Model 3 · 3D** page. Do not restore the archived WebView page automatically
when a native render fails.
