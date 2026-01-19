namespace ProiectPSSC.Domain.Billing;

public sealed record InvoiceLine(string ProductCode, int Quantity, decimal UnitPrice)
{
    public decimal LineTotal => Quantity * UnitPrice;
}

