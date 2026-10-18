namespace AtlantTools;

/// <summary>
/// Окно входа с двумя вкладками: «Администратор» и «Пользователь».
/// Вкладка определяет, КАК человек входит в систему: учётная запись должна быть той же роли,
/// что и выбранная вкладка. Регистрация (всегда обычный пользователь) — только на вкладке «Пользователь».
/// </summary>
public class LoginForm : Form
{
    private readonly TabControl tabs = new();
    private readonly List<Button> loginButtons = new();
    private int attempts;

    public AppUser? CurrentUser { get; private set; }

    public LoginForm()
    {
        Text = "ЗАО «Атлант» — Вход";
        ClientSize = new Size(380, 420);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 10F);

        var title = new Label
        {
            Text = "ЗАО «Атлант»",
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(0, 15),
            Size = new Size(380, 36)
        };
        var subtitle = new Label
        {
            Text = "Учёт расхода инструмента и оснастки",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.DimGray,
            Location = new Point(0, 50),
            Size = new Size(380, 24)
        };

        tabs.Location = new Point(20, 90);
        tabs.Size = new Size(340, 310);
        tabs.TabPages.Add(BuildTab("Администратор", "admin", false));
        tabs.TabPages.Add(BuildTab("Пользователь", "user", true));

        // Enter нажимает кнопку «Войти» той вкладки, на которой стоим
        AcceptButton = loginButtons[0];
        tabs.SelectedIndexChanged += (_, _) => AcceptButton = loginButtons[tabs.SelectedIndex];

        Controls.AddRange(new Control[] { title, subtitle, tabs });
    }

    private TabPage BuildTab(string caption, string role, bool allowRegister)
    {
        var page = new TabPage(caption);

        var txtLogin = new TextBox { Location = new Point(20, 38), Size = new Size(290, 27) };
        var txtPassword = new TextBox
        {
            Location = new Point(20, 98),
            Size = new Size(290, 27),
            UseSystemPasswordChar = true
        };
        var chkShow = new CheckBox { Text = "Показать пароль", Location = new Point(20, 132), AutoSize = true };
        chkShow.CheckedChanged += (_, _) => txtPassword.UseSystemPasswordChar = !chkShow.Checked;

        var lblError = new Label
        {
            ForeColor = Color.Firebrick,
            Location = new Point(20, 160),
            Size = new Size(290, 42)
        };

        var btnLogin = new Button { Text = "Войти", Location = new Point(20, 207), Size = new Size(290, 32) };
        btnLogin.Click += (_, _) => DoLogin(role, txtLogin, txtPassword, lblError);
        loginButtons.Add(btnLogin);

        page.Controls.AddRange(new Control[]
        {
            new Label { Text = "Логин", Location = new Point(20, 14), AutoSize = true },
            txtLogin,
            new Label { Text = "Пароль", Location = new Point(20, 74), AutoSize = true },
            txtPassword,
            chkShow,
            lblError,
            btnLogin
        });

        if (allowRegister)
        {
            var lnkRegister = new LinkLabel
            {
                Text = "Нет аккаунта? Зарегистрироваться",
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(20, 245),
                Size = new Size(290, 24)
            };
            lnkRegister.LinkClicked += (_, _) =>
            {
                using var reg = new RegisterForm();
                if (reg.ShowDialog(this) == DialogResult.OK)
                {
                    // Подставляем зарегистрированный логин, остаётся ввести пароль
                    txtLogin.Text = reg.RegisteredLogin ?? "";
                    txtPassword.Clear();
                    lblError.Text = "";
                    txtPassword.Focus();
                }
            };
            page.Controls.Add(lnkRegister);
        }

        return page;
    }

    private void DoLogin(string role, TextBox txtLogin, TextBox txtPassword, Label lblError)
    {
        lblError.Text = "";
        var login = txtLogin.Text.Trim();
        var password = txtPassword.Text;

        if (login.Length == 0 || password.Length == 0)
        {
            lblError.Text = "Введите логин и пароль";
            return;
        }

        AppUser? user;
        try
        {
            user = Database.Authenticate(login, password);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Ошибка работы с базой данных:\n" + ex.Message,
                "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (user == null)
        {
            attempts++;
            lblError.Text = "Неверный логин или пароль";
            txtPassword.Clear();
            txtPassword.Focus();

            if (attempts >= 5)
            {
                MessageBox.Show("Слишком много неудачных попыток. Программа будет закрыта.",
                    "Вход", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.Cancel;
                Close();
            }
            return;
        }

        if (!user.IsActive)
        {
            lblError.Text = "Учётная запись отключена";
            return;
        }

        // Вкладка должна совпадать с ролью учётной записи
        if (user.Role != role)
        {
            lblError.Text = role == "admin"
                ? "Эта учётная запись не администратора. Войдите на вкладке «Пользователь»."
                : "Это учётная запись администратора. Войдите на вкладке «Администратор».";
            txtPassword.Clear();
            return;
        }

        CurrentUser = user;
        DialogResult = DialogResult.OK;
        Close();
    }
}
