using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using eStarter.Core;
using eStarter.Models;

namespace eStarter.ViewModels
{
    public class CalendarViewModel : INotifyPropertyChanged
    {
        private DateTime _currentDate;
        private string _headerText = string.Empty;

        public ObservableCollection<CalendarDay> Days { get; } = new ObservableCollection<CalendarDay>();
        public ObservableCollection<string> WeekDays { get; } = new ObservableCollection<string> 
        { 
            "星期一", "星期二", "星期三", "星期四", "星期五", "星期六", "星期日" 
        };

        public string HeaderText
        {
            get => _headerText;
            set { _headerText = value; OnPropertyChanged(); }
        }

        public DateTime CurrentDate
        {
            get => _currentDate;
            set
            {
                if (_currentDate != value)
                {
                    _currentDate = value;
                    OnPropertyChanged();
                    GenerateCalendar();
                }
            }
        }

        public ICommand PreviousMonthCommand { get; }
        public ICommand NextMonthCommand { get; }
        public ICommand GoToTodayCommand { get; }

        public CalendarViewModel()
        {
            CurrentDate = DateTime.Today;
            PreviousMonthCommand = new RelayCommand(_ => NavigateMonth(-1));
            NextMonthCommand = new RelayCommand(_ => NavigateMonth(1));
            GoToTodayCommand = new RelayCommand(_ => { CurrentDate = DateTime.Today; });
        }

        private void NavigateMonth(int months)
        {
            CurrentDate = CurrentDate.AddMonths(months);
        }

        private void GenerateCalendar()
        {
            HeaderText = _currentDate.ToString("yyyy年M月");
            Days.Clear();

            var firstDayOfMonth = new DateTime(_currentDate.Year, _currentDate.Month, 1);
            var daysInMonth = DateTime.DaysInMonth(_currentDate.Year, _currentDate.Month);
            
            // Calculate start date (find the previous Monday)
            // DayOfWeek: Sunday=0, Monday=1, ... Saturday=6
            // We want Monday to be the start.
            int offset = (int)firstDayOfMonth.DayOfWeek - 1;
            if (offset < 0) offset = 6; // If Sunday, offset is 6 days back

            var startDate = firstDayOfMonth.AddDays(-offset);

            // Generate 42 days (6 weeks) to cover all possibilities
            for (int i = 0; i < 42; i++)
            {
                var date = startDate.AddDays(i);
                var day = new CalendarDay
                {
                    Date = date,
                    IsCurrentMonth = date.Month == _currentDate.Month,
                    IsToday = date.Date == DateTime.Today
                };

                // Add dummy events for demo
                if (date.DayOfWeek == DayOfWeek.Thursday && date.Day % 2 == 0)
                {
                    day.Events.Add(new CalendarEvent { Title = "劳动节(第一天)", Color = "#FF800080" });
                }
                if (date.Day == 4 && date.Month == 5)
                {
                    day.Events.Add(new CalendarEvent { Title = "青年节", Color = "#FF800080" });
                }
                if (date.DayOfWeek == DayOfWeek.Friday && date.Day > 15)
                {
                    day.Events.Add(new CalendarEvent { Title = "农历 甲午年 【马年】...", Color = "#FF800080" });
                    if (date.Day == 16)
                        day.Events.Add(new CalendarEvent { Title = "早饭", Time = "6:35", Color = "#FF6495ED" });
                }

                Days.Add(day);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
