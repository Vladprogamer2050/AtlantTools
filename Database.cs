using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace AtlantTools;

public class AppUser
{
    public int Id { get; set; }
    public string Login { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "user"; // "admin" или "user"
    public bool IsActive { get; set; } = true;

    public string RoleTitle => Role == "admin" ? "Администратор" : "Пользователь";
}

public static partial class Database
{
    // Файл БД лежит рядом с exe
    private static readonly string DbPath =
        Path.Combine(AppContext.BaseDirectory, "atlant.db");

    private static string ConnectionString => $"Data Source={DbPath}";

    private static SqliteConnection Open()
    {
        var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        return conn;
    }

    /// <summary>Создаёт таблицы и учётные записи по умолчанию.</summary>
    public static void Initialize()
    {
        using var conn = Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    Login        TEXT NOT NULL UNIQUE,
                    PasswordHash TEXT NOT NULL,
                    FullName     TEXT NOT NULL,
                    Role         TEXT NOT NULL CHECK (Role IN ('admin','user')),
                    IsActive     INTEGER NOT NULL DEFAULT 1
                );";
            cmd.ExecuteNonQuery();
        }

        long count;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM Users";
            count = (long)cmd.ExecuteScalar()!;
        }

        if (count == 0)
        {
            AddUser("admin", "admin123", "Администратор системы", "admin");
            AddUser("user", "user123", "Сотрудник склада", "user");
        }

        // Таблицы склада: остатки, движение, справочники и коды
        InitializeData(conn);
    }

    public static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }

    /// <summary>Проверяет логин/пароль. Возвращает пользователя или null.</summary>
    public static AppUser? Authenticate(string login, string password)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, Login, FullName, Role, IsActive
            FROM Users
            WHERE Login = $login AND PasswordHash = $hash";
        cmd.Parameters.AddWithValue("$login", login);
        cmd.Parameters.AddWithValue("$hash", HashPassword(password));

        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;

        return new AppUser
        {
            Id = r.GetInt32(0),
            Login = r.GetString(1),
            FullName = r.GetString(2),
            Role = r.GetString(3),
            IsActive = r.GetInt32(4) == 1
        };
    }

    public static List<AppUser> GetUsers()
    {
        var list = new List<AppUser>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Login, FullName, Role, IsActive FROM Users ORDER BY Id";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new AppUser
            {
                Id = r.GetInt32(0),
                Login = r.GetString(1),
                FullName = r.GetString(2),
                Role = r.GetString(3),
                IsActive = r.GetInt32(4) == 1
            });
        }
        return list;
    }

    /// <summary>Добавляет пользователя. false — если такой логин уже есть.</summary>
    public static bool AddUser(string login, string password, string fullName, string role)
    {
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Users (Login, PasswordHash, FullName, Role, IsActive)
                VALUES ($login, $hash, $name, $role, 1)";
            cmd.Parameters.AddWithValue("$login", login);
            cmd.Parameters.AddWithValue("$hash", HashPassword(password));
            cmd.Parameters.AddWithValue("$name", fullName);
            cmd.Parameters.AddWithValue("$role", role);
            cmd.ExecuteNonQuery();
            return true;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // UNIQUE constraint
        {
            return false;
        }
    }

    public static void SetActive(int userId, bool active)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Users SET IsActive = $a WHERE Id = $id";
        cmd.Parameters.AddWithValue("$a", active ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", userId);
        cmd.ExecuteNonQuery();
    }

    public static void DeleteUser(int userId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Users WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", userId);
        cmd.ExecuteNonQuery();
    }
}
