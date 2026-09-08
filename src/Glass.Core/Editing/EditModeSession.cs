namespace Glass.Core.Editing;

public enum EditableSurfaceKind { Bar, Widget }

public readonly record struct EditSelection(EditableSurfaceKind Kind, Guid Id);

public sealed class EditModeSession
{
    public bool IsActive { get; private set; }
    public EditSelection? Selection { get; private set; }
    public event EventHandler? Changed;

    public void Enter(EditSelection? selection = null)
    {
        IsActive = true;
        Selection = selection;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Select(EditSelection selection)
    {
        if (!IsActive) IsActive = true;
        Selection = selection;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Exit()
    {
        if (!IsActive && Selection is null) return;
        IsActive = false;
        Selection = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
