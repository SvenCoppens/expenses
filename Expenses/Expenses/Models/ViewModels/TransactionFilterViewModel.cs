using Expenses.Models.DTOs;
using Expenses.Models.Enums;

namespace Expenses.Models.ViewModels;

public class TransactionFilterViewModel
{
    public const string PresetThisMonth = "ThisMonth";
    public const string PresetLastMonth = "LastMonth";
    public const string PresetYearToDate = "YearToDate";
    public const string PresetAllTime = "AllTime";
    public const string PresetCustom = "Custom";

    public string DatePreset { get; set; } = PresetThisMonth;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public TransactionType? Type { get; set; }
    public Guid? SelectedCategoryId { get; set; }
    public string? SearchTerm { get; set; }

    public TransactionFilterViewModel(DateOnly? referenceDate = null)
    {
        ApplyPreset(PresetThisMonth, referenceDate);
    }

    public void ApplyPreset(string preset, DateOnly? referenceDate = null)
    {
        var today = referenceDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        DatePreset = preset;

        if (string.Equals(preset, PresetThisMonth, StringComparison.OrdinalIgnoreCase))
        {
            StartDate = new DateOnly(today.Year, today.Month, 1);
            EndDate = StartDate.Value.AddMonths(1).AddDays(-1);
        }
        else if (string.Equals(preset, PresetLastMonth, StringComparison.OrdinalIgnoreCase))
        {
            var firstLastMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
            StartDate = firstLastMonth;
            EndDate = firstLastMonth.AddMonths(1).AddDays(-1);
        }
        else if (string.Equals(preset, PresetYearToDate, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(preset, "YTD", StringComparison.OrdinalIgnoreCase))
        {
            StartDate = new DateOnly(today.Year, 1, 1);
            EndDate = today;
        }
        else if (string.Equals(preset, PresetAllTime, StringComparison.OrdinalIgnoreCase))
        {
            StartDate = null;
            EndDate = null;
        }
        else if (string.Equals(preset, PresetCustom, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(preset, "Custom Range", StringComparison.OrdinalIgnoreCase))
        {
            // Custom mode leaves StartDate and EndDate as configured by the user
        }
    }

    public void Reset(DateOnly? referenceDate = null)
    {
        Type = null;
        SelectedCategoryId = null;
        SearchTerm = null;
        ApplyPreset(PresetThisMonth, referenceDate);
    }

    public TransactionFilter ToFilter() => new(
        StartDate: StartDate,
        EndDate: EndDate,
        CategoryIds: SelectedCategoryId.HasValue ? [SelectedCategoryId.Value] : null,
        Type: Type,
        SearchTerm: string.IsNullOrWhiteSpace(SearchTerm) ? null : SearchTerm.Trim()
    );
}
