using Domain.Finance.FinancialClosings;
using SharedKernel;

namespace Application.Finance.FinancialClosings;

public interface IFinancialClosingReportRenderer
{
  Task<Result<byte[]>> RenderAsync(FinancialClosing closing, CancellationToken cancellationToken);
}
