using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Deceive;

/// Settings for the optional "agendamento" feature: automatically switches between
/// two statuses at fixed times on the chosen days of the week.
internal sealed class Schedule
{
    internal bool Enabled { get; set; }

    /// Which days of the week the schedule applies to.
    internal bool[] Days { get; set; } = new bool[7]; // index = (int)DayOfWeek

    /// Local time the "window" status starts.
    internal TimeSpan StartTime { get; set; } = new(20, 0, 0);

    /// Local time the "window" status ends. May be earlier than StartTime (window crosses midnight).
    internal TimeSpan EndTime { get; set; } = new(23, 0, 0);

    /// Status applied while inside the window: "offline" or "mobile".
    internal string WindowStatus { get; set; } = "offline";

    /// Status applied outside the window: "chat" (online) or "offline".
    internal string OutsideStatus { get; set; } = "chat";

    private static readonly string Path_ = System.IO.Path.Combine(Persistence.DataDir, "schedule.txt");

    /// Whether `when` falls inside the configured window. For a window that wraps past
    /// midnight (StartTime > EndTime), the day-of-week check applies to the day the window
    /// STARTED on: e.g. a Tuesday 22:00-02:00 window still counts as "inside" at 01:00 on
    /// Wednesday, because that's a continuation of Tuesday night, not a Wednesday window.
    internal bool IsInsideWindow(DateTime when)
    {
        if (!Enabled)
            return false;

        var time = when.TimeOfDay;

        if (StartTime <= EndTime)
            return Days[(int)when.DayOfWeek] && time >= StartTime && time < EndTime;

        if (time >= StartTime)
            return Days[(int)when.DayOfWeek];

        if (time < EndTime)
        {
            var previousDay = ((int)when.DayOfWeek + 6) % 7;
            return Days[previousDay];
        }

        return false;
    }

    internal static Schedule Load()
    {
        var schedule = new Schedule();
        if (!File.Exists(Path_))
            return schedule;

        try
        {
            var lines = File.ReadAllLines(Path_);
            foreach (var line in lines)
            {
                var parts = line.Split(new[] { '=' }, 2);
                if (parts.Length != 2)
                    continue;

                switch (parts[0])
                {
                    case "enabled":
                        schedule.Enabled = parts[1] == "1";
                        break;
                    case "days":
                        var flags = parts[1].Split(',');
                        for (var i = 0; i < 7 && i < flags.Length; i++)
                            schedule.Days[i] = flags[i] == "1";
                        break;
                    case "start":
                        if (TimeSpan.TryParse(parts[1], CultureInfo.InvariantCulture, out var start))
                            schedule.StartTime = start;
                        break;
                    case "end":
                        if (TimeSpan.TryParse(parts[1], CultureInfo.InvariantCulture, out var end))
                            schedule.EndTime = end;
                        break;
                    case "windowStatus":
                        schedule.WindowStatus = parts[1];
                        break;
                    case "outsideStatus":
                        schedule.OutsideStatus = parts[1];
                        break;
                }
            }
        }
        catch
        {
            // If the file is corrupt for any reason, fall back to defaults.
            return new Schedule();
        }

        return schedule;
    }

    internal void Save()
    {
        var lines = new[]
        {
            "enabled=" + (Enabled ? "1" : "0"),
            "days=" + string.Join(",", Days.Select(d => d ? "1" : "0")),
            "start=" + StartTime.ToString("c", CultureInfo.InvariantCulture),
            "end=" + EndTime.ToString("c", CultureInfo.InvariantCulture),
            "windowStatus=" + WindowStatus,
            "outsideStatus=" + OutsideStatus
        };
        File.WriteAllLines(Path_, lines);
    }
}
