using FinFlow.Domain.Enums;

namespace FinFlow.Domain.Calculators;

/// <summary>
/// Формирование прежнего и изменённого номера счёта (постановление № 123).
/// Формат номеров в записке не приводится, поэтому принят следующий формат (допущение):
/// прежний номер – 13 цифр (балансовый счёт 4 + код клиента 5 + номер договора 4),
/// изменённый – 17 цифр (те же 13 + контрольный блок 4), действует с 01.01.2027.
/// Точный формат уточняется при внедрении по действующей редакции постановления.
/// </summary>
public static class AccountNumberGenerator
{
    public const int PreviousLength = 13;
    public const int ModifiedLength = 17;
    public static readonly DateOnly ModifiedNumbersStart = new(2027, 1, 1);

    public static string Build(string balanceAccount, Guid clientId, int sequence, NumberKind kind)
    {
        var account = Normalize(balanceAccount, 4);
        var clientCode = Math.Abs(BitConverter.ToInt32(clientId.ToByteArray(), 0)) % 100000;
        var sequenceCode = Math.Abs(sequence) % 10000;
        var core = account + clientCode.ToString("D5") + sequenceCode.ToString("D4");

        if (kind == NumberKind.Previous)
        {
            return core;
        }

        var check = (core.Sum(c => c - '0') * 37 + 2027) % 10000;
        return core + check.ToString("D4");
    }

    private static string Normalize(string value, int length)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length == length)
        {
            return digits;
        }

        if (digits.Length > length)
        {
            return digits[..length];
        }

        return digits.PadRight(length, '0');
    }
}
