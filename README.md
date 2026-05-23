# Expense Report Tracker

Serverless expense-approval workflow built for module **5ENTAPP / E5WMD — Enterprise Software Engineering on AWS** (Master, Semester 2). Supervisor: Dr. Abdelhak TOUITI.

A field employee photographs a receipt, submits a reimbursement request from a .NET MAUI app; a Finance Manager reviews the request and approves or rejects it with a written justification. Every state transition is enforced server-side in AWS Lambda.

---

## Architecture

```
┌──────────────────────┐
│  .NET MAUI (Mac Catalyst)
│  Role-conditional UI │
└─────────┬────────────┘
          │ HTTPS + Bearer JWT
          ▼
┌──────────────────────┐         ┌──────────────────────┐
│  Amazon Cognito      │ JWT     │  Amazon API Gateway  │
│  User Pool           │────────▶│  (REST + Cognito     │
│  Groups: employees,  │ claims  │   Authorizer)        │
│  finance             │         └─────────┬────────────┘
└──────────────────────┘                   │
                                           ▼
            ┌──────────────────────────────────────────┐
            │  AWS Lambda (.NET 8, C#) — 8 functions   │
            │  • CreateExpense  • ListMyExpenses       │
            │  • ListSubmittedQueue • GetExpense       │
            │  • SubmitExpense  • DecideExpense        │
            │  • GetUploadUrl   • GetReceiptUrl        │
            │  Shared state machine enforces workflow  │
            └────────┬─────────────────────┬───────────┘
                     │                     │
                     ▼                     ▼
        ┌────────────────────┐   ┌────────────────────┐
        │  Amazon DynamoDB   │   │  Amazon S3         │
        │  ExpenseTracker    │   │  Receipts bucket   │
        │  + GSI1 + GSI2     │   │  (private, SSE)    │
        └────────────────────┘   └────────────────────┘
```

## Repository layout

```
.
├── src/
│   ├── Shared/ExpenseTracker.Shared/      Shared contracts (DTOs, enums, state machine)
│   ├── Lambdas/ExpenseTracker.Lambdas/    All 8 AWS Lambda handlers
│   └── App/ExpenseTracker.App/            .NET MAUI app (Mac Catalyst)
├── ExpenseTracker.sln
├── Directory.Build.props
└── README.md
```

## Workflow

```
Draft ──Submit──▶ Submitted ──Approve──▶ Approved
                       │
                       └──Reject──▶ Rejected ──Resubmit──▶ Resubmitted ──Approve / Reject──▶ ...
```

Every transition is encoded in `ExpenseTracker.Shared/Workflow/ExpenseStateMachine.cs`. The Lambdas call `EnsureTransition(...)` which throws `WorkflowException` if the action is not allowed for the current state, role, ownership, or comment requirement. The MAUI ViewModels call the same machine through `CanSubmit / CanApprove / CanReject / CanResubmit` to enable or hide buttons, so the UI never offers an action the server would reject.

### Approval-threshold rule (bonus)

`Workflow/ApprovalPolicy.cs` enforces an additional rule: any **Approve** decision on an amount strictly greater than **500 €** requires a written justification from the Finance Manager. This addresses the *"missing approval threshold"* scenario from the project brief.

## DynamoDB single-table design

Table name: `ExpenseTracker`

| Item                | PK                       | SK                          | Notes |
|---------------------|--------------------------|-----------------------------|-------|
| Expense             | `USER#<ownerSub>`        | `EXPENSE#<expenseId>`       | One employee → one partition |

### Sparse GSIs

| Index | PK                       | SK                                       | Populated when                | Serves                                  |
|-------|--------------------------|------------------------------------------|-------------------------------|-----------------------------------------|
| GSI1  | `STATUS#<status>`        | `<submittedAt iso8601>#<expenseId>`      | Status ∈ {Submitted, Resubmitted} | Finance queue (FIFO) |
| GSI2  | `DECIDER#<deciderSub>`   | `<decisionAt iso8601>#<expenseId>`       | Status ∈ {Approved, Rejected}     | Audit per manager |

A `DynamoMapper` centralises the key shape so it cannot drift across functions; the sparse pattern means the queue index contains *only* what is actually awaiting decision.

### Access patterns covered

| Use case                                | Operation                                              |
|-----------------------------------------|--------------------------------------------------------|
| Employee lists own expenses             | `Query PK = USER#<sub>, SK begins_with EXPENSE#`       |
| Employee or Finance reads one expense   | `GetItem PK = USER#<sub>, SK = EXPENSE#<id>`           |
| Finance lists submitted/resubmitted     | `Query GSI1PK = STATUS#Submitted` (or `Resubmitted`)   |
| Audit of decisions per Finance Manager  | `Query GSI2PK = DECIDER#<sub>`                          |

## REST endpoints

| Method | Path | Authorized role | Lambda |
|--------|------|------------------|--------|
| POST   | `/expenses` | employees | `CreateExpenseFunction` |
| GET    | `/expenses` | employees | `ListMyExpensesFunction` |
| GET    | `/expenses/queue` | finance | `ListSubmittedQueueFunction` |
| GET    | `/expenses/{id}` | employees (owner) or finance + `?ownerSub` | `GetExpenseFunction` |
| POST   | `/expenses/{id}/submit` | employees (owner) | `SubmitExpenseFunction` |
| POST   | `/expenses/{id}/decision` | finance + `?ownerSub` | `DecideExpenseFunction` |
| POST   | `/expenses/{id}/receipt-upload-url` | employees (owner) | `GetUploadUrlFunction` |
| GET    | `/expenses/{id}/receipt-url` | employees (owner) or finance + `?ownerSub` | `GetReceiptUrlFunction` |

All routes sit behind an API Gateway **Cognito Authorizer** that validates the JWT before the Lambda runs. The Lambda then reads `cognito:groups` and `sub` from the injected claims via `UserContext.From(request)`.

## Receipts: never public

S3 bucket access is fully private. The MAUI app:

1. Calls `POST /expenses/{id}/receipt-upload-url` → Lambda returns a short-lived (≤ 15 min) **pre-signed PUT URL**.
2. Uploads the photo directly to S3 against that URL — does not transit through Lambda or API Gateway.
3. Calls `GET /expenses/{id}/receipt-url` to display the receipt → Lambda returns a short-lived **pre-signed GET URL**.

The Lambda enforces ownership and editability before signing (the bucket itself has Block-Public-Access enabled).

## Prerequisites

- **.NET 8 SDK** (`brew install --cask dotnet-sdk` on macOS)
- **MAUI workload**: `dotnet workload install maui`
- **Xcode** (required for the Mac Catalyst target)
- For Step 4 deployment: AWS CLI configured (`aws configure`)

## Running locally — no AWS needed

The app ships with a complete on-device mock backend (`InMemoryMockApi` + `MockAuthService`) that re-uses the **same** `ExpenseStateMachine` and `ApprovalPolicy` as the Lambdas. The mock flag lives at the top of `MauiProgram.cs`:

```csharp
private const bool UseMockBackend = true;
```

Build and launch on Mac Catalyst:

```bash
cd src/App/ExpenseTracker.App
dotnet build -f net8.0-maccatalyst
dotnet build -t:Run -f net8.0-maccatalyst
```

### Demo accounts (mock mode)

| Email            | Password | Role             |
|------------------|----------|------------------|
| `alice@demo`     | `demo`   | Employee         |
| `charlie@demo`   | `demo`   | Employee         |
| `bob@demo`       | `demo`   | Finance Manager  |

When Alice signs in, she sees five pre-seeded expenses across all statuses (Draft, Submitted, Rejected, plus a 1 200 € item that triggers the senior-approval badge). When Bob signs in, the queue lists everything currently awaiting decision.

## Deploying to AWS (Step 4 — to be completed)

1. Create the DynamoDB table `ExpenseTracker` with `PK` + `SK`, plus GSIs `GSI1` and `GSI2`.
2. Create the S3 bucket (Block Public Access **on**, SSE-S3 default, CORS on `PUT/GET`).
3. Create the Cognito User Pool, two groups (`employees`, `finance`), and an app client.
4. Deploy each Lambda with `dotnet lambda deploy-function` using the handler names listed below, scoped IAM roles (least privilege per function), and the environment variables `TABLE_NAME`, `RECEIPTS_BUCKET`, `PRESIGNED_URL_TTL_MINUTES`.
5. Wire the API Gateway REST API with a Cognito Authorizer, then point each route to its Lambda integration.
6. In `MauiProgram.cs`, flip `UseMockBackend = false` and fill in the `CognitoConfig` and `ApiConfig` values.

Lambda handler entry points:

```
ExpenseTracker.Lambdas::ExpenseTracker.Lambdas.Functions.CreateExpenseFunction::Handler
ExpenseTracker.Lambdas::ExpenseTracker.Lambdas.Functions.ListMyExpensesFunction::Handler
ExpenseTracker.Lambdas::ExpenseTracker.Lambdas.Functions.ListSubmittedQueueFunction::Handler
ExpenseTracker.Lambdas::ExpenseTracker.Lambdas.Functions.GetExpenseFunction::Handler
ExpenseTracker.Lambdas::ExpenseTracker.Lambdas.Functions.SubmitExpenseFunction::Handler
ExpenseTracker.Lambdas::ExpenseTracker.Lambdas.Functions.DecideExpenseFunction::Handler
ExpenseTracker.Lambdas::ExpenseTracker.Lambdas.Functions.GetUploadUrlFunction::Handler
ExpenseTracker.Lambdas::ExpenseTracker.Lambdas.Functions.GetReceiptUrlFunction::Handler
```
