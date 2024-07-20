using BuildingBlocks.Core.Exception.Types;

namespace BuildingBlocks.Core.Domain.Exceptions;

public class InvalidAmountException : BadRequestException
{
    public decimal Amount { get; }

    public InvalidAmountException(decimal amount)
        : base($"Amount: '{amount}' is invalid.")
    {
        Amount = amount;
    }
}
