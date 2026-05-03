namespace ThemeManagement.Models;

public record DashboardKpiDto(
    int ActiveEngineerCount,
    int ActiveThemeCount,
    decimal AverageWorkRate,
    decimal TotalMonthlyCost
);

public record AllocationImportData(int EngineerId, int ThemeId, int Year, int Month, decimal Hours);

public record AllocationRowDto(
    int Id,
    int EngineerId,
    string EngineerName,
    int GradeId,
    string GradeName,
    decimal UnitSalePrice,
    int ThemeId,
    string ThemeName,
    int Year,
    int Month,
    decimal AllocatedHours
);

public record EngineerWorkSummaryDto(
    int EngineerId,
    string EngineerName,
    string GradeName,
    decimal MaxDevelopableHours,
    decimal TotalAllocatedHours,
    decimal RemainingHours,
    decimal WorkRate
);

public record ThemeProgressDto(
    int ThemeId,
    string ThemeName,
    string Status,
    decimal OrderAmount,
    decimal TotalAllocatedCost,
    decimal CarryOverAmount,
    decimal ProgressRate,
    decimal RemainingAmount,
    int? EstimatedCompletionYear,
    int? EstimatedCompletionMonth,
    DateOnly? EstimatedCompletionDate
);

public enum AlertSeverity
{
    Error,
    Warning,
    Info
}

public record AlertItemDto(
    AlertSeverity Severity,
    string Category,
    string Message
);
