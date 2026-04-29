namespace ThemeManagement.Services;

public interface IJapaneseBusinessDayService
{
    int GetBusinessDays(int year, int month);
    List<DateOnly> GetHolidays(int year, int month);
}

public class JapaneseBusinessDayService : IJapaneseBusinessDayService
{
    public int GetBusinessDays(int year, int month)
    {
        var holidays = GetAllHolidays(year);
        int count = 0;
        int days = DateTime.DaysInMonth(year, month);
        for (int d = 1; d <= days; d++)
        {
            var date = new DateOnly(year, month, d);
            if (date.DayOfWeek != DayOfWeek.Saturday &&
                date.DayOfWeek != DayOfWeek.Sunday &&
                !holidays.Contains(date))
                count++;
        }
        return count;
    }

    public List<DateOnly> GetHolidays(int year, int month)
    {
        return GetAllHolidays(year)
            .Where(h => h.Year == year && h.Month == month)
            .OrderBy(h => h)
            .ToList();
    }

    private HashSet<DateOnly> GetAllHolidays(int year)
    {
        var holidays = new HashSet<DateOnly>();

        // --- 固定祝日 ---
        TryAdd(holidays, year, 1, 1);   // 元旦
        TryAdd(holidays, year, 2, 11);  // 建国記念の日
        TryAdd(holidays, year, 2, 23);  // 天皇誕生日
        TryAdd(holidays, year, 4, 29);  // 昭和の日
        TryAdd(holidays, year, 5, 3);   // 憲法記念日
        TryAdd(holidays, year, 5, 4);   // みどりの日
        TryAdd(holidays, year, 5, 5);   // こどもの日
        TryAdd(holidays, year, 8, 11);  // 山の日
        TryAdd(holidays, year, 11, 3);  // 文化の日
        TryAdd(holidays, year, 11, 23); // 勤労感謝の日

        // --- 移動祝日 ---
        AddNthWeekday(holidays, year, 1, DayOfWeek.Monday, 2);  // 成人の日（1月第2月曜）
        AddNthWeekday(holidays, year, 7, DayOfWeek.Monday, 3);  // 海の日（7月第3月曜）
        AddNthWeekday(holidays, year, 9, DayOfWeek.Monday, 3);  // 敬老の日（9月第3月曜）
        AddNthWeekday(holidays, year, 10, DayOfWeek.Monday, 2); // スポーツの日（10月第2月曜）

        // --- 春分の日・秋分の日（天文計算近似式） ---
        int springDay = GetEquinox(year, spring: true);
        int autumnDay = GetEquinox(year, spring: false);
        TryAdd(holidays, year, 3, springDay);
        TryAdd(holidays, year, 9, autumnDay);

        // --- 振替休日（祝日が日曜なら翌月曜） ---
        var baseHolidays = new HashSet<DateOnly>(holidays);
        foreach (var h in baseHolidays)
        {
            if (h.DayOfWeek == DayOfWeek.Sunday)
            {
                var furikae = h.AddDays(1);
                // 振替が他の祝日と重なる場合はさらに翌日に移動
                while (holidays.Contains(furikae))
                    furikae = furikae.AddDays(1);
                holidays.Add(furikae);
            }
        }

        // --- 国民の休日（祝日に挟まれた平日） ---
        var snapshot = new HashSet<DateOnly>(holidays);
        for (int m = 1; m <= 12; m++)
        {
            int daysInMonth = DateTime.DaysInMonth(year, m);
            for (int d = 2; d < daysInMonth; d++)
            {
                var date = new DateOnly(year, m, d);
                if (date.DayOfWeek != DayOfWeek.Saturday &&
                    date.DayOfWeek != DayOfWeek.Sunday &&
                    !snapshot.Contains(date) &&
                    snapshot.Contains(date.AddDays(-1)) &&
                    snapshot.Contains(date.AddDays(1)))
                {
                    holidays.Add(date);
                }
            }
        }

        return holidays;
    }

    /// <summary>
    /// 春分・秋分の日の日付を近似式で計算（1980〜2099年対応）
    /// </summary>
    private static int GetEquinox(int year, bool spring)
    {
        double c = year < 2000 ? (spring ? 20.8357 : 23.2588) : (spring ? 20.8431 : 23.2488);
        return (int)(c + 0.242194 * (year - 1980) - Math.Floor((year - 1980) / 4.0));
    }

    private static void TryAdd(HashSet<DateOnly> set, int year, int month, int day)
    {
        try { set.Add(new DateOnly(year, month, day)); } catch { }
    }

    private static void AddNthWeekday(HashSet<DateOnly> set, int year, int month, DayOfWeek dow, int n)
    {
        int count = 0;
        int days = DateTime.DaysInMonth(year, month);
        for (int d = 1; d <= days; d++)
        {
            var date = new DateOnly(year, month, d);
            if (date.DayOfWeek == dow && ++count == n)
            {
                set.Add(date);
                return;
            }
        }
    }
}
