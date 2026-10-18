namespace AtlantTools;

/// <summary>
/// Окно «Ввод нового» — одна позиция документа.
/// Приход: код материала (6 цифр), наименование, единица измерения (кодом), цена, количество.
/// Расход: позиция выбирается из остатков — код, единица и цена подставляются сами.
/// </summary>
public class LineForm : Form
{
    private sealed class StockItem
    {
        public StockRow Row { get; }
        public double Available { get; }

        public StockItem(StockRow row, double available)
        {
            Row = row;
            Available = available;
        }

        public override string ToString() =>
            $"{Row.MaterialCode} — {Row.MaterialName} | цена {Row.Price:N2} | остаток {Available:0.###} {Row.UnitName}";
    }

    private readonly bool isExpense;
    private readonly TextBox txtCode = new();
    private readonly TextBox txtName = new();
    private readonly ComboBox cmbUnit = new();
    private readonly TextBox txtUnit = new();
    private readonly TextBox txtPrice = new();
    private readonly TextBox txtQty = new();
    private readonly ComboBox cmbStock = new();
    private readonly Label lblSum = new();
    private readonly Label lblHint = new();
    private bool nameFromRef;

    /// <summary>Введённая позиция (после нажатия «Сохранить»).</summary>
    public DocLine? Result { get; private set; }

    public LineForm(bool isExpense, IReadOnlyList<DocLine> existing)
    {
        this.isExpense = isExpense;

        Text = "Ввод нового";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 10F);

        Controls.Add(new Label
        {
            Text = "ВВОД НОВОГО",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(0, 10),
            Size = new Size(520, 30)
        });

        int y = 52;

        void AddRow(string caption, Control c)
        {
            Controls.Add(new Label
            {
                Text = caption,
                AutoSize = false,
                Location = new Point(20, y + 3),
                Size = new Size(190, 24)
            });
            c.Location = new Point(215, y);
            c.Size = new Size(285, 27);
            Controls.Add(c);
            y += 44;
        }

        if (isExpense)
        {
            // Что ещё можно списать: остаток минус уже добавленное в этот документ
            var reserved = new Dictionary<int, double>();
            foreach (var l in existing)
            {
                if (l.StockId <= 0) continue;
                reserved.TryGetValue(l.StockId, out var already);
                reserved[l.StockId] = already + l.Quantity;
            }

            var items = new List<object>();
            foreach (var s in Database.GetStock(true))
            {
                reserved.TryGetValue(s.Id, out var used);
                var available = Math.Round(s.Quantity - used, 3);
                if (available > 0) items.Add(new StockItem(s, available));
            }

            cmbStock.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbStock.DropDownWidth = 640;
            cmbStock.Items.AddRange(items.ToArray());
            cmbStock.SelectedIndexChanged += (_, _) => OnStockSelected();
            AddRow("Позиция на складе", cmbStock);
        }

        txtCode.MaxLength = 6;
        txtCode.KeyPress += (_, e) =>
        {
            if (!char.IsControl(e.KeyChar) && (e.KeyChar < '0' || e.KeyChar > '9'))
                e.Handled = true;
        };
        txtName.MaxLength = 120;
        AddRow("1. Код материала", txtCode);
        AddRow("2. Наименование материала", txtName);

        if (isExpense)
        {
            txtUnit.ReadOnly = true;
            AddRow("3. Единица измерения", txtUnit);
            txtCode.ReadOnly = true;
            txtName.ReadOnly = true;
            txtPrice.ReadOnly = true;
        }
        else
        {
            cmbUnit.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (var u in Database.GetRef(RefKind.Units))
                cmbUnit.Items.Add(u);
            if (cmbUnit.Items.Count > 0) cmbUnit.SelectedIndex = 0;
            AddRow("3. Единица измерения", cmbUnit);

            txtName.ReadOnly = true; // станет доступно после ввода 6-значного кода
            txtCode.TextChanged += (_, _) => OnCodeChanged();
            lblHint.Text = "Введите 6-значный код материала.";
        }

        AddRow("4. Цена", txtPrice);
        AddRow("5. Количество", txtQty);

        lblSum.BorderStyle = BorderStyle.FixedSingle;
        lblSum.BackColor = SystemColors.Window;
        lblSum.TextAlign = ContentAlignment.MiddleRight;
        AddRow("6. Сумма", lblSum);

        txtPrice.TextChanged += (_, _) => UpdateSum();
        txtQty.TextChanged += (_, _) => UpdateSum();

        lblHint.ForeColor = Color.DimGray;
        lblHint.AutoSize = false;
        lblHint.Location = new Point(20, y - 4);
        lblHint.Size = new Size(480, 40);
        Controls.Add(lblHint);

        var btnOk = new Button { Text = "Сохранить", Location = new Point(280, y + 44), Size = new Size(110, 34) };
        var btnCancel = new Button
        {
            Text = "Отмена",
            Location = new Point(400, y + 44),
            Size = new Size(100, 34),
            DialogResult = DialogResult.Cancel
        };
        btnOk.Click += (_, _) => Save();
        Controls.Add(btnOk);
        Controls.Add(btnCancel);
        AcceptButton = btnOk;
        CancelButton = btnCancel;

        ClientSize = new Size(520, y + 96);
    }

    // ───────── приход: ввод кода ─────────

    private void OnCodeChanged()
    {
        var code = txtCode.Text.Trim();

        if (code.Length == 6 && UiHelper.IsDigits(code))
        {
            var name = Database.FindRefName(RefKind.Materials, code);
            if (name != null)
            {
                txtName.Text = name;
                txtName.ReadOnly = true;
                nameFromRef = true;
                lblHint.Text = "Материал найден в справочнике.";
            }
            else
            {
                if (nameFromRef) txtName.Clear();
                nameFromRef = false;
                txtName.ReadOnly = false;
                lblHint.Text = "Такого кода нет в справочнике — введите наименование, материал добавится как новый.";
            }
        }
        else
        {
            if (nameFromRef) txtName.Clear();
            nameFromRef = false;
            txtName.ReadOnly = true;
            lblHint.Text = "Введите 6-значный код материала.";
        }
    }

    // ───────── расход: выбор позиции из остатков ─────────

    private void OnStockSelected()
    {
        if (cmbStock.SelectedItem is not StockItem si) return;

        txtCode.Text = si.Row.MaterialCode;
        txtName.Text = si.Row.MaterialName;
        txtUnit.Text = $"{si.Row.UnitName} (код {si.Row.UnitCode})";
        txtPrice.Text = si.Row.Price.ToString("0.00");
        lblHint.Text = $"Доступно для списания: {si.Available:0.###} {si.Row.UnitName}.";
        UpdateSum();
    }

    private void UpdateSum()
    {
        if (UiHelper.TryParseNumber(txtPrice.Text, out var price) &&
            UiHelper.TryParseNumber(txtQty.Text, out var qty))
            lblSum.Text = Math.Round(price * qty, 2).ToString("N2");
        else
            lblSum.Text = "";
    }

    // ───────── сохранение ─────────

    private void Save()
    {
        if (isExpense)
        {
            if (cmbStock.SelectedItem is not StockItem si)
            {
                UiHelper.Warn("Выберите позицию на складе.");
                return;
            }
            if (!TryReadQty(out var qty)) return;

            if (qty > si.Available)
            {
                UiHelper.Warn($"Недостаточно остатка: доступно {si.Available:0.###} {si.Row.UnitName}.");
                txtQty.Focus();
                return;
            }

            Result = new DocLine
            {
                StockId = si.Row.Id,
                MaterialCode = si.Row.MaterialCode,
                MaterialName = si.Row.MaterialName,
                UnitCode = si.Row.UnitCode,
                UnitName = si.Row.UnitName,
                Price = si.Row.Price,
                Quantity = qty
            };
        }
        else
        {
            var code = txtCode.Text.Trim();
            var name = txtName.Text.Trim();

            if (code.Length != 6 || !UiHelper.IsDigits(code))
            {
                UiHelper.Warn("Код материала — ровно 6 цифр.");
                txtCode.Focus();
                return;
            }
            if (name.Length == 0)
            {
                UiHelper.Warn("Введите наименование материала.");
                txtName.Focus();
                return;
            }
            if (cmbUnit.SelectedItem is not RefItem unit)
            {
                UiHelper.Warn("Выберите единицу измерения.");
                return;
            }
            if (!UiHelper.TryParseNumber(txtPrice.Text, out var price) || price < 0 || price > 999_999_999)
            {
                UiHelper.Warn("Цена — число не меньше нуля.");
                txtPrice.Focus();
                return;
            }
            if (!TryReadQty(out var qty)) return;

            if (Database.FindRefName(RefKind.Materials, code) == null)
            {
                var ask = MessageBox.Show(
                    $"Материала с кодом {code} нет в справочнике.\nДобавить «{name}» как новый материал?",
                    "Новый материал", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (ask != DialogResult.Yes) return;

                var err = Database.AddRef(RefKind.Materials, code, name);
                if (err != null)
                {
                    UiHelper.Warn(err, "Ошибка");
                    return;
                }
            }

            Result = new DocLine
            {
                StockId = 0,
                MaterialCode = code,
                MaterialName = name,
                UnitCode = int.Parse(unit.Code, System.Globalization.CultureInfo.InvariantCulture),
                UnitName = unit.Name,
                Price = Math.Round(price, 2),
                Quantity = qty
            };
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private bool TryReadQty(out double qty)
    {
        if (!UiHelper.TryParseNumber(txtQty.Text, out qty) || qty <= 0 || qty > 999_999_999)
        {
            UiHelper.Warn("Количество — число больше нуля.");
            txtQty.Focus();
            return false;
        }
        qty = Math.Round(qty, 3);
        if (qty <= 0)
        {
            UiHelper.Warn("Количество слишком мало (минимум 0,001).");
            txtQty.Focus();
            return false;
        }
        return true;
    }
}
