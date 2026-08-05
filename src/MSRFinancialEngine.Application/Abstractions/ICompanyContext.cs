namespace MSRFinancialEngine.Application.Abstractions;

public interface ICompanyContext
{
    Guid? CompanyId { get; }

    void SetCompany(Guid? companyId);
}

public class CompanyContext : ICompanyContext
{
    public Guid? CompanyId { get; private set; }

    public void SetCompany(Guid? companyId) => CompanyId = companyId;
}
