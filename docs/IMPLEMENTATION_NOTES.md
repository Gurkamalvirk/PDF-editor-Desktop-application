# Implementation notes mapped to the blueprint

- **Source document remains immutable:** edits are represented by `ReplaceTextOperation` instances.
- **PDF coordinates are canonical:** `RectPdf` stores geometry independently of WPF zoom/DPI; `GeometryService` creates display rectangles.
- **Rendering/extraction off UI thread:** `PdfDocumentService` uses `Task.Run` and cancellation.
- **WPF native editing overlay:** the page image is the base layer; `ItemsControl + Canvas` is the selection/editor overlay.
- **Undo/redo:** `EditHistoryService` stores committed replacement operations and a cursor.
- **Export:** `ExportService` opens the untouched source, whiteouts edited bounds, redraws replacement text, saves a temp PDF, verifies it using PDFium, then moves it to the requested path.
- **Privacy:** no document upload, browser runtime, local web server, or persistent document database.
- **OCR:** intentionally left as a later local Tesseract integration because the first engineering milestone should prove rendering, text geometry, selection, and export alignment first.
