namespace ThemeManagement.Models;

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
    int? EstimatedCompletionMonth
);
