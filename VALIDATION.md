# Validation status

The project was statically checked in the generation environment:

- WPF XAML and the project file parse as valid XML.
- Source files passed basic delimiter/static scans.
- The PDFium adapter was checked against the current `PdfiumViewer.Net.WPF` 3.x source API (`PdfDocument.Pages`, `PdfPage.Render`, `GetText`, `GetTextBounds`, `DeviceToPage`).
- The source uses canonical PDF coordinates for edit operations and converts to WPF coordinates only for display.

## Build validation limitation

This generation environment does not have the Windows .NET/WPF SDK installed, so a Windows executable could not be compiled or launched here. Build on Windows with Visual Studio 2022 (.NET desktop development workload) or the .NET 8 SDK using `build-release.ps1`.


## 2026-09-08 namespace-collision fix
- Removed `<UseWindowsForms>true</UseWindowsForms>` from the WPF project.
- Qualified WPF `Application`, input/drag event types, `MessageBox`, `Point`, and `Size` where collisions could occur.
- Hardened `build-release.ps1` so a failing `dotnet restore` or `dotnet publish` exits as a build failure instead of printing a false success message.

## v5 startup fix
- Fixed WPF KeyBinding syntax in `MainWindow.xaml`: `Modifiers="Control,Shift"` -> `Modifiers="Control+Shift"`.
- WPF `ModifierKeysConverter` requires multiple modifier keys to be separated by `+`.
- Startup error dialog now surfaces the deepest inner-exception message while retaining the complete stack trace in `%LOCALAPPDATA%\\PdfTextEditor\\startup-error.log`.
