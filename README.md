# Expense Report Tracker

**Module 5ENTAPP / E5WMD — Enterprise Software Engineering on AWS**  
Master Semestre 2 — Encadrant : Dr. Abdelhak TOUITI

Système de gestion de notes de frais serverless sur AWS avec workflow d'approbation, authentification Cognito et interface .NET MAUI.

---

## Démarrage rapide

### 1. Prérequis

| Outil | Version min | Installation |
|-------|-------------|--------------|
| **Xcode** (app complète) | 15+ | App Store → chercher "Xcode" → Installer (~10 Go, 30–60 min) |
| **.NET SDK** | 10.0 | `brew install --cask dotnet-sdk` |
| **Workload MAUI** | dernier | `dotnet workload install maui` |
| **Git** | quelconque | pré-installé sur macOS (`xcode-select --install` sinon) |

Après installation de Xcode, **lancer Xcode une fois** (il effectue sa première configuration), puis dans un terminal :

```bash
sudo xcode-select -s /Applications/Xcode.app/Contents/Developer
sudo xcodebuild -license accept
```

### 2. Cloner et restaurer

```bash
git clone https://github.com/ludovicmoyo/expense-tracker-5entapp.git
cd expense-tracker-5entapp
dotnet restore
```

### 3. Lancer l'application

```bash
cd src/App/ExpenseTracker.App
dotnet build -t:Run -f net10.0-maccatalyst
```

Une fenêtre macOS native s'ouvre avec l'écran de connexion. L'application se connecte directement au **backend AWS déjà déployé** (Cognito + Lambda + DynamoDB + S3 en `eu-north-1`) — aucune configuration AWS requise.

### Comptes de démonstration

| Email | Mot de passe | Rôle |
|-------|-------------|------|
| `alice@demo` | `Demo1#@@` | Employee |
| `charlie@demo` | `Demo1#@@` | Employee |
| `bob@demo` | `Demo1#@@` | Finance Manager |

---

## Scénario de démonstration complet

1. **Connexion alice@demo / Demo1#@@** → liste des notes de frais (statuts variés)
2. **"New Expense"** → remplir montant / catégorie / description → **"Save as Draft"**
3. Ouvrir la note → **"Attach / Replace receipt"** → sélectionner une photo
4. **"Submit for approval"** → la note passe en `Submitted`
5. **Sign out** → connexion **bob@demo / Demo1#@@** → la queue Finance contient la note
6. Cliquer dessus → voir le reçu → écrire un commentaire → **Approve** ou **Reject**

> **Règle métier à tester :** toute note > 500 € nécessite un commentaire obligatoire pour être approuvée. Tenter d'approuver sans commentaire déclenche une erreur `SENIOR_APPROVAL_COMMENT_REQUIRED`.

---

## Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                    .NET MAUI (Mac Catalyst)                       │
│          Vue Employé ──── ou ──── Vue Finance Manager            │
└───────────────────────────┬──────────────────────────────────────┘
                            │ HTTPS + JWT Bearer (IdToken Cognito)
                            ▼
            ┌───────────────────────────────┐
            │     Amazon Cognito            │
            │  User Pool eu-north-1         │
            │  Groupes: employees / finance │
            │  Auth SRP (pas de mot de      │
            │  passe en clair sur le réseau)│
            └───────────────┬───────────────┘
                            │ JWT validé par Authorizer
                            ▼
            ┌───────────────────────────────┐
            │    Amazon API Gateway         │
            │  REST API — 8 routes          │
            │  Cognito Authorizer global    │
            │  Intégration AWS_PROXY        │
            └───────────────┬───────────────┘
                            │ APIGatewayProxyRequest
                            ▼
┌───────────────────────────────────────────────────────────────┐
│                  AWS Lambda — .NET 10 / arm64                  │
│                                                               │
│  CreateExpense    ListMyExpenses    ListSubmittedQueue         │
│  GetExpense       SubmitExpense     DecideExpense              │
│  GetUploadUrl     GetReceiptUrl                               │
│                                                               │
│  → Chaque fonction lit le claim cognito:groups pour le RBAC   │
│  → EnsureTransition() bloque toute transition illégale        │
└──────────────────┬────────────────────┬───────────────────────┘
                   │                    │
                   ▼                    ▼
   ┌───────────────────────┐  ┌─────────────────────────┐
   │     Amazon DynamoDB   │  │       Amazon S3          │
   │  Table: ExpenseTracker│  │  Bucket: privé           │
   │  PK/SK + GSI1 + GSI2  │  │  Accès: pre-signed URLs  │
   │  Single-table design  │  │  TTL: 15 min             │
   └───────────────────────┘  └─────────────────────────┘
```

---

## Structure du dépôt

```
.
├── src/
│   ├── Shared/ExpenseTracker.Shared/       DTOs, modèles, machine à états
│   │   ├── Models/                         Expense, ExpenseStatus, UserRole…
│   │   ├── Dtos/                           Contrats API (CreateExpenseRequest…)
│   │   └── Workflow/
│   │       ├── ExpenseStateMachine.cs      Table de transitions (source unique de vérité)
│   │       └── ApprovalPolicy.cs           Règle seuil 500 €
│   │
│   ├── Lambdas/ExpenseTracker.Lambdas/     8 fonctions Lambda C#
│   │   ├── Functions/                      Un fichier par Lambda
│   │   └── Common/
│   │       ├── UserContext.cs              Extraction RBAC depuis les claims JWT
│   │       ├── DynamoMapper.cs             Sérialisation DynamoDB (keys + GSIs)
│   │       ├── ExpenseRepository.cs        Accès DynamoDB
│   │       ├── ReceiptStorage.cs           Génération pre-signed URLs S3
│   │       └── ServiceFactory.cs           Singleton des clients AWS SDK
│   │
│   └── App/ExpenseTracker.App/             Interface .NET MAUI
│       ├── Services/
│       │   ├── CognitoAuthService.cs       Auth SRP via Amazon.Extensions.CognitoAuthentication
│       │   ├── RealExpenseApi.cs           Appels HTTP vers API Gateway
│       │   └── AuthorizingHttpHandler.cs   Injection du Bearer token automatique
│       └── MauiProgram.cs                  Injection de dépendances (Cognito + API Gateway)
│
├── RAPPORT.md                              Rapport académique du projet
└── README.md                               Ce fichier
```

---

## Workflow des états

```
           [Employee]              [Finance Manager]
              │                         │
         Submit ↓                  Approve ↓    Reject ↓
                                        │           │
Draft ──────────▶ Submitted ────────────▶ Approved  │
                      │                             │
                      └──────────────▶ Rejected ────┘
                                          │
                                    Resubmit ↓ [Employee]
                                          │
                                    Resubmitted ──▶ Approved / Rejected
```

Toutes les transitions sont encodées dans `ExpenseStateMachine.cs`. La méthode `EnsureTransition()` est appelée **server-side** dans chaque Lambda et lève une `WorkflowException` si la transition est interdite (mauvais rôle, mauvais propriétaire, commentaire manquant). Le client ne peut pas contourner cette vérification.

---

## Modèle DynamoDB (single-table design)

**Table :** `ExpenseTracker` — région `eu-north-1`

| Clé | Valeur | Description |
|-----|--------|-------------|
| `PK` | `USER#{ownerSub}` | Partition par utilisateur |
| `SK` | `EXPENSE#{expenseId}` | Tri par note |

### Index secondaires globaux (GSIs)

| Index | GSI PK | GSI SK | Rempli quand | Usage |
|-------|--------|--------|--------------|-------|
| **GSI1** | `STATUS#{status}` | `{submittedAt}#{expenseId}` | `Submitted` ou `Resubmitted` | File d'attente Finance (FIFO) |
| **GSI2** | `DECIDER#{sub}` | `{decisionAt}#{expenseId}` | `Approved` ou `Rejected` | Audit par gestionnaire |

Les GSIs sont **sparse** : un item n'apparaît dans GSI1 que lorsqu'il attend une décision, et en disparaît automatiquement quand il est traité (les attributs GSI1PK/GSI1SK ne sont pas écrits pour les autres statuts).

---

## Routes API Gateway

**Base URL :** `https://aqervpkypa.execute-api.eu-north-1.amazonaws.com/dev/`  
**Authorizer :** Cognito User Pool `eu-north-1_5Gh4yACKF` sur toutes les routes

| Méthode | Route | Rôle requis | Lambda |
|---------|-------|-------------|--------|
| `POST` | `/expenses` | employees | `CreateExpenseFunction` |
| `GET` | `/expenses` | employees | `ListMyExpensesFunction` |
| `GET` | `/expenses/queue` | finance | `ListSubmittedQueueFunction` |
| `GET` | `/expenses/{id}` | employees (owner) ou finance + `?ownerSub` | `GetExpenseFunction` |
| `POST` | `/expenses/{id}/submit` | employees (owner) | `SubmitExpenseFunction` |
| `POST` | `/expenses/{id}/decision` | finance + `?ownerSub` | `DecideExpenseFunction` |
| `POST` | `/expenses/{id}/receipt-upload-url` | employees (owner) | `GetUploadUrlFunction` |
| `GET` | `/expenses/{id}/receipt-url` | employees (owner) ou finance | `GetReceiptUrlFunction` |

---

## Upload de justificatifs (S3 pre-signed URLs)

Le bucket S3 est **entièrement privé** (Block Public Access activé). Le flow est en deux étapes :

```
App ──POST /receipt-upload-url──▶ Lambda ──GeneratePreSignedPUT──▶ S3
App ◀── { uploadUrl, s3Key } ─────────────────────────────────────
App ──PUT uploadUrl (direct S3, sans passer par API Gateway)────▶ S3
```

L'application envoie le fichier directement à S3 via l'URL pré-signée (valide 15 min). Lambda persiste la clé S3 dans DynamoDB immédiatement — `SubmitExpense` vérifie que la clé est présente avant d'autoriser la soumission.

---

## Problèmes fréquents

**Xcode non trouvé :**
```bash
sudo xcode-select -s /Applications/Xcode.app/Contents/Developer
```

**MAUI workload manquant :**
```bash
dotnet workload install maui
```

**`dotnet : command not found` après installation brew :**
```bash
# Ajouter au ~/.zshrc puis relancer le terminal
export PATH="$PATH:/usr/local/share/dotnet"
```

**Vérifier que le backend compile sans Xcode :**
```bash
dotnet build src/Shared/ExpenseTracker.Shared/ExpenseTracker.Shared.csproj
dotnet build src/Lambdas/ExpenseTracker.Lambdas/ExpenseTracker.Lambdas.csproj
```
