namespace ExpenseTracker.Shared.Workflow;

public class WorkflowException : Exception
{
    public string Code { get; }

    public WorkflowException(string code, string message) : base(message)
    {
        Code = code;
    }
}
