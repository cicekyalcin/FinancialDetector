using System;
using System.Threading.Tasks;

namespace FinancialDetector.Domain.Interfaces
{
    public interface ITransactionAnalyzerService
    {
        Task<object> AnalyzeUserTransactions(Guid userId);
    }
}