using System.Globalization;

namespace AtlantTools;

/// <summary>Мелкие общие помощники для окон.</summary>
internal static class UiHelper
{
    /// <summary>Единый вид таблиц: только чтение, выделение строки целиком.</summary>
    public static void StyleGrid(DataGridView g)
    {
        g.ReadOnly = true;
        g.AllowUserToAddRows = false;
        g.AllowUserToDeleteRows = false;
        g.AllowUserToResizeRows = false;
        g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        g.MultiSelect = false;
        g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        g.RowHeadersVisible = false;
        g.BackgroundColor = SystemColors.Window;
    }

    /// <summary>Разбор числа: принимает и запятую, и точку, и пробелы-разделители.</summary>
    public static bool TryParseNumber(string? text, out double value)
    {
        var s = (text ?? "").Trim().Replace(" ", "").Replace(" ", "").Replace(',', '.');
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
               && !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>Строка состоит только из цифр 0-9 (и не пустая).</summary>
    public static bool IsDigits(string s)
    {
        if (s.Length == 0) return false;
        foreach (var c in s)
            if (c < '0' || c > '9') return false;
        return true;
    }

    /// <summary>Дата хранится в БД как текст yyyy-MM-dd.</summary>
    public static DateTime ParseDbDate(string s)
    {
        return DateTime.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public static void Warn(string message, string title = "Проверьте данные")
    {
        MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
