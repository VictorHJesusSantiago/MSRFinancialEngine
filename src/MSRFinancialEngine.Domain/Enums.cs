namespace MSRFinancialEngine.Domain;

public enum SourceType
{
    BankStatementCsv,
    BankStatementOfx,
    ErpJson,
    InvoiceXmlNfe,
    BankStatementMt940
}

public enum MatchingRuleType
{
    Deterministic,
    Fuzzy
}

public enum MatchCandidateStatus
{
    PendingReview,
    AutoApproved,
    ManuallyApproved,
    Rejected
}

public enum DivergenceReason
{
    NoCandidate,
    MultipleCandidates,
    AmountOutOfTolerance,
    CurrencyMismatch,
    DateOutOfTolerance
}

public enum DivergenceStatus
{
    Open,
    InReview,
    Resolved,
    NotReconcilable
}

public enum ApprovalDecisionType
{
    AcceptSuggestion,
    ManualMatch,
    MarkNotReconcilable
}

public enum UserRole
{
    Viewer,

    Analyst,

    Approver,

    Admin
}
