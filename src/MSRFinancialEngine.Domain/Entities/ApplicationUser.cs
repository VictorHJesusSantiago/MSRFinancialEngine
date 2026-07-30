namespace MSRFinancialEngine.Domain.Entities;

/// <summary>Usuário simplificado do sistema (revisor/aprovador). Sem autenticação completa neste MVP.</summary>
public class ApplicationUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
}
