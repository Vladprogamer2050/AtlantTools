using System.Globalization;

namespace AtlantTools;

/// <summary>
/// Ввод документа: Приходный ордер (от поставщика / со служб завода) или Требование-накладная.
/// Каждая позиция при проведении становится отдельной записью движения.
/// </summary>
public class DocumentForm : Form
{
    private readonly int docTypeCode;
    private readonly bool isExpense;
    private readonly List<DocLine> lines = new();

    private readonly TextBox txtNumber = new();
    private readonly DateTimePicker dtpDate = new();
    private readonly ComboBox cmbDept = new();
    private readonly DataGridView grid = new();
    private readonly Label lblTotal = new();

    public DocumentForm(int docTypeCode)
    {
        this.docTypeCode = docTypeCode;
        isExpense = DocCodes.IsExpense(docTypeCode);

        var docName = Database.FindRefName(RefKind.DocTypes,
            docTypeCode.ToString(CultureInfo.InvariantCulture)) ?? "Документ";

        Text = $"{docName} — ввод документа";
        ClientSize = new Size(880, 560);
        MinimumSize = new Size(760, 480);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10F);

        // ── шапка документа ──
        Controls.Add(new Label { Text = "Номер документа", Location = new Point(20, 19), AutoSize = true });
        txtNumber.Location = new Point(150, 15);
        txtNumber.Size = new Size(110, 27);
        txtNumber.MaxLength = 20;
        txtNumber.Text = Database.NextDocNumber(docTypeCode);
        Controls.Add(txtNumber);

        Controls.Add(new Label { Text = "Дата", Location = new Point(285, 19), AutoSize = true });
        dtpDate.Format = DateTimePickerFormat.Custom;
        dtpDate.CustomFormat = "dd.MM.yyyy";
        dtpDate.Location = new Point(330, 15);
        dtpDate.Size = new Size(125, 27);
        dtpDate.Value = DateTime.Today;
        Controls.Add(dtpDate);

        if (isExpense)
        {
            Controls.Add(new Label { Text = DocCodes.RecipientLabel(docTypeCode), Location = new Point(485, 19), AutoSize = true });
            cmbDept.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbDept.Location = new Point(610, 15);
            cmbDept.Size = new Size(250, 27);
            foreach (var d in Database.GetRef(RefKind.Departments))
                cmbDept.Items.Add(d);
            if (cmbDept.Items.Count > 0) cmbDept.SelectedIndex = 0;
            Controls.Add(cmbDept);
        }

        // ── кнопки над таблицей ──
        var btnAdd = new Button { Text = "Добавить позицию", Location = new Point(20, 58), Size = new Size(170, 32) };
        var btnDel = new Button { Text = "Удалить позицию", Location = new Point(200, 58), Size = new Size(160, 32) };
        btnAdd.Click += (_, _) => AddLine();
        btnDel.Click += (_, _) => DeleteLine();
        Controls.Add(btnAdd);
        Controls.Add(btnDel);

        // ── таблица позиций ──
        UiHelper.StyleGrid(grid);
        grid.Location = new Point(20, 100);
        grid.Size = new Size(840, 345);
        grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        AddColumn("code", "Код материала");
        AddColumn("name", "Наименование");
        AddColumn("unit", "Ед. изм.");
        AddColumn("qty", "Количество", "N3");
        AddColumn("price", "Цена", "N2");
        AddColumn("sum", "Сумма", "N2");
        Controls.Add(grid);

        // ── низ окна ──
        lblTotal.AutoSize = true;
        lblTotal.Location = new Point(20, 460);
        lblTotal.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        Controls.Add(lblTotal);

        var btnPost = new Button
        {
            Text = "Провести документ",
            Location = new Point(560, 505),
            Size = new Size(180, 36),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        var btnCancel = new Button
        {
            Text = "Отмена",
            Location = new Point(750, 505),
            Size = new Size(110, 36),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            DialogResult = DialogResult.Cancel
        };
        btnPost.Click += (_, _) => Post();
        Controls.Add(btnPost);
        Controls.Add(btnCancel);
        CancelButton = btnCancel;

        // Не даём случайно потерять введённые позиции
        FormClosing += (_, e) =>
        {
            if (DialogResult != DialogResult.OK && lines.Count > 0)
            {
                var ask = MessageBox.Show("Документ не проведён. Закрыть окно и потерять введённые позиции?",
                    "Закрытие", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (ask != DialogResult.Yes) e.Cancel = true;
            }
        };

        RefreshGrid();
    }

    private void AddColumn(string name, string header, string? format = null)
    {
        int i = grid.Columns.Add(name, header);
        if (format != null)
        {
            grid.Columns[i].DefaultCellStyle.Format = format;
            grid.Columns[i].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
    }

    private void RefreshGrid()
    {
        grid.Rows.Clear();
        foreach (var l in lines)
            grid.Rows.Add(l.MaterialCode, l.MaterialName, l.UnitName, l.Quantity, l.Price, l.Sum);

        lblTotal.Text = $"Позиций: {lines.Count}      Итого: {lines.Sum(l => l.Sum):N2}";
    }

    private void AddLine()
    {
        if (isExpense && Database.GetStock(true).Count == 0)
        {
            UiHelper.Warn("На складе нет остатков, списывать нечего.", "Нет остатков");
            return;
        }

        using var f = new LineForm(isExpense, lines);
        if (f.ShowDialog(this) == DialogResult.OK && f.Result != null)
        {
            lines.Add(f.Result);
            RefreshGrid();
        }
    }

    private void DeleteLine()
    {
        var row = grid.CurrentRow;
        if (row == null || row.Index < 0 || row.Index >= lines.Count) return;

        lines.RemoveAt(row.Index);
        RefreshGrid();
    }

    private void Post()
    {
        var number = txtNumber.Text.Trim();
        if (number.Length == 0)
        {
            UiHelper.Warn("Укажите номер документа.");
            txtNumber.Focus();
            return;
        }

        int? dept = null;
        if (isExpense)
        {
            if (cmbDept.SelectedItem is not RefItem d)
            {
                UiHelper.Warn("Выберите получателя.");
                return;
            }
            dept = int.Parse(d.Code, CultureInfo.InvariantCulture);
        }

        var err = Database.PostDocument(dtpDate.Value.Date, number, docTypeCode, isExpense, dept, lines);
        if (err != null)
        {
            UiHelper.Warn(err, "Документ не проведён");
            return;
        }

        MessageBox.Show($"Документ № {number} проведён. Позиций: {lines.Count}.",
            "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
        Close();
    }
}
