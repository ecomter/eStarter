using System;
using System.Collections.Generic;

namespace eStarter.Models
{
    public class CalendarEvent
    {
        public string Title { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string Color { get; set; } = "#FF800080"; // Purple
    }

    public class CalendarDay
    {
        public DateTime Date { get; set; }
        public string DayNumber => Date.Day.ToString();
        public bool IsCurrentMonth { get; set; }
        public bool IsToday { get; set; }
        public List<CalendarEvent> Events { get; set; } = new List<CalendarEvent>();
    }
}
