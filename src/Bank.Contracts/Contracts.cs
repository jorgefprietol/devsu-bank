namespace Bank.Contracts;

public sealed class BusinessException(string message, int status = 400) : Exception(message)
{
    public int Status { get; } = status;
}

public static class Rules
{
    public static string Required(string? value, string field, int max = 150)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > max)
            throw new BusinessException($"{field} es obligatorio y admite hasta {max} caracteres.");
        return value.Trim();
    }

    public static decimal Money(decimal value)
    {
        if (decimal.Round(value, 2) != value || value > 9999999999999999.99m || value < -9999999999999999.99m)
            throw new BusinessException("El importe debe tener como máximo 2 decimales y caber en decimal(18,2).");
        return value;
    }
}

// Only integration data crosses the service boundary. Passwords and PII are excluded.
public sealed record ClientChanged(Guid EventId, Guid ClienteId, string Nombre,
    bool Estado, bool Eliminado, long Version, DateTime OccurredAt);
