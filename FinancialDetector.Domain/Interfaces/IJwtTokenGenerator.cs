using FinancialDetector.Domain.Entities;

namespace FinancialDetector.Domain.Interfaces
{
    public interface IJwtTokenGenerator
    {
        string GenerateToken(User user);
    }
}