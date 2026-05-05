using System.ComponentModel;
using GitHub.Copilot.SDK;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using ThemeManagement.Data;
using ThemeManagement.Domain.Entities;

namespace ThemeManagement.Services;

/// <summary>
/// GitHub Copilot SDK を使って、テーマへのエンジニア最適アサインを支援するエージェントサービス。
/// </summary>
public interface ICopilotAgentService
{
    Task<CopilotAgentSession> CreateSessionAsync(CancellationToken cancellationToken = default);
    Task<bool> IsAvailableAsync();
}

public class CopilotAgentService : ICopilotAgentService, IAsyncDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICapacitySettings _capacitySettings;
    private readonly ILogger<CopilotAgentService> _logger;
    private CopilotClient? _client;
    private bool _started;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly string SystemPrompt = """
        あなたはエンジニア稼働管理システムのアシスタントです。
        テーマ（案件）にエンジニアを最適にアサインするお手伝いをします。

        利用可能なツール:
        - get_active_themes: アクティブなテーマ（案件）の一覧を取得します
        - get_active_engineers: アクティブなエンジニアの一覧を取得します
        - get_engineer_availability: 特定の年月におけるエンジニアの稼働余力を取得します
        - get_all_engineers_availability: 特定の年月における全エンジニアの稼働余力を一覧で取得します
        - get_theme_allocations: テーマの既存割り当て状況を取得します
        - assign_engineer_to_theme: エンジニアをテーマに割り当てます

        アサイン推奨時の考慮事項:
        1. エンジニアの残余稼働時間（余力）を確認する
        2. テーマの必要スキルとエンジニアのスキルをマッチングする
        3. スキルレベル（エンジニアのレベル >= テーマの要求レベル）を確認する
        4. テーマの受注金額上限と割り当てコストを確認する

        ユーザーがアサインを依頼したら、最適なエンジニアを提案し、確認後に assign_engineer_to_theme を実行してください。
        """;

    public CopilotAgentService(
        IServiceScopeFactory scopeFactory,
        ICapacitySettings capacitySettings,
        ILogger<CopilotAgentService> logger)
    {
        _scopeFactory = scopeFactory;
        _capacitySettings = capacitySettings;
        _logger = logger;
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            await EnsureStartedAsync();
            var response = await _client!.PingAsync();
            return response != null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Copilot SDK is not available");
            return false;
        }
    }

    public async Task<CopilotAgentSession> CreateSessionAsync(CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken);

        var tools = BuildTools();

        var session = await _client!.CreateSessionAsync(new SessionConfig
        {
            Model = "gpt-4o",
            Streaming = true,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Append,
                Content = SystemPrompt,
            },
            Tools = tools,
            OnPermissionRequest = PermissionHandler.ApproveAll,
        }, cancellationToken);

        return new CopilotAgentSession(session);
    }

    private async Task EnsureStartedAsync(CancellationToken cancellationToken = default)
    {
        if (_started && _client != null) return;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_started && _client != null) return;

            _client = new CopilotClient();
            await _client.StartAsync(cancellationToken);
            _started = true;
        }
        catch
        {
            _client = null;
            _started = false;
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static AIFunctionFactoryOptions ReadOnlyTool(string name, string description) =>
        new AIFunctionFactoryOptions
        {
            Name = name,
            Description = description,
            AdditionalProperties = new System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>(
                new Dictionary<string, object?> { ["skip_permission"] = true }),
        };

    private AIFunction[] BuildTools()
    {
        return
        [
            // ─── テーマ一覧取得 ───
            AIFunctionFactory.Create(
                async () =>
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var themes = await db.Themes
                        .AsNoTracking()
                        .Include(t => t.RequiredSkills).ThenInclude(rs => rs.Skill)
                        .Where(t => ThemeStatus.ActiveStatuses.Contains(t.Status))
                        .OrderBy(t => t.Name)
                        .ToListAsync();

                    return themes.Select(t => new
                    {
                        id = t.Id,
                        themeNo = t.ThemeNo,
                        name = t.Name,
                        status = t.Status,
                        orderType = t.OrderType,
                        orderAmount = t.OrderAmount,
                        estimatedCompletionDate = t.EstimatedCompletionDate.ToString("yyyy-MM-dd"),
                        requiredSkills = t.RequiredSkills.Select(rs => new
                        {
                            skillId = rs.SkillId,
                            skillName = rs.Skill.Name,
                            requiredLevel = rs.RequiredLevel,
                        }).ToList(),
                    }).ToList();
                },
                ReadOnlyTool("get_active_themes",
                    "アクティブなテーマ（案件）の一覧を取得します。テーマID、テーマ名、受注金額、必要スキルなどが含まれます。")),

            // ─── エンジニア一覧取得 ───
            AIFunctionFactory.Create(
                async () =>
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var engineers = await db.Engineers
                        .AsNoTracking()
                        .Include(e => e.Grade)
                        .Include(e => e.Skills).ThenInclude(es => es.Skill)
                        .Where(e => e.IsActive)
                        .OrderBy(e => e.Name)
                        .ToListAsync();

                    return engineers.Select(e => new
                    {
                        id = e.Id,
                        name = e.Name,
                        grade = e.Grade.Name,
                        unitSalePrice = e.Grade.UnitSalePrice,
                        skills = e.Skills.Select(es => new
                        {
                            skillId = es.SkillId,
                            skillName = es.Skill.Name,
                            level = es.Level,
                        }).ToList(),
                    }).ToList();
                },
                ReadOnlyTool("get_active_engineers",
                    "アクティブなエンジニアの一覧を取得します。エンジニアID、名前、等級、時間単価、スキル情報が含まれます。")),

            // ─── エンジニア稼働余力取得 ───
            AIFunctionFactory.Create(
                async (
                    [Description("エンジニアID")] int engineerId,
                    [Description("年（例: 2025）")] int year,
                    [Description("月（1〜12）")] int month) =>
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var allocationService = scope.ServiceProvider.GetRequiredService<IAllocationService>();

                    var engineer = await db.Engineers
                        .AsNoTracking()
                        .Include(e => e.Grade)
                        .FirstOrDefaultAsync(e => e.Id == engineerId);

                    if (engineer == null)
                        return (object)new { error = $"エンジニアID {engineerId} が見つかりません" };

                    var maxHours = allocationService.GetMaxDevelopableHours(engineerId, year, month);
                    var allocatedHours = allocationService.GetTotalAllocatedHoursByEngineer(engineerId, year, month);
                    var remainingHours = maxHours - allocatedHours;

                    return (object)new
                    {
                        engineerId,
                        engineerName = engineer.Name,
                        year,
                        month,
                        maxDevelopableHours = maxHours,
                        totalAllocatedHours = allocatedHours,
                        remainingHours,
                        workRate = maxHours > 0 ? Math.Round(allocatedHours / maxHours * 100, 1) : 0m,
                    };
                },
                ReadOnlyTool("get_engineer_availability",
                    "特定の年月における1名のエンジニアの稼働余力（最大稼働時間・割り当て済み時間・残余時間）を取得します。")),

            // ─── 全エンジニア稼働余力取得 ───
            AIFunctionFactory.Create(
                async (
                    [Description("年（例: 2025）")] int year,
                    [Description("月（1〜12）")] int month) =>
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var allocationService = scope.ServiceProvider.GetRequiredService<IAllocationService>();

                    var engineers = await db.Engineers
                        .AsNoTracking()
                        .Include(e => e.Grade)
                        .Include(e => e.Skills).ThenInclude(es => es.Skill)
                        .Where(e => e.IsActive)
                        .OrderBy(e => e.Name)
                        .ToListAsync();

                    return engineers.Select(e =>
                    {
                        var maxHours = allocationService.GetMaxDevelopableHours(e.Id, year, month);
                        var allocated = allocationService.GetTotalAllocatedHoursByEngineer(e.Id, year, month);
                        var remaining = maxHours - allocated;
                        return new
                        {
                            engineerId = e.Id,
                            engineerName = e.Name,
                            grade = e.Grade.Name,
                            unitSalePrice = e.Grade.UnitSalePrice,
                            skills = e.Skills.Select(es => new
                            {
                                skillId = es.SkillId,
                                skillName = es.Skill.Name,
                                level = es.Level,
                            }).ToList(),
                            maxDevelopableHours = maxHours,
                            totalAllocatedHours = allocated,
                            remainingHours = remaining,
                            workRate = maxHours > 0 ? Math.Round(allocated / maxHours * 100, 1) : 0m,
                        };
                    }).ToList();
                },
                ReadOnlyTool("get_all_engineers_availability",
                    "特定の年月における全アクティブエンジニアの稼働余力一覧を取得します。スキル情報や稼働率も含まれます。")),

            // ─── テーマの割り当て状況取得 ───
            AIFunctionFactory.Create(
                async (
                    [Description("テーマID")] int themeId,
                    [Description("年（例: 2025）")] int year,
                    [Description("月（1〜12）")] int month) =>
                {
                    using var scope = _scopeFactory.CreateScope();
                    var allocationService = scope.ServiceProvider.GetRequiredService<IAllocationService>();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var theme = await db.Themes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == themeId);
                    if (theme == null)
                        return (object)new { error = $"テーマID {themeId} が見つかりません" };

                    var allocations = await allocationService.GetByThemeAsync(themeId, year, month);
                    var totalCost = allocationService.GetTotalAllocatedCostByTheme(themeId);

                    return (object)new
                    {
                        themeId,
                        themeName = theme.Name,
                        orderAmount = theme.OrderAmount,
                        totalAllocatedCost = totalCost,
                        remainingBudget = theme.OrderAmount - totalCost,
                        allocations = allocations.Select(a => new
                        {
                            engineerName = a.EngineerName,
                            grade = a.GradeName,
                            hours = a.AllocatedHours,
                            cost = a.AllocatedHours * a.UnitSalePrice,
                        }).ToList(),
                    };
                },
                ReadOnlyTool("get_theme_allocations",
                    "テーマの特定月における割り当て状況（割り当てエンジニア・時間・コスト・予算残高）を取得します。")),

            // ─── エンジニアをテーマにアサイン ───
            AIFunctionFactory.Create(
                async (
                    [Description("エンジニアID")] int engineerId,
                    [Description("テーマID")] int themeId,
                    [Description("年（例: 2025）")] int year,
                    [Description("月（1〜12）")] int month,
                    [Description("割り当て時間（時間単位、0より大きい値）")] decimal hours) =>
                {
                    using var scope = _scopeFactory.CreateScope();
                    var allocationService = scope.ServiceProvider.GetRequiredService<IAllocationService>();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var engineer = await db.Engineers.AsNoTracking()
                        .Include(e => e.Grade)
                        .FirstOrDefaultAsync(e => e.Id == engineerId);
                    var theme = await db.Themes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == themeId);

                    if (engineer == null)
                        return new { success = false, message = $"エンジニアID {engineerId} が見つかりません" };
                    if (theme == null)
                        return new { success = false, message = $"テーマID {themeId} が見つかりません" };

                    try
                    {
                        await allocationService.UpsertAllocationAsync(engineerId, themeId, year, month, hours);
                        return new
                        {
                            success = true,
                            message = $"{engineer.Name} を {theme.Name} に {year}/{month:D2} {hours}h 割り当てました。",
                        };
                    }
                    catch (Exception ex)
                    {
                        return new { success = false, message = ex.Message };
                    }
                },
                new AIFunctionFactoryOptions
                {
                    Name = "assign_engineer_to_theme",
                    Description = "エンジニアを特定のテーマ・月に割り当てます（稼働上限・受注金額チェックあり）。",
                }),
        ];
    }

    public async ValueTask DisposeAsync()
    {
        if (_client != null)
        {
            try { await _client.StopAsync(); } catch { /* ignore */ }
            _client = null;
        }
        _lock.Dispose();
    }
}

/// <summary>
/// エージェントセッションのラッパー。メッセージ送受信と履歴管理を提供します。
/// </summary>
public class CopilotAgentSession : IAsyncDisposable
{
    private readonly CopilotSession _session;

    public CopilotAgentSession(CopilotSession session)
    {
        _session = session;
    }

    public string SessionId => _session.SessionId;

    /// <summary>
    /// メッセージを送信し、ストリーミングで応答を受け取ります。
    /// </summary>
    public async IAsyncEnumerable<string> SendAsync(
        string prompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = System.Threading.Channels.Channel.CreateUnbounded<string>();

        IDisposable? subscription = null;
        subscription = _session.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageDeltaEvent delta when !string.IsNullOrEmpty(delta.Data.DeltaContent):
                    channel.Writer.TryWrite(delta.Data.DeltaContent);
                    break;
                case SessionIdleEvent:
                    channel.Writer.TryComplete();
                    subscription?.Dispose();
                    break;
                case SessionErrorEvent err:
                    channel.Writer.TryComplete(new InvalidOperationException(err.Data.Message));
                    subscription?.Dispose();
                    break;
            }
        });

        await _session.SendAsync(new MessageOptions { Prompt = prompt }, cancellationToken);

        await foreach (var chunk in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return chunk;
        }
    }

    public ValueTask DisposeAsync() => _session.DisposeAsync();
}

