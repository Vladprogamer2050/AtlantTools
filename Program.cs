namespace AtlantTools;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Создаём БД и учётные записи по умолчанию при первом запуске
        Database.Initialize();

        while (true)
        {
            using var login = new LoginForm();
            if (login.ShowDialog() != DialogResult.OK || login.CurrentUser == null)
                break;

            Form main = login.CurrentUser.Role == "admin"
                ? new AdminForm(login.CurrentUser)
                : new UserForm(login.CurrentUser);

            Application.Run(main);

            // Если пользователь нажал «Сменить пользователя» — снова показываем вход,
            // иначе (закрыл окно крестиком) — выходим из программы
            if (!(main.Tag is string tag && tag == "logout"))
                break;
        }
    }
}
