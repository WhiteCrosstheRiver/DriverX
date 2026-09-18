# DriverX Fluent UI reference notes

The UI references are checked out under `vendor/ui-reference` for local study:

- Bluechirp: navigation rail, compact command bar, calm information hierarchy.
- Files: navigation hierarchy, toolbar actions, density, tabs and settings structure.
- DevToys: tool-like page layout, search, grouped settings and empty states.
- WinUI Gallery: NavigationView, InfoBar, Card, CommandBar and responsive layout patterns.
- WPF UI: Fluent palette, Mica-like surfaces, title-bar spacing and theme tokens.
- PowerToys: settings information architecture, module cards and clear status feedback.

## Source files inspected

- `Bluechirp/Source/Bluechirp/Views/ShellPage.xaml` and `Controls/TitleBar/TitleBar.xaml`: compact navigation, search/title-bar alignment, footer destinations.
- `Files/src/Files.App/Views` and its resource dictionaries: file-manager density, command placement, multi-level navigation, reusable surfaces.
- `DevToys/src/app/dev/platforms/desktop/DevToys.Windows/MainWindow.xaml` and `Controls/MicaWindowWithOverlay.xaml`: Mica host window and tool-oriented page framing.
- `WinUI-Gallery/WinUIGallery/MainWindow.xaml` and `Pages/SettingsPage.xaml`: adaptive navigation and canonical WinUI settings grouping.
- `wpfui/src/Wpf.Ui.Gallery/Views/Windows/MainWindow.xaml` and `src/Wpf.Ui/Resources/Variables.xaml`: WPF Fluent theme tokens and navigation shell behavior.
- `PowerToys/src/settings-ui/Settings.UI.Controls/ModuleList/ModuleList.xaml`: production settings cards, status hierarchy and restrained action density.

The checked-out revisions are recorded by Git in each repository. All six directories retain their `.git` metadata and their `origin` URLs point to the requested upstream GitHub repositories through the supplied IPv6 proxy. Those large study checkouts stay local and are intentionally excluded from the DriverX product repository; this keeps the product clone small while preserving reproducible source links and the design audit above.

DriverX applies the useful patterns without copying product branding or source code:

1. A navigation rail groups disks and protocols; settings is a top-level destination.
2. The content area starts with a command bar and a short page description.
3. Connections are cards with one primary action, a drive badge, endpoint metadata and a quiet status line.
4. Empty state explains the next action instead of showing a blank table.
5. Theme tokens define background, surface, text, muted text, border, accent and selection colors.
6. Actions remain keyboard focusable and the interface uses a consistent 8px spacing rhythm.

Performance defaults are deliberately conservative: directory cache 2 seconds, attribute cache 1 second, SFTP polling disabled because SFTP has no change-notification API, two transfers/checkers, 4 MB buffer and bounded read chunks. This targets change visibility and predictable local resource use while leaving rclone/WinFsp responsible for the data path.

Reference repositories are used for design study only. The product implementation now lives in `native/DriverX.Desktop` and uses C# with WPF/.NET 8. The earlier Tkinter build remains only as a compatibility fallback. C# was chosen over C++ because rclone and WinFsp already perform the network and filesystem work in native processes; WPF provides native Windows integration with much lower UI development and maintenance cost than a custom C++ frontend.
