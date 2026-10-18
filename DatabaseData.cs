using System.Data;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace AtlantTools;

// ───────────────────────── Справочники ─────────────────────────

/// <summary>Какой справочник открыт. Все справочники хранятся «кодами»: Код + Расшифровка.</summary>
public enum RefKind { Units, Departments, DocTypes, Materials }

public static class RefMeta
{
    public static string Table(RefKind k) => k switch
    {
        RefKind.Units => "Units",
        RefKind.Departments => "Departments",
        RefKind.DocTypes => "DocTypes",
        _ => "Materials"
    };

    public static string Title(RefKind k) => k switch
    {
        RefKind.Units => "Единицы измерения",
        RefKind.Departments => "Цеха и получатели",
        RefKind.DocTypes => "Виды документов",
        _ => "Справочник материалов"
    };

    public static string CodeTitle(RefKind k) => k == RefKind.Materials ? "Код материала (6 цифр)" : "Код";

    public static string NameTitle(RefKind k) => k == RefKind.Units ? "Расшифровка" : "Наименование";

    /// <summary>Точная длина кода (0 — любая, до 9 цифр).</summary>
    public static int FixedCodeLength(RefKind k) => k == RefKind.Materials ? 6 : 0;

    /// <summary>Числовые коды приводим к каноничному виду (010 → 10), код материала остаётся текстом.</summary>
    public static string NormalizeCode(RefKind k, string code) =>
        k == RefKind.Materials
            ? code
            : long.Parse(code, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
}

/// <summary>Коды видов документов, которые умеет вводить программа.</summary>
public static class DocCodes
{
    public const int PurchaseFromSupplier = 1; // Приходный ордер — от поставщика
    public const int PurchaseFromPlant = 2;    // Приходный ордер — со служб завода
    public const int Requisition = 3;          // Требование-накладная
    public const int LimitCard = 4;            // Лимитная карта
    public const int TransportInvoice = 5;     // Товарно-транспортная накладная

    public static bool IsSystem(int code) => code >= 1 && code <= 5;

    /// <summary>Расходные документы: списывают остатки и указывают получателя.</summary>
    public static bool IsExpense(int code) =>
        code == Requisition || code == LimitCard || code == TransportInvoice;

    /// <summary>Подпись поля получателя в окне документа.</summary>
    public static string RecipientLabel(int code) =>
        code == TransportInvoice ? "Получатель" : "Цех-получатель";
}

public class RefItem
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";

    public override string ToString() => $"{Name} ({Code})";
}

// ───────────────────────── Модели ─────────────────────────

/// <summary>Строка таблицы «Остатки».</summary>
public class StockRow
{
    public int Id { get; set; }
    public string MaterialCode { get; set; } = "";
    public string MaterialName { get; set; } = "";
    public double Price { get; set; }
    public double Quantity { get; set; }
    public int UnitCode { get; set; }
    public string UnitName { get; set; } = "";
    public string Date { get; set; } = "";
}

/// <summary>Позиция документа. Каждая позиция при проведении станет отдельной записью движения.</summary>
public class DocLine
{
    public int StockId { get; set; } // только для расхода: из какой строки остатков списываем
    public string MaterialCode { get; set; } = "";
    public string MaterialName { get; set; } = "";
    public int UnitCode { get; set; }
    public string UnitName { get; set; } = "";
    public double Price { get; set; }
    public double Quantity { get; set; }

    public double Sum => Math.Round(Price * Quantity, 2);
}

// ───────────────────────── Работа с БД ─────────────────────────

public static partial class Database
{
    /// <summary>Номер склада в заголовке главного окна.</summary>
    public const int WarehouseNo = 70;

    private static readonly string[] CreateTableSql =
    {
        // Коды единиц измерения: 10 → кг и т.д.
        @"CREATE TABLE IF NOT EXISTS Units (
              Id   INTEGER PRIMARY KEY AUTOINCREMENT,
              Code INTEGER NOT NULL UNIQUE,
              Name TEXT NOT NULL
          )",

        // Коды видов документов
        @"CREATE TABLE IF NOT EXISTS DocTypes (
              Id   INTEGER PRIMARY KEY AUTOINCREMENT,
              Code INTEGER NOT NULL UNIQUE,
              Name TEXT NOT NULL
          )",

        // Цеха-получатели
        @"CREATE TABLE IF NOT EXISTS Departments (
              Id   INTEGER PRIMARY KEY AUTOINCREMENT,
              Code INTEGER NOT NULL UNIQUE,
              Name TEXT NOT NULL
          )",

        // Справочник материалов: код ровно 6 цифр
        @"CREATE TABLE IF NOT EXISTS Materials (
              Id   INTEGER PRIMARY KEY AUTOINCREMENT,
              Code TEXT NOT NULL UNIQUE
                   CHECK (length(Code) = 6 AND Code NOT GLOB '*[^0-9]*'),
              Name TEXT NOT NULL
          )",

        // Остатки: код материала до 15 цифр, единица измерения — кодом
        @"CREATE TABLE IF NOT EXISTS Stock (
              Id           INTEGER PRIMARY KEY AUTOINCREMENT,
              MaterialCode TEXT NOT NULL
                           CHECK (length(MaterialCode) BETWEEN 1 AND 15 AND MaterialCode NOT GLOB '*[^0-9]*'),
              Price        REAL NOT NULL CHECK (Price >= 0),
              Quantity     REAL NOT NULL CHECK (Quantity >= 0),
              UnitCode     INTEGER NOT NULL REFERENCES Units(Code),
              Date         TEXT NOT NULL,
              UNIQUE (MaterialCode, UnitCode, Price)
          )",

        // Движение: каждая позиция документа — отдельная запись.
        // IsExpense: 1 (true) = расходный, 0 (false) = приходный
        @"CREATE TABLE IF NOT EXISTS Movements (
              Id             INTEGER PRIMARY KEY AUTOINCREMENT,
              Date           TEXT NOT NULL,
              DocNumber      TEXT NOT NULL,
              DocTypeCode    INTEGER NOT NULL REFERENCES DocTypes(Code),
              IsExpense      INTEGER NOT NULL CHECK (IsExpense IN (0, 1)),
              MaterialCode   TEXT NOT NULL,
              Quantity       REAL NOT NULL CHECK (Quantity > 0),
              UnitCode       INTEGER NOT NULL REFERENCES Units(Code),
              Price          REAL NOT NULL CHECK (Price >= 0),
              DepartmentCode INTEGER NULL REFERENCES Departments(Code)
          )"
    };

    // Начальные данные кладутся один раз — при первом создании таблиц.
    private static readonly (int Code, string Name)[] SeedUnits =
    {
        (10, "кг"), (11, "шт"), (12, "компл."),
        (101, "мкм"), (102, "мм"), (103, "см"), (104, "дм"), (105, "м"), (106, "км"),
        (202, "кв.мм"), (203, "кв.см"), (204, "кв.дм"), (205, "кв.м"), (206, "кв.км"),
        (302, "куб.мм"), (303, "куб.см"), (304, "куб.дм"), (305, "куб.м"),
        (311, "мкл"), (312, "мл")
    };

    private static readonly (int Code, string Name)[] SeedDocTypes =
    {
        (DocCodes.PurchaseFromSupplier, "Приходный ордер (от поставщика)"),
        (DocCodes.PurchaseFromPlant, "Приходный ордер (со служб завода)"),
        (DocCodes.Requisition, "Требование-накладная"),
        (DocCodes.LimitCard, "Лимитная карта"),
        (DocCodes.TransportInvoice, "Товарно-транспортная накладная")
    };

    private static readonly (int Code, string Name)[] SeedDepartments =
    {
        (1, "Цех № 1"), (2, "Цех № 2"), (3, "Инструментальная мастерская")
    };

    private static readonly (string Code, string Name)[] SeedMaterials =
    {
        ("100101", "Сверло спиральное Ø6 мм"),
        ("100102", "Фреза концевая Ø10 мм"),
        ("100103", "Резец проходной"),
        ("100104", "Штангенциркуль ШЦ-I-150"),
        ("100105", "Метчик М8")
    };

    private static SqliteCommand NewCmd(SqliteConnection conn, SqliteTransaction tx, string sql)
    {
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        return cmd;
    }

    private static bool TableExists(SqliteConnection conn, string name)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $n";
        cmd.Parameters.AddWithValue("$n", name);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    private static void SeedRef(SqliteConnection conn, SqliteTransaction tx, string table, string code, string name)
    {
        using var cmd = NewCmd(conn, tx, $"INSERT INTO {table} (Code, Name) VALUES ($c, $n)");
        cmd.Parameters.AddWithValue("$c", code);
        cmd.Parameters.AddWithValue("$n", name);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Создаёт таблицы склада; вызывается из Initialize().</summary>
    private static void InitializeData(SqliteConnection conn)
    {
        bool fresh = !TableExists(conn, "Units");

        using var tx = conn.BeginTransaction();

        foreach (var sql in CreateTableSql)
        {
            using var cmd = NewCmd(conn, tx, sql);
            cmd.ExecuteNonQuery();
        }

        if (fresh)
        {
            foreach (var (code, name) in SeedUnits)
                SeedRef(conn, tx, "Units", code.ToString(CultureInfo.InvariantCulture), name);
            foreach (var (code, name) in SeedDocTypes)
                SeedRef(conn, tx, "DocTypes", code.ToString(CultureInfo.InvariantCulture), name);
            foreach (var (code, name) in SeedDepartments)
                SeedRef(conn, tx, "Departments", code.ToString(CultureInfo.InvariantCulture), name);
            foreach (var (code, name) in SeedMaterials)
                SeedRef(conn, tx, "Materials", code, name);
        }

        // Системные виды документов добавляем и в уже существующую БД
        foreach (var (docCode, docName) in SeedDocTypes)
        {
            using var cmd = NewCmd(conn, tx, "INSERT OR IGNORE INTO DocTypes (Code, Name) VALUES ($c, $n)");
            cmd.Parameters.AddWithValue("$c", docCode);
            cmd.Parameters.AddWithValue("$n", docName);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    // ───────────── Справочники: чтение / добавление / изменение / удаление ─────────────

    public static List<RefItem> GetRef(RefKind kind)
    {
        var list = new List<RefItem>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"SELECT Id, CAST(Code AS TEXT), Name FROM {RefMeta.Table(kind)} ORDER BY CAST(Code AS INTEGER), Code";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new RefItem { Id = r.GetInt32(0), Code = r.GetString(1), Name = r.GetString(2) });
        }
        return list;
    }

    /// <summary>Расшифровка по коду или null, если такого кода нет.</summary>
    public static string? FindRefName(RefKind kind, string code)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT Name FROM {RefMeta.Table(kind)} WHERE Code = $c";
        cmd.Parameters.AddWithValue("$c", code);
        return cmd.ExecuteScalar() as string;
    }

    /// <summary>Возвращает текст ошибки или null, если всё хорошо.</summary>
    public static string? AddRef(RefKind kind, string code, string name)
    {
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"INSERT INTO {RefMeta.Table(kind)} (Code, Name) VALUES ($c, $n)";
            cmd.Parameters.AddWithValue("$c", code);
            cmd.Parameters.AddWithValue("$n", name);
            cmd.ExecuteNonQuery();
            return null;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            return "Запись с таким кодом уже существует.";
        }
    }

    private static string? GetRefCode(SqliteConnection conn, RefKind kind, int id)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT CAST(Code AS TEXT) FROM {RefMeta.Table(kind)} WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteScalar() as string;
    }

    private static bool IsRefUsed(SqliteConnection conn, RefKind kind, string code)
    {
        string sql = kind switch
        {
            RefKind.Units =>
                "SELECT (SELECT COUNT(*) FROM Stock WHERE UnitCode = $c) + (SELECT COUNT(*) FROM Movements WHERE UnitCode = $c)",
            RefKind.Departments =>
                "SELECT COUNT(*) FROM Movements WHERE DepartmentCode = $c",
            RefKind.DocTypes =>
                "SELECT COUNT(*) FROM Movements WHERE DocTypeCode = $c",
            _ =>
                "SELECT (SELECT COUNT(*) FROM Stock WHERE MaterialCode = $c) + (SELECT COUNT(*) FROM Movements WHERE MaterialCode = $c)"
        };

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$c", code);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    public static string? UpdateRef(RefKind kind, int id, string code, string name)
    {
        try
        {
            using var conn = Open();
            var oldCode = GetRefCode(conn, kind, id);
            if (oldCode == null) return "Запись не найдена.";

            if (oldCode != code)
            {
                if (kind == RefKind.DocTypes && DocCodes.IsSystem(int.Parse(oldCode, CultureInfo.InvariantCulture)))
                    return "Код системного вида документа менять нельзя.";
                if (IsRefUsed(conn, kind, oldCode))
                    return "Этот код уже используется в документах и остатках — можно изменить только название.";
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"UPDATE {RefMeta.Table(kind)} SET Code = $c, Name = $n WHERE Id = $id";
            cmd.Parameters.AddWithValue("$c", code);
            cmd.Parameters.AddWithValue("$n", name);
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
            return null;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            return "Запись с таким кодом уже существует.";
        }
    }

    public static string? DeleteRef(RefKind kind, int id)
    {
        using var conn = Open();
        var code = GetRefCode(conn, kind, id);
        if (code == null) return "Запись не найдена.";

        if (kind == RefKind.DocTypes && DocCodes.IsSystem(int.Parse(code, CultureInfo.InvariantCulture)))
            return "Это системный вид документа, удалить его нельзя.";
        if (IsRefUsed(conn, kind, code))
            return "Запись используется в документах или остатках — удалить нельзя.";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM {RefMeta.Table(kind)} WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
        return null;
    }

    // ───────────── Остатки и движение ─────────────

    public static List<StockRow> GetStock(bool onlyPositive)
    {
        var list = new List<StockRow>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT s.Id, s.MaterialCode, COALESCE(m.Name, '(нет в справочнике)'),
                   s.Price, s.Quantity, s.UnitCode, COALESCE(u.Name, CAST(s.UnitCode AS TEXT)), s.Date
            FROM Stock s
            LEFT JOIN Materials m ON m.Code = s.MaterialCode
            LEFT JOIN Units u ON u.Code = s.UnitCode
            WHERE ($pos = 0 OR s.Quantity > 0)
            ORDER BY s.MaterialCode, s.Date, s.Id";
        cmd.Parameters.AddWithValue("$pos", onlyPositive ? 1 : 0);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new StockRow
            {
                Id = r.GetInt32(0),
                MaterialCode = r.GetString(1),
                MaterialName = r.GetString(2),
                Price = r.GetDouble(3),
                Quantity = r.GetDouble(4),
                UnitCode = r.GetInt32(5),
                UnitName = r.GetString(6),
                Date = r.GetString(7)
            });
        }
        return list;
    }

    /// <summary>Журнал движения за период (включительно) уже с расшифровкой кодов.</summary>
    public static DataTable GetMovementsTable(DateTime from, DateTime to)
    {
        var t = new DataTable();
        t.Columns.Add("Дата", typeof(DateTime));
        t.Columns.Add("№ документа", typeof(string));
        t.Columns.Add("Документ", typeof(string));
        t.Columns.Add("Движение", typeof(string));
        t.Columns.Add("Код материала", typeof(string));
        t.Columns.Add("Наименование", typeof(string));
        t.Columns.Add("Количество", typeof(double));
        t.Columns.Add("Ед. изм.", typeof(string));
        t.Columns.Add("Цена", typeof(double));
        t.Columns.Add("Сумма", typeof(double));
        t.Columns.Add("Получатель", typeof(string));

        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT mv.Id, mv.Date, mv.DocNumber,
                   COALESCE(dt.Name, CAST(mv.DocTypeCode AS TEXT)),
                   mv.IsExpense, mv.MaterialCode, COALESCE(m.Name, ''),
                   mv.Quantity, COALESCE(u.Name, CAST(mv.UnitCode AS TEXT)),
                   mv.Price, COALESCE(d.Name, '')
            FROM Movements mv
            LEFT JOIN DocTypes dt ON dt.Code = mv.DocTypeCode
            LEFT JOIN Materials m ON m.Code = mv.MaterialCode
            LEFT JOIN Units u ON u.Code = mv.UnitCode
            LEFT JOIN Departments d ON d.Code = mv.DepartmentCode
            WHERE mv.Date >= $from AND mv.Date <= $to
            ORDER BY mv.Date DESC, mv.Id DESC";
        cmd.Parameters.AddWithValue("$from", from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$to", to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            double qty = r.GetDouble(7);
            double price = r.GetDouble(9);
            t.Rows.Add(
                UiHelper.ParseDbDate(r.GetString(1)),
                r.GetString(2),
                r.GetString(3),
                r.GetInt32(4) == 1 ? "Расход" : "Приход",
                r.GetString(5),
                r.GetString(6),
                qty,
                r.GetString(8),
                price,
                Math.Round(qty * price, 2),
                r.GetString(10));
        }
        return t;
    }

    /// <summary>Следующий свободный номер документа данного вида.</summary>
    public static string NextDocNumber(int docTypeCode)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT MAX(CAST(DocNumber AS INTEGER)) FROM Movements WHERE DocTypeCode = $t";
        cmd.Parameters.AddWithValue("$t", docTypeCode);
        var v = cmd.ExecuteScalar();
        long n = (v == null || v is DBNull) ? 0 : Convert.ToInt64(v);
        return (n + 1).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Проводит документ одной транзакцией: каждая позиция → отдельная запись движения
    /// + изменение остатков. При любой ошибке не меняется ничего.
    /// Возвращает текст ошибки или null, если документ проведён.
    /// </summary>
    public static string? PostDocument(DateTime date, string docNumber, int docTypeCode,
        bool isExpense, int? departmentCode, IList<DocLine> lines)
    {
        if (lines.Count == 0) return "Добавьте хотя бы одну позицию.";
        if (isExpense && departmentCode == null) return "Не указан получатель.";

        try
        {
            using var conn = Open();
            using var tx = conn.BeginTransaction();
            var d = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            using (var chk = NewCmd(conn, tx,
                       "SELECT COUNT(*) FROM Movements WHERE DocTypeCode = $t AND DocNumber = $n"))
            {
                chk.Parameters.AddWithValue("$t", docTypeCode);
                chk.Parameters.AddWithValue("$n", docNumber);
                if (Convert.ToInt64(chk.ExecuteScalar()) > 0)
                    return $"Документ № {docNumber} этого вида уже есть. Укажите другой номер.";
            }

            foreach (var l in lines)
            {
                var qty = Math.Round(l.Quantity, 3);
                var price = Math.Round(l.Price, 2);
                var code = l.MaterialCode;
                var unit = l.UnitCode;

                if (isExpense)
                {
                    // Списываем с конкретной строки остатков (код + цена)
                    using (var upd = NewCmd(conn, tx,
                               "UPDATE Stock SET Quantity = ROUND(Quantity - $q, 3) WHERE Id = $id AND Quantity >= $q"))
                    {
                        upd.Parameters.AddWithValue("$q", qty);
                        upd.Parameters.AddWithValue("$id", l.StockId);
                        if (upd.ExecuteNonQuery() == 0)
                            return $"Недостаточно остатка по материалу {l.MaterialCode} ({l.MaterialName}).";
                    }

                    // Код, единицу и цену берём из самих остатков
                    using (var sel = NewCmd(conn, tx,
                               "SELECT MaterialCode, UnitCode, Price FROM Stock WHERE Id = $id"))
                    {
                        sel.Parameters.AddWithValue("$id", l.StockId);
                        using var r = sel.ExecuteReader();
                        if (!r.Read()) return "Строка остатков не найдена.";
                        code = r.GetString(0);
                        unit = r.GetInt32(1);
                        price = r.GetDouble(2);
                    }
                }
                else
                {
                    using (var chkMat = NewCmd(conn, tx, "SELECT COUNT(*) FROM Materials WHERE Code = $m"))
                    {
                        chkMat.Parameters.AddWithValue("$m", code);
                        if (Convert.ToInt64(chkMat.ExecuteScalar()) == 0)
                            return $"Материала с кодом {code} нет в справочнике.";
                    }

                    // Тот же материал, единица и цена — увеличиваем остаток, иначе новая строка
                    using (var upd = NewCmd(conn, tx,
                               "UPDATE Stock SET Quantity = ROUND(Quantity + $q, 3), Date = $d " +
                               "WHERE MaterialCode = $m AND UnitCode = $u AND Price = $p"))
                    {
                        upd.Parameters.AddWithValue("$q", qty);
                        upd.Parameters.AddWithValue("$d", d);
                        upd.Parameters.AddWithValue("$m", code);
                        upd.Parameters.AddWithValue("$u", unit);
                        upd.Parameters.AddWithValue("$p", price);
                        if (upd.ExecuteNonQuery() == 0)
                        {
                            using var ins = NewCmd(conn, tx,
                                "INSERT INTO Stock (MaterialCode, Price, Quantity, UnitCode, Date) " +
                                "VALUES ($m, $p, $q, $u, $d)");
                            ins.Parameters.AddWithValue("$m", code);
                            ins.Parameters.AddWithValue("$p", price);
                            ins.Parameters.AddWithValue("$q", qty);
                            ins.Parameters.AddWithValue("$u", unit);
                            ins.Parameters.AddWithValue("$d", d);
                            ins.ExecuteNonQuery();
                        }
                    }
                }

                using var mv = NewCmd(conn, tx,
                    "INSERT INTO Movements (Date, DocNumber, DocTypeCode, IsExpense, MaterialCode, " +
                    "Quantity, UnitCode, Price, DepartmentCode) " +
                    "VALUES ($d, $n, $t, $e, $m, $q, $u, $p, $dep)");
                mv.Parameters.AddWithValue("$d", d);
                mv.Parameters.AddWithValue("$n", docNumber);
                mv.Parameters.AddWithValue("$t", docTypeCode);
                mv.Parameters.AddWithValue("$e", isExpense ? 1 : 0);
                mv.Parameters.AddWithValue("$m", code);
                mv.Parameters.AddWithValue("$q", qty);
                mv.Parameters.AddWithValue("$u", unit);
                mv.Parameters.AddWithValue("$p", price);
                mv.Parameters.AddWithValue("$dep", (object?)departmentCode ?? DBNull.Value);
                mv.ExecuteNonQuery();
            }

            tx.Commit();
            return null;
        }
        catch (Exception ex)
        {
            return "Ошибка базы данных: " + ex.Message;
        }
    }
}
