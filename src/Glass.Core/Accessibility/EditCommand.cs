namespace Glass.Core.Accessibility;

public enum EditCommandKind
{
    SelectNext,
    SelectPrevious,
    NudgeLeft,
    NudgeRight,
    NudgeUp,
    NudgeDown,
    Grow,
    Shrink,
    MoveToPreviousZone,
    MoveToNextZone,
    Delete,
}

public sealed record EditCommand(EditCommandKind Kind, bool Coarse)
{
    public double Delta => Coarse ? 10 : 1;
}
