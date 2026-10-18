using System.Text.RegularExpressions;

namespace AtlantTools;

/// <summary>Окно самостоятельной регистрации. Новые учётные записи всегда получают роль «Пользователь».</summary>
public class RegisterForm : Form
{
    private readonly TextBox txtName = new();
    private readonly TextBox txtLogin = new();
    private readonly TextBox txtPass = new();
    private readonly TextBox txtPass2 = new();
    private readonly CheckBox chkShow = new();
    private readonly Label lblError = new();
    private readonly Button btnRegister = new();

    public string? RegisteredLogin { get; private set; }

    public RegisterForm()
    {
        Text = "ЗАО «Атлант» — Регистрация";
        ClientSize = new Size(380, 420);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 10F);

        var title = new Label
        {
            Text = "Регистрация",
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(0, 15),
            Size = new Size(380, 36)
        };

        var lblName = new Label { Text = "ФИО", Location = new Point(40, 65), AutoSize = true };
        txtName.Location = new Point(40, 88);
        txtName.Size = new Size(300, 27);

        var lblLogin = new Label
        {
            Text = "Логин (латиница, цифры, _)",
            Location = new Point(40, 125),
            AutoSize = true
        };
        txtLogin.Location = new Point(40, 148);
        txtLogin.Size = new Size(300, 27);

        var lblPass = new Label { Text = "Пароль (минимум 6 символов)", Location = new Point(40, 185), AutoSize = true };
        txtPass.Location = new Point(40, 208);
        txtPass.Size = new Size(300, 27);
        txtPass.UseSystemPasswordChar = true;

        var lblPass2 = new Label { Text = "Повторите пароль", Location = new Point(40, 245), AutoSize = true };
        txtPass2.Location = new Point(40, 268);
        txtPass2.Size = new Size(300, 27);
        txtPass2.UseSystemPasswordChar = true;

        chkShow.Text = "Показать пароль";
        chkShow.Location = new Point(40, 302);
        chkShow.AutoSize = true;
        chkShow.CheckedChanged += (_, _) =>
        {
            txtPass.UseSystemPasswordChar = !chkShow.Checked;
            txtPass2.UseSystemPasswordChar = !chkShow.Checked;
        };

        lblError.ForeColor = Color.Firebrick;
        lblError.Location = new Point(40, 328);
        lblError.Size = new Size(300, 40);

        btnRegister.Text = "Зарегистрироваться";
        btnRegister.Location = new Point(40, 372);
        btnRegister.Size = new Size(300, 32);
        btnRegister.Click += (_, _) => DoRegister();
        AcceptButton = btnRegister;

        Controls.AddRange(new Control[]
        {
            title, lblName, txtName, lblLogin, txtLogin, lblPass, txtPass,
            lblPass2, txtPass2, chkShow, lblError, btnRegister
        });
    }

    private void DoRegister()
    {
        lblError.Text = "";
        var name = txtName.Text.Trim();
        var login = txtLogin.Text.Trim();
        var pass = txtPass.Text;
        var pass2 = txtPass2.Text;

        if (name.Length < 3)
        {
            lblError.Text = "Введите ФИО (минимум 3 символа)";
            txtName.Focus();
            return;
        }
        if (login.Length < 3)
        {
            lblError.Text = "Логин — минимум 3 символа";
            txtLogin.Focus();
            return;
        }
        if (!Regex.IsMatch(login, "^[A-Za-z0-9_]+$"))
        {
            lblError.Text = "В логине только латиница, цифры и _";
            txtLogin.Focus();
            return;
        }
        if (pass.Length < 6)
        {
            lblError.Text = "Пароль — минимум 6 символов";
            txtPass.Focus();
            return;
        }
        if (pass != pass2)
        {
            lblError.Text = "Пароли не совпадают";
            txtPass2.Clear();
            txtPass2.Focus();
            return;
        }

        try
        {
            // Роль всегда "user": администратора может создать только другой администратор
            if (!Database.AddUser(login, pass, name, "user"))
            {
                lblError.Text = "Такой логин уже занят";
                txtLogin.Focus();
                return;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Ошибка работы с базой данных:\n" + ex.Message,
                "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        MessageBox.Show("Регистрация прошла успешно! Теперь можно войти.",
            "Регистрация", MessageBoxButtons.OK, MessageBoxIcon.Information);

        RegisteredLogin = login;
        DialogResult = DialogResult.OK;
        Close();
    }
}
