namespace AtlantTools;

/// <summary>Главное окно пользователя (кладовщика): ввод документов и просмотр информации.</summary>
public class UserForm : Form
{
    private readonly AppUser user;

    public UserForm(AppUser user)
    {
        this.user = user;

        Text = "ЗАО «Атлант» — Учёт инструмента и оснастки";
        ClientSize = new Size(960, 600);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10F);

        var hello = new Label
        {
            Text = "Выберите действие в меню сверху.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.DimGray,
            Font = new Font("Segoe UI", 12F)
        };

        var status = new StatusStrip();
        status.Items.Add(new ToolStripStatusLabel($"Пользователь: {user.FullName} ({user.RoleTitle})"));

        var menu = new MenuStrip();
        menu.Items.Add(BuildDocumentsMenu());
        menu.Items.Add(BuildViewMenu());
        menu.Items.Add(BuildExtraMenu());
        menu.Items.Add(BuildHelpMenu());

        var exit = new ToolStripMenuItem("ВЫХОД");
        exit.Click += (_, _) => Close();
        menu.Items.Add(exit);

        // Порядок добавления важен: первым — то, что занимает всё оставшееся место
        Controls.Add(hello);
        Controls.Add(status);
        Controls.Add(menu);
        MainMenuStrip = menu;
    }

    private ToolStripMenuItem BuildDocumentsMenu()
    {
        var docs = new ToolStripMenuItem("Ввод документов");

        var receipt = new ToolStripMenuItem("Приходный ордер");
        receipt.DropDownItems.Add("От поставщика", null, (_, _) => OpenDocument(DocCodes.PurchaseFromSupplier));
        receipt.DropDownItems.Add("Со служб завода", null, (_, _) => OpenDocument(DocCodes.PurchaseFromPlant));
        docs.DropDownItems.Add(receipt);

        docs.DropDownItems.Add("Лимитная карта", null, (_, _) => OpenDocument(DocCodes.LimitCard));
        docs.DropDownItems.Add("Требование-накладная", null, (_, _) => OpenDocument(DocCodes.Requisition));
        AddPlanned(docs, "Акт");
        AddPlanned(docs, "Акт-требование");
        AddPlanned(docs, "Акт приемки оборудования в монтаж");
        AddPlanned(docs, "Акт приемки основных средств");
        AddPlanned(docs, "Накладная");
        AddPlanned(docs, "Накладная на сторону");
        docs.DropDownItems.Add("Товарно-транспортная накладная", null, (_, _) => OpenDocument(DocCodes.TransportInvoice));
        AddPlanned(docs, "Сопровождающая накладная");

        return docs;
    }

    /// <summary>Пункты меню, которые есть в заводской программе, но у нас пока не реализованы.</summary>
    private static void AddPlanned(ToolStripMenuItem parent, string title)
    {
        parent.DropDownItems.Add(new ToolStripMenuItem(title)
        {
            Enabled = false,
            ToolTipText = "Будет реализовано позже"
        });
    }

    private ToolStripMenuItem BuildViewMenu()
    {
        var view = new ToolStripMenuItem("Просмотр информации");
        view.DropDownItems.Add("Остатки", null, (_, _) => { using var f = new StockForm(); f.ShowDialog(this); });
        view.DropDownItems.Add("Движение", null, (_, _) => { using var f = new MovementsForm(); f.ShowDialog(this); });
        view.DropDownItems.Add(new ToolStripSeparator());

        foreach (var kind in new[] { RefKind.Materials, RefKind.Units, RefKind.Departments, RefKind.DocTypes })
        {
            var k = kind;
            view.DropDownItems.Add(RefMeta.Title(k), null, (_, _) =>
            {
                // Кладовщик справочники только смотрит; правит их администратор
                using var f = new ReferenceForm(k, false);
                f.ShowDialog(this);
            });
        }
        return view;
    }

    private ToolStripMenuItem BuildExtraMenu()
    {
        var extra = new ToolStripMenuItem("Дополнительные функции");
        extra.DropDownItems.Add("Сменить пользователя", null, (_, _) => { Tag = "logout"; Close(); });
        return extra;
    }

    private ToolStripMenuItem BuildHelpMenu()
    {
        var help = new ToolStripMenuItem("Помощь");
        help.DropDownItems.Add("О программе", null, (_, _) =>
            MessageBox.Show(
                "ЗАО «Атлант»\nИнформационная система учёта расхода инструмента и оснастки\n\n" +
                $"Пользователь: {user.FullName} ({user.RoleTitle})",
                "О программе", MessageBoxButtons.OK, MessageBoxIcon.Information));
        return help;
    }

    private void OpenDocument(int docTypeCode)
    {
        using var f = new DocumentForm(docTypeCode);
        f.ShowDialog(this);
    }
}
