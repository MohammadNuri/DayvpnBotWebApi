using System;
using System.Globalization;

namespace DayvpnBotWebApi.Shared
{
    public static class PersianHelper
    {
        public static string GetPersianCalendar(DateTime dateTime)
        {
            var pc = new PersianCalendar();
            return $"{pc.GetYear(dateTime)}/{pc.GetMonth(dateTime):00}/{pc.GetDayOfMonth(dateTime):00}";
        }
    }
}
