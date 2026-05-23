using ExpenseTracker.Shared.Models;

namespace ExpenseTracker.Shared.Workflow;

/// <summary>
/// Single source of truth for the expense lifecycle.
/// Used by Lambdas to enforce transitions, and by MAUI to enable/disable buttons.
/// </summary>
public static class ExpenseStateMachine
{
    private record Transition(
        ExpenseStatus From,
        ExpenseAction Action,
        ExpenseStatus To,
        UserRole RequiredRole,
        bool RequiresOwnership,
        bool RequiresComment);

    private static readonly Transition[] _transitions =
    {
        new(ExpenseStatus.Draft,       ExpenseAction.Submit,    ExpenseStatus.Submitted,   UserRole.Employee,       RequiresOwnership: true,  RequiresComment: false),
        new(ExpenseStatus.Submitted,   ExpenseAction.Approve,   ExpenseStatus.Approved,    UserRole.FinanceManager, RequiresOwnership: false, RequiresComment: false),
        new(ExpenseStatus.Submitted,   ExpenseAction.Reject,    ExpenseStatus.Rejected,    UserRole.FinanceManager, RequiresOwnership: false, RequiresComment: true),
        new(ExpenseStatus.Rejected,    ExpenseAction.Resubmit,  ExpenseStatus.Resubmitted, UserRole.Employee,       RequiresOwnership: true,  RequiresComment: false),
        new(ExpenseStatus.Resubmitted, ExpenseAction.Approve,   ExpenseStatus.Approved,    UserRole.FinanceManager, RequiresOwnership: false, RequiresComment: false),
        new(ExpenseStatus.Resubmitted, ExpenseAction.Reject,    ExpenseStatus.Rejected,    UserRole.FinanceManager, RequiresOwnership: false, RequiresComment: true),
    };

    public static bool CanTransition(
        ExpenseStatus current,
        ExpenseAction action,
        UserRole role,
        bool isOwner,
        string? comment = null)
    {
        var t = Find(current, action);
        if (t is null) return false;
        if (t.RequiredRole != role) return false;
        if (t.RequiresOwnership && !isOwner) return false;
        if (t.RequiresComment && string.IsNullOrWhiteSpace(comment)) return false;
        return true;
    }

    /// <summary>
    /// Server-side enforcement: throws WorkflowException when the action is not allowed,
    /// otherwise returns the target status.
    /// </summary>
    public static ExpenseStatus EnsureTransition(
        ExpenseStatus current,
        ExpenseAction action,
        UserRole role,
        bool isOwner,
        string? comment = null)
    {
        var t = Find(current, action)
                ?? throw new WorkflowException("INVALID_TRANSITION",
                    $"Action '{action}' is not allowed from status '{current}'.");

        if (t.RequiredRole != role)
            throw new WorkflowException("FORBIDDEN",
                $"Role '{role}' cannot perform '{action}'. Required: '{t.RequiredRole}'.");

        if (t.RequiresOwnership && !isOwner)
            throw new WorkflowException("FORBIDDEN",
                $"Only the owner can perform '{action}'.");

        if (t.RequiresComment && string.IsNullOrWhiteSpace(comment))
            throw new WorkflowException("COMMENT_REQUIRED",
                $"A comment is required to perform '{action}'.");

        return t.To;
    }

    // UI convenience helpers --------------------------------------------------

    public static bool CanSubmit(ExpenseStatus status, UserRole role, bool isOwner)
        => CanTransition(status, ExpenseAction.Submit, role, isOwner);

    public static bool CanResubmit(ExpenseStatus status, UserRole role, bool isOwner)
        => CanTransition(status, ExpenseAction.Resubmit, role, isOwner);

    public static bool CanApprove(ExpenseStatus status, UserRole role)
        => CanTransition(status, ExpenseAction.Approve, role, isOwner: false, comment: null);

    public static bool CanReject(ExpenseStatus status, UserRole role)
        // Comment requirement is checked at submit time on the UI; here we only gate visibility.
        => Find(status, ExpenseAction.Reject) is { } t && t.RequiredRole == role;

    /// <summary>
    /// Whether the owner can edit the editable fields (amount/category/description/receipt).
    /// Draft is freely editable; Rejected is editable so the employee can fix and resubmit.
    /// </summary>
    public static bool CanEdit(ExpenseStatus status, UserRole role, bool isOwner)
    {
        if (role != UserRole.Employee || !isOwner) return false;
        return status is ExpenseStatus.Draft or ExpenseStatus.Rejected;
    }

    private static Transition? Find(ExpenseStatus from, ExpenseAction action)
    {
        foreach (var t in _transitions)
            if (t.From == from && t.Action == action) return t;
        return null;
    }
}
