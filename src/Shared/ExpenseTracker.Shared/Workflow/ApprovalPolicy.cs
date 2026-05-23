using ExpenseTracker.Shared.Models;

namespace ExpenseTracker.Shared.Workflow;

/// <summary>
/// Business policy applied on top of the state machine. Kept separate so transition rules
/// (which states can move where) stay independent from monetary policy (what to require
/// for certain amounts).
///
/// Rule: any approval of an amount strictly greater than <see cref="SeniorApprovalThreshold"/>
/// requires a written justification by the Finance Manager — addresses the
/// "missing approval threshold" scenario described in the project brief.
/// </summary>
public static class ApprovalPolicy
{
    public const decimal SeniorApprovalThreshold = 500m;

    public static bool RequiresSeniorApproval(decimal amount)
        => amount > SeniorApprovalThreshold;

    public static void EnsureDecisionComplies(decimal amount, ExpenseAction action, string? comment)
    {
        if (action != ExpenseAction.Approve) return;
        if (!RequiresSeniorApproval(amount)) return;
        if (string.IsNullOrWhiteSpace(comment))
            throw new WorkflowException(
                "SENIOR_APPROVAL_COMMENT_REQUIRED",
                $"Amounts above {SeniorApprovalThreshold} require a written justification when approving.");
    }
}
