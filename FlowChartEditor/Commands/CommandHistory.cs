namespace FlowChartEditor.Commands;

public interface IFlowCommand
{
    string Description { get; }
    void Execute();
    void Undo();
}

public class CommandHistory
{
    private readonly Stack<IFlowCommand> _undoStack = new();
    private readonly Stack<IFlowCommand> _redoStack = new();

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public string? UndoDescription => _undoStack.TryPeek(out var c) ? c.Description : null;
    public string? RedoDescription => _redoStack.TryPeek(out var c) ? c.Description : null;

    public event Action? StateChanged;

    public void Execute(IFlowCommand command)
    {
        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear();
        StateChanged?.Invoke();
    }

    public void Undo()
    {
        if (!CanUndo) return;
        var command = _undoStack.Pop();
        command.Undo();
        _redoStack.Push(command);
        StateChanged?.Invoke();
    }

    public void Redo()
    {
        if (!CanRedo) return;
        var command = _redoStack.Pop();
        command.Execute();
        _undoStack.Push(command);
        StateChanged?.Invoke();
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        StateChanged?.Invoke();
    }
}
