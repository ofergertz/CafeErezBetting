using CafeErezBetting.Core.DTOs;

namespace CafeErezBetting.Core.Services;

public static class LotteryValidationService
{
    public static void ValidateLotto(SubmitLottoFormDto dto)
    {
        if (dto.Rows.Count == 0)
            throw new ArgumentException("At least one row is required");

        foreach (var row in dto.Rows)
        {
            if (row.Numbers.Count != 6)
                throw new ArgumentException("Each Lotto row must have exactly 6 numbers");
            if (row.Numbers.Any(n => n < 1 || n > 37))
                throw new ArgumentException("Lotto numbers must be between 1 and 37");
            if (row.Numbers.Distinct().Count() != 6)
                throw new ArgumentException("Lotto numbers must be unique");
            if (row.Strong < 1 || row.Strong > 7)
                throw new ArgumentException("Strong number must be between 1 and 7");
        }
    }

    public static void ValidateChance(SubmitChanceFormDto dto)
    {
        if (dto.Rows.Count == 0)
            throw new ArgumentException("At least one row is required");

        foreach (var row in dto.Rows)
        {
            if (row.Numbers.Count != 5)
                throw new ArgumentException("Each Chance row must have exactly 5 numbers");
            if (row.Numbers.Any(n => n < 1 || n > 36))
                throw new ArgumentException("Chance numbers must be between 1 and 36");
            if (row.Numbers.Distinct().Count() != 5)
                throw new ArgumentException("Chance numbers must be unique");
        }
    }

    public static void ValidateLucky777(SubmitLucky777FormDto dto)
    {
        if (dto.Rows.Count == 0)
            throw new ArgumentException("At least one row is required");

        foreach (var row in dto.Rows)
        {
            if (row.Numbers.Count != 7)
                throw new ArgumentException("Each 777 row must have exactly 7 numbers");
            if (row.Numbers.Any(n => n < 1 || n > 70))
                throw new ArgumentException("777 numbers must be between 1 and 70");
            if (row.Numbers.Distinct().Count() != 7)
                throw new ArgumentException("777 numbers must be unique");
        }
    }

    public static void ValidateToto(SubmitTotoFormDto dto, int expectedMatchCount)
    {
        if (dto.Columns.Count == 0)
            throw new ArgumentException("At least one column is required");
        if (dto.Columns.Count > 14)
            throw new ArgumentException("Maximum 14 columns allowed");

        foreach (var col in dto.Columns)
        {
            if (col.Picks.Count != expectedMatchCount)
                throw new ArgumentException("All matches must be filled in each column");

            foreach (var pick in col.Picks.Values)
                if (pick != "1" && pick != "X" && pick != "2")
                    throw new ArgumentException($"Invalid pick value: {pick}");
        }
    }
}
