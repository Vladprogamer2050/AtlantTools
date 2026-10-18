using System.Data;

namespace AtlantTools;

internal static class GridFormat
{
    public static void Apply(DataGridView g, string column, string format, bool rightAlign = true)
    {
        var c = g.Columns[column];
        if (c == null) return;
        c.DefaultCellStyle.Format = format;
        if (rightAlign)
            c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
    }
}

/// <summary>Просмотр таблицы «Остатки».</summary>
public class StockForm : Form
{
    private readonly DataGridView grid = new();
    private readonly CheckBox chkPositive = new();
    private readonly Label lblTotal = new();

    public StockForm()
    {
        Text = "Остатки";
        ClientSize = new Size(1000, 520);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10F);

        UiHelper.StyleGrid(grid);
        grid.Dock = DockStyle.Fill;
        Controls.Add(grid);

        var panel = new Panel { Dock = DockStyle.Top, Height = 44 };
        chkPositive.Text = "Только позиции с остатком";
        chkPositive.Checked = true;
        chkPositive.AutoSize = true;
        chkPositive.Location = new Point(12, 11);
        chkPositive.CheckedChanged += (_, _) => LoadData();

        lblTotal.AutoSize = true;
        lblTotal.Location = new Point(300, 12);

        panel.Controls.Add(chkPositive);
        panel.Controls.Add(lblTotal);
        Controls.Add(panel);

        LoadData();
    }

    private void LoadData()
    {
        var t = new DataTable();
        t.Columns.Add("Код материала", typeof(string));
        t.Columns.Add("Наименование", typeof(string));
        t.Columns.Add("Цена", typeof(double));
        t.Columns.Add("Количество", typeof(double));
        t.Columns.Add("Код ед.", typeof(int));
        t.Columns.Add("Ед. изм.", typeof(string));
        t.Columns.Add("Сумма", typeof(double));
        t.Columns.Add("Дата", typeof(DateTime));

        double total = 0;
        int count = 0;
        foreach (var s in Database.GetStock(chkPositive.Checked))
        {
            var sum = Math.Round(s.Price * s.Quantity, 2);
            total += sum;
            count++;
            t.Rows.Add(s.MaterialCode, s.MaterialName, s.Price, s.Quantity,
                s.UnitCode, s.UnitName, sum, UiHelper.ParseDbDate(s.Date));
        }

        grid.DataSource = t;
        GridFormat.Apply(grid, "Цена", "N2");
        GridFormat.Apply(grid, "Количество", "N3");
        GridFormat.Apply(grid, "Сумма", "N2");
        GridFormat.Apply(grid, "Дата", "dd.MM.yyyy", false);

        lblTotal.Text = $"Позиций: {count}      Общая сумма: {total:N2}";
    }
}

/// <summary>Просмотр журнала «Движение» за период.</summary>
public class MovementsForm : Form
{
    private readonly DataGridView grid = new();
    private readonly DateTimePicker dtpFrom = new();
    private readonly DateTimePicker dtpTo = new();
    private readonly Label lblCount = new();

    public MovementsForm()
    {
        Text = "Движение";
        ClientSize = new Size(1150, 540);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10F);

        UiHelper.StyleGrid(grid);
        grid.Dock = DockStyle.Fill;
        Controls.Add(grid);

        var panel = new Panel { Dock = DockStyle.Top, Height = 48 };

        dtpFrom.Format = DateTimePickerFormat.Custom;
        dtpFrom.CustomFormat = "dd.MM.yyyy";
        dtpFrom.Location = new Point(40, 10);
        dtpFrom.Size = new Size(120, 27);
        dtpFrom.Value = new DateTime(DateTime.Today.Year, 1, 1);

        dtpTo.Format = DateTimePickerFormat.Custom;
        dtpTo.CustomFormat = "dd.MM.yyyy";
        dtpTo.Location = new Point(205, 10);
        dtpTo.Size = new Size(120, 27);
        dtpTo.Value = DateTime.Today;

        var btnShow = new Button { Text = "Показать", Location = new Point(345, 8), Size = new Size(110, 30) };
        btnShow.Click += (_, _) => LoadData();

        lblCount.AutoSize = true;
        lblCount.Location = new Point(475, 13);

        panel.Controls.Add(new Label { Text = "С", Location = new Point(14, 14), AutoSize = true });
        panel.Controls.Add(dtpFrom);
        panel.Controls.Add(new Label { Text = "по", Location = new Point(175, 14), AutoSize = true });
        panel.Controls.Add(dtpTo);
        panel.Controls.Add(btnShow);
        panel.Controls.Add(lblCount);
        Controls.Add(panel);

        LoadData();
    }

    private void LoadData()
    {
        if (dtpFrom.Value.Date > dtpTo.Value.Date)
        {
            UiHelper.Warn("Начало периода позже его конца.");
            return;
        }

        var t = Database.GetMovementsTable(dtpFrom.Value.Date, dtpTo.Value.Date);
        grid.DataSource = t;
        GridFormat.Apply(grid, "Дата", "dd.MM.yyyy", false);
        GridFormat.Apply(grid, "Количество", "N3");
        GridFormat.Apply(grid, "Цена", "N2");
        GridFormat.Apply(grid, "Сумма", "N2");

        lblCount.Text = $"Записей движения: {t.Rows.Count}";
    }
}
