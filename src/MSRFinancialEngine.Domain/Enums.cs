namespace MSRFinancialEngine.Domain;

public enum SourceType
{
    BankStatementCsv,
    BankStatementOfx,
    ErpJson,
    InvoiceXmlNfe
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
