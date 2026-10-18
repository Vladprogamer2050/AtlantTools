namespace AtlantTools;

/// <summary>Главное окно администратора: управление учётными записями.</summary>
public class AdminForm : Form
{
    private readonly AppUser currentUser;
    private readonly DataGridView grid = new();

    public AdminForm(AppUser user)
    {
        currentUser = user;

        Text = "ЗАО «Атлант» — Администратор";
        ClientSize = new Size(900, 560);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10F);

        var status = new StatusStrip();
        status.Items.Add(new ToolStripStatusLabel($"Вы вошли как: {user.FullName} ({user.RoleTitle})"));

        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("Файл");
        var switchUser = new ToolStripMenuItem("Сменить пользователя");
        switchUser.Click += (_, _) => { Tag = "logout"; Close(); };
        var exit = new ToolStripMenuItem("Выход");
        exit.Click += (_, _) => Close();
        file.DropDownItems.Add(switchUser);
        file.DropDownItems.Add(exit);
        menu.Items.Add(file);

        // Справочники (коды) — правит только администратор
        var refs = new ToolStripMenuItem("Справочники");
        foreach (var kind in new[] { RefKind.Materials, RefKind.Units, RefKind.Departments, RefKind.DocTypes })
        {
            var k = kind;
            refs.DropDownItems.Add(RefMeta.Title(k), null, (_, _) =>
            {
                using var f = new ReferenceForm(k, true);
                f.ShowDialog(this);
            });
        }
        menu.Items.Add(refs);

        var view = new ToolStripMenuItem("Просмотр");
        view.DropDownItems.Add("Остатки", null, (_, _) => { using var f = new StockForm(); f.ShowDialog(this); });
        view.DropDownItems.Add("Движение", null, (_, _) => { using var f = new MovementsForm(); f.ShowDialog(this); });
        menu.Items.Add(view);

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(8)
        };
        var btnAdd = new Button { Text = "Добавить", Size = new Size(110, 30) };
        var btnToggle = new Button { Text = "Вкл/Выкл", Size = new Size(110, 30) };
        var btnDelete = new Button { Text = "Удалить", Size = new Size(110, 30) };
        btnAdd.Click += (_, _) => AddUserDialog();
        btnToggle.Click += (_, _) => ToggleSelected();
        btnDelete.Click += (_, _) => DeleteSelected();
        panel.Controls.AddRange(new Control[] { btnAdd, btnToggle, btnDelete });

        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.RowHeadersVisible = false;
        grid.BackgroundColor = SystemColors.Window;

        Controls.Add(grid);
        Controls.Add(panel);
        Controls.Add(status);
        Controls.Add(menu);
        MainMenuStrip = menu;

        LoadUsers();
    }

    private void LoadUsers()
    {
        var table = new System.Data.DataTable();
        table.Columns.Add("ID", typeof(int));
        table.Columns.Add("Логин", typeof(string));
        table.Columns.Add("ФИО", typeof(string));
        table.Columns.Add("Роль", typeof(string));
        table.Columns.Add("Активен", typeof(string));

        foreach (var u in Database.GetUsers())
            table.Rows.Add(u.Id, u.Login, u.FullName, u.RoleTitle, u.IsActive ? "Да" : "Нет");

        grid.DataSource = table;
    }

    private (int id, string login)? Selected()
    {
        if (grid.CurrentRow == null) return null;
        return (Convert.ToInt32(grid.CurrentRow.Cells["ID"].Value),
                grid.CurrentRow.Cells["Логин"].Value?.ToString() ?? "");
    }

    private void ToggleSelected()
    {
        var sel = Selected();
        if (sel == null) return;
        if (sel.Value.id == currentUser.Id)
        {
            MessageBox.Show("Нельзя отключить собственную учётную запись.", "Внимание",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var u = Database.GetUsers().First(x => x.Id == sel.Value.id);
        Database.SetActive(u.Id, !u.IsActive);
        LoadUsers();
    }

    private void DeleteSelected()
    {
        var sel = Selected();
        if (sel == null) return;
        if (sel.Value.id == currentUser.Id)
        {
            MessageBox.Show("Нельзя удалить собственную учётную запись.", "Внимание",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (MessageBox.Show($"Удалить пользователя «{sel.Value.login}»?", "Подтверждение",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        Database.DeleteUser(sel.Value.id);
        LoadUsers();
    }

    private void AddUserDialog()
    {
        using var dlg = new Form
        {
            Text = "Новый пользователь",
            ClientSize = new Size(340, 270),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            Font = Font
        };

        var txtLogin = new TextBox { Location = new Point(20, 35), Size = new Size(300, 27) };
        var txtName = new TextBox { Location = new Point(20, 90), Size = new Size(300, 27) };
        var txtPass = new TextBox
        {
            Location = new Point(20, 145),
            Size = new Size(300, 27),
            UseSystemPasswordChar = true
        };
        var cmbRole = new ComboBox
        {
            Location = new Point(20, 200),
            Size = new Size(160, 27),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbRole.Items.AddRange(new object[] { "Пользователь", "Администратор" });
        cmbRole.SelectedIndex = 0;

        var btnOk = new Button { Text = "Создать", Location = new Point(200, 198), Size = new Size(120, 30) };
        btnOk.Click += (_, _) =>
        {
            var login = txtLogin.Text.Trim();
            var name = txtName.Text.Trim();
            var pass = txtPass.Text;

            if (login.Length < 3 || name.Length == 0 || pass.Length < 4)
            {
                MessageBox.Show("Логин — минимум 3 символа, ФИО обязательно, пароль — минимум 4 символа.",
                    "Проверьте данные", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var role = cmbRole.SelectedIndex == 1 ? "admin" : "user";
            if (!Database.AddUser(login, pass, name, role))
            {
                MessageBox.Show("Пользователь с таким логином уже существует.", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            dlg.DialogResult = DialogResult.OK;
        };

        dlg.Controls.AddRange(new Control[]
        {
            new Label { Text = "Логин", Location = new Point(20, 12), AutoSize = true }, txtLogin,
            new Label { Text = "ФИО", Location = new Point(20, 67), AutoSize = true }, txtName,
            new Label { Text = "Пароль", Location = new Point(20, 122), AutoSize = true }, txtPass,
            new Label { Text = "Роль", Location = new Point(20, 177), AutoSize = true }, cmbRole,
            btnOk
        });
        dlg.AcceptButton = btnOk;

        if (dlg.ShowDialog(this) == DialogResult.OK)
            LoadUsers();
    }
}
