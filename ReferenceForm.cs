using System.Data;

namespace AtlantTools;

/// <summary>Справочник «Код — Расшифровка»: единицы измерения, цеха, виды документов, материалы.</summary>
public class ReferenceForm : Form
{
    private readonly RefKind kind;
    private readonly DataGridView grid = new();

    public ReferenceForm(RefKind kind, bool canEdit)
    {
        this.kind = kind;

        Text = RefMeta.Title(kind);
        ClientSize = new Size(640, 460);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10F);

        UiHelper.StyleGrid(grid);
        grid.Dock = DockStyle.Fill;
        Controls.Add(grid);

        if (canEdit)
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 48,
                Padding = new Padding(8)
            };
            var btnAdd = new Button { Text = "Добавить", Size = new Size(110, 30) };
            var btnEdit = new Button { Text = "Изменить", Size = new Size(110, 30) };
            var btnDelete = new Button { Text = "Удалить", Size = new Size(110, 30) };
            btnAdd.Click += (_, _) => ShowEditor(null);
            btnEdit.Click += (_, _) =>
            {
                var selected = Selected();
                if (selected != null) ShowEditor(selected);
            };
            btnDelete.Click += (_, _) => DeleteSelected();
            panel.Controls.AddRange(new Control[] { btnAdd, btnEdit, btnDelete });
            Controls.Add(panel);
        }

        LoadData();
    }

    private void LoadData()
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Code", typeof(string));
        table.Columns.Add("Name", typeof(string));

        foreach (var r in Database.GetRef(kind))
            table.Rows.Add(r.Id, r.Code, r.Name);

        grid.DataSource = table;
        grid.Columns["Id"]!.Visible = false;
        grid.Columns["Code"]!.HeaderText = RefMeta.CodeTitle(kind);
        grid.Columns["Name"]!.HeaderText = RefMeta.NameTitle(kind);
    }

    private RefItem? Selected()
    {
        var row = grid.CurrentRow;
        if (row == null) return null;
        return new RefItem
        {
            Id = Convert.ToInt32(row.Cells["Id"].Value),
            Code = row.Cells["Code"].Value?.ToString() ?? "",
            Name = row.Cells["Name"].Value?.ToString() ?? ""
        };
    }

    private void DeleteSelected()
    {
        var item = Selected();
        if (item == null) return;

        if (MessageBox.Show($"Удалить запись «{item.Name}» (код {item.Code})?", "Подтверждение",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        var err = Database.DeleteRef(kind, item.Id);
        if (err != null)
            UiHelper.Warn(err, "Удаление невозможно");
        LoadData();
    }

    /// <summary>Окно добавления (item == null) или изменения записи.</summary>
    private void ShowEditor(RefItem? item)
    {
        int fixedLen = RefMeta.FixedCodeLength(kind);

        using var dlg = new Form
        {
            Text = item == null ? "Новая запись" : "Изменение записи",
            ClientSize = new Size(380, 230),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            Font = Font
        };

        var txtCode = new TextBox
        {
            Location = new Point(20, 35),
            Size = new Size(160, 27),
            MaxLength = fixedLen > 0 ? fixedLen : 9,
            Text = item?.Code ?? ""
        };
        txtCode.KeyPress += (_, e) =>
        {
            if (!char.IsControl(e.KeyChar) && (e.KeyChar < '0' || e.KeyChar > '9'))
                e.Handled = true;
        };

        var txtName = new TextBox
        {
            Location = new Point(20, 95),
            Size = new Size(340, 27),
            MaxLength = 120,
            Text = item?.Name ?? ""
        };

        var btnOk = new Button { Text = "Сохранить", Location = new Point(130, 165), Size = new Size(110, 32) };
        var btnCancel = new Button
        {
            Text = "Отмена",
            Location = new Point(250, 165),
            Size = new Size(110, 32),
            DialogResult = DialogResult.Cancel
        };

        btnOk.Click += (_, _) =>
        {
            var code = txtCode.Text.Trim();
            var name = txtName.Text.Trim();

            if (!UiHelper.IsDigits(code) || (fixedLen > 0 && code.Length != fixedLen))
            {
                UiHelper.Warn(fixedLen > 0
                    ? $"Код должен состоять ровно из {fixedLen} цифр."
                    : "Код — число (только цифры, до 9 знаков).");
                txtCode.Focus();
                return;
            }
            if (name.Length == 0)
            {
                UiHelper.Warn("Введите название.");
                txtName.Focus();
                return;
            }

            code = RefMeta.NormalizeCode(kind, code);
            var err = item == null
                ? Database.AddRef(kind, code, name)
                : Database.UpdateRef(kind, item.Id, code, name);

            if (err != null)
            {
                UiHelper.Warn(err, "Ошибка");
                return;
            }
            dlg.DialogResult = DialogResult.OK;
        };

        dlg.Controls.AddRange(new Control[]
        {
            new Label { Text = RefMeta.CodeTitle(kind), Location = new Point(20, 12), AutoSize = true },
            txtCode,
            new Label { Text = RefMeta.NameTitle(kind), Location = new Point(20, 72), AutoSize = true },
            txtName,
            btnOk, btnCancel
        });
        dlg.AcceptButton = btnOk;
        dlg.CancelButton = btnCancel;

        if (dlg.ShowDialog(this) == DialogResult.OK)
            LoadData();
    }
}
