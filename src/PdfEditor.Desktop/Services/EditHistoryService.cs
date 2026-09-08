using PdfEditor.Desktop.Models;

namespace PdfEditor.Desktop.Services;

public sealed class EditHistoryService
{
    private readonly List<ReplaceTextOperation> _operations = new();
    private int _cursor;

    public IReadOnlyList<ReplaceTextOperation> ActiveOperations => _operations.Take(_cursor).ToList();
    public bool CanUndo => _cursor > 0;
    public bool CanRedo => _cursor < _operations.Count;
    public bool IsDirty => _cursor > 0;

    public void Add(ReplaceTextOperation operation)
    {
        if (_cursor < _operations.Count)
            _operations.RemoveRange(_cursor, _operations.Count - _cursor);
        _operations.Add(operation);
        _cursor = _operations.Count;
    }

    public ReplaceTextOperation? Undo()
    {
        if (!CanUndo) return null;
        _cursor--;
        return _operations[_cursor];
    }

    public ReplaceTextOperation? Redo()
    {
        if (!CanRedo) return null;
        var operation = _operations[_cursor];
        _cursor++;
        return operation;
    }

    public ReplaceTextOperation? EffectiveOperation(Guid regionId) =>
        ActiveOperations.LastOrDefault(x => x.RegionId == regionId);

    public string EffectiveText(Guid regionId, string original) =>
        EffectiveOperation(regionId)?.NewText ?? original;

    public void Clear()
    {
        _operations.Clear();
        _cursor = 0;
    }
}
