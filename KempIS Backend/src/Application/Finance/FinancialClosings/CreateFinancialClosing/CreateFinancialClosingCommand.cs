using Application.Abstractions.Messaging;

namespace Application.Finance.FinancialClosings.CreateFinancialClosing;

public sealed record CreateFinancialClosingCommand : ICommand<CreateFinancialClosingResponse>;
