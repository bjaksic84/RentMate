namespace RentMate.Shared.Contracts.Responses;

/// <summary>
/// Lightweight payment summary for lists.
/// </summary>
public record PaymentSummary(
    int Id,
    decimal Amount,
    DateTime PaymentDate,
    string PaymentMethod,
    string? TransactionId,
    int RentalId,
    string PayerUserName
);

/// <summary>
/// Full payment details with rental context.
/// </summary>
public record PaymentDetails(
    int Id,
    decimal Amount,
    DateTime PaymentDate,
    string PaymentMethod,
    string? TransactionId,
    RentalSummary Rental,
    UserSummary Payer
);
