using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using AAALogistics.Client.Services;

namespace AAALogistics.Client.Views;

/// <summary>Helpers de UI oficina (Fiori-lite) reutilizables en vistas Avalonia.</summary>
internal static class OfficeUi
{
    public static TextBlock PageTitle(string text) =>
        new()
        {
            Text = text,
            Classes = { "page-title" },
        };

    /// <summary>Botón circular (i) que abre diálogo de ayuda ES. ToolTip «Ayuda».</summary>
    public static Button InfoButton(string helpKey)
    {
        var button = new Button
        {
            Content = "i",
            Classes = { "info-help" },
            Tag = helpKey,
        };
        ToolTip.SetTip(button, "Ayuda");
        button.Click += async (_, _) =>
        {
            var owner = TopLevel.GetTopLevel(button) as Window;
            if (owner is null)
            {
                return;
            }

            await ShowHelpAsync(owner, helpKey);
        };
        return button;
    }

    public static void AttachInfoHelp(Panel titleRow, string helpKey) =>
        titleRow.Children.Add(InfoButton(helpKey));

    /// <summary>Asigna el botón (i) a un ContentControl host (AXAML).</summary>
    public static void BindHelp(ContentControl host, string helpKey) =>
        host.Content = InfoButton(helpKey);

    /// <summary>Control + (i) en fila horizontal (botones o cajas Studio).</summary>
    public static StackPanel WithHelp(Control control, string helpKey)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.Children.Add(control);
        row.Children.Add(InfoButton(helpKey));
        return row;
    }

    public static async Task ShowHelpAsync(Window owner, string helpKey)
    {
        var (title, body) = OfficeHelpTexts.Get(helpKey);
        var close = new Button
        {
            Content = "Cerrar",
            Classes = { "primary" },
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 96,
        };
        var panel = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 14,
        };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
        });
        panel.Children.Add(new TextBlock
        {
            Text = body,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 420,
        });
        panel.Children.Add(close);
        var dialog = new Window
        {
            Title = title,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = BrandTheme.Brush(BrandTheme.Background),
            Foreground = BrandTheme.Brush(BrandTheme.Text),
            Content = panel,
        };
        close.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(owner);
    }

    public static TextBlock Muted(string text) =>
        new()
        {
            Text = text,
            Classes = { "muted" },
            TextWrapping = TextWrapping.Wrap,
        };

    public static Border EmptyState(string title, string hint)
    {
        var stack = new StackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(24),
        };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        stack.Children.Add(new TextBlock
        {
            Text = hint,
            Classes = { "muted" },
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = 420,
            TextAlignment = TextAlignment.Center,
        });
        return new Border
        {
            Classes = { "panel" },
            Child = stack,
            MinHeight = 120,
        };
    }

    public static StackPanel LabeledField(
        string label,
        Control field,
        double labelWidth = 110,
        string? helpKey = null)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };
        row.Children.Add(new TextBlock
        {
            Text = label,
            Width = labelWidth,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeight.SemiBold,
        });
        row.Children.Add(field);
        if (!string.IsNullOrWhiteSpace(helpKey))
        {
            row.Children.Add(InfoButton(helpKey));
        }

        return row;
    }

    public static StackPanel CheckWithHelp(CheckBox checkBox, string helpKey)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.Children.Add(checkBox);
        row.Children.Add(InfoButton(helpKey));
        return row;
    }

    public static void SetTextColumns(
        DataGrid grid,
        params (string header, string property)[] columns)
    {
        grid.AutoGenerateColumns = false;
        grid.Columns.Clear();
        foreach (var (header, property) in columns)
        {
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(property),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            });
        }
    }

    public static void SetCheckColumn(
        DataGrid grid,
        string header,
        string property)
    {
        grid.Columns.Add(new DataGridCheckBoxColumn
        {
            Header = header,
            Binding = new Binding(property),
            Width = new DataGridLength(90),
            IsReadOnly = true,
        });
    }

    public static Border Section(string title, params Control[] children)
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeight.SemiBold,
            FontSize = 14,
        });
        foreach (var child in children)
        {
            stack.Children.Add(child);
        }

        return new Border
        {
            Classes = { "panel" },
            Child = stack,
            Margin = new Thickness(0, 0, 0, 8),
        };
    }

    public static TextBox Box(string placeholder, double width = 180) =>
        new()
        {
            PlaceholderText = placeholder,
            Width = width,
            MinHeight = 28,
        };

    /// <summary>Traduce estados de dominio/API (EN) a etiquetas de oficina ES.</summary>
    public static string StatusToEs(string? status) =>
        status?.Trim() switch
        {
            null or "" => "—",
            "Draft" => "Borrador",
            "Submitted" => "Enviado",
            "Requested" => "Solicitado",
            "PartiallyReceived" => "Recepción parcial",
            "Received" => "Recibido",
            "Cancelled" or "Canceled" => "Cancelado",
            "Closed" => "Cerrado",
            "Confirmed" => "Confirmado",
            "InPreparation" => "En preparación",
            "Prepared" => "Preparado",
            "PartiallyDelivered" => "Entrega parcial",
            "Delivered" => "Entregado",
            "Rejected" => "Rechazado",
            "Available" => "Disponible",
            "Blocked" => "Bloqueado",
            "Damaged" => "Dañado",
            "Quality" or "QualityInspection" or "Qi" or "QI" => "En calidad",
            "Reserved" => "Reservado",
            "Open" => "Abierto",
            "Exclusive" => "Exclusivo",
            "Posted" => "Contabilizado",
            "Issued" => "Emitida",
            "Rectified" => "Rectificada",
            "Pending" => "Pendiente",
            "Approved" => "Aprobado",
            "Suspended" => "Suspendido",
            "Held" => "Retenido",
            "Released" => "Liberado",
            "Planned" => "Planificado",
            "Completed" => "Completado",
            "Active" => "Activo",
            "Awarded" => "Adjudicado",
            "Sent" => "Enviado",
            "Failed" => "Fallido",
            _ => status,
        };

    public static string RangeToEs(string? range) =>
        range?.Trim() switch
        {
            null or "" => "—",
            "Admin" => "Administrador",
            "Gerente" => "Gerente",
            "Compras" => "Compras",
            "Ventas" => "Ventas",
            "Almacen" => "Almacén",
            "Finanzas" => "Finanzas",
            "Calidad" => "Calidad",
            "Cliente" => "Cliente (portal)",
            "GerenteLogistica" => "Gerente (antiguo)",
            "CoordinadorLogistica" => "Coordinador (antiguo)",
            "Administrativo" => "Administrativo (antiguo)",
            "AsistenteLogistica" => "Asistente (antiguo)",
            "OperarioLogistica" => "Operario (antiguo)",
            "Trabajador" => "Trabajador (antiguo)",
            _ => range,
        };

    public static string RangesToEs(IEnumerable<string> ranges) =>
        string.Join(", ", ranges.Select(RangeToEs));

    public static string PermissionToEs(string permission) =>
        permission switch
        {
            AppPermissionNames.MaterialManage => "Materiales: gestionar",
            AppPermissionNames.MaterialDelete => "Materiales: solicitar baja",
            AppPermissionNames.MaterialDeleteApprove => "Materiales: aprobar baja",
            AppPermissionNames.WarehouseManage => "Almacenes y plantas: gestionar",
            AppPermissionNames.LocationManage => "Ubicaciones: gestionar",
            AppPermissionNames.SupplierManage => "Proveedores: gestionar",
            AppPermissionNames.CustomerManage => "Clientes y precios: gestionar",
            AppPermissionNames.CarrierManage => "Transportistas: gestionar",
            AppPermissionNames.PurchaseOrderCreate => "Compras: crear pedidos",
            AppPermissionNames.PurchaseOrderCancelOpen => "Compras: cancelar pendiente",
            AppPermissionNames.PurchaseOrderClose => "Compras: cerrar pedidos",
            AppPermissionNames.GoodsReceiptPost => "Compras: contabilizar recepción",
            AppPermissionNames.SalesOrderCreate => "Ventas: crear pedidos",
            AppPermissionNames.SalesOrderRead => "Ventas: consultar pedidos",
            AppPermissionNames.SalesOrderCreateOwn => "Portal: pedidos propios",
            AppPermissionNames.SalesOrderConfirm => "Ventas: confirmar pedidos",
            AppPermissionNames.SalesOrderPrepare => "Ventas: preparar (picking)",
            AppPermissionNames.SalesOrderCancel => "Ventas: cancelar pedidos",
            AppPermissionNames.SalesOrderClose => "Ventas: cerrar pedidos",
            AppPermissionNames.DeliveryPost => "Expedición: contabilizar entregas",
            AppPermissionNames.DeliveryNoteManage => "Albaranes: gestionar",
            AppPermissionNames.CmrManage => "CMR: gestionar",
            AppPermissionNames.InvoiceIssue => "Facturas: emitir",
            AppPermissionNames.InvoiceRectify => "Facturas: rectificar",
            AppPermissionNames.InvoicePrint => "Facturas: imprimir (impresora)",
            AppPermissionNames.InvoiceConfig => "Facturación y finanzas: configurar",
            AppPermissionNames.ReturnManage => "Devoluciones: gestionar",
            AppPermissionNames.StockRead => "Stock: consultar",
            AppPermissionNames.StockTransfer => "Stock: traspasar",
            AppPermissionNames.StockAdjust => "Stock y calidad: ajustar",
            AppPermissionNames.StockReverse => "Stock: anular movimientos",
            AppPermissionNames.StockCount => "Stock: inventariar",
            AppPermissionNames.StockOpeningImport => "Stock: importar apertura",
            AppPermissionNames.AuditVerify => "Auditoría: verificar",
            AppPermissionNames.BackupManage => "Copias de seguridad",
            AppPermissionNames.RegistrationApprove => "Registro: aprobar",
            AppPermissionNames.RegistrationReject => "Registro: rechazar",
            AppPermissionNames.RegistrationExpire => "Registro: caducar",
            AppPermissionNames.UserRead => "Usuarios: consultar",
            AppPermissionNames.UserCreate => "Usuarios: crear y restablecer contraseña",
            AppPermissionNames.UserManagePermissions => "Usuarios: permisos y aprobaciones",
            AppPermissionNames.UserManageRanges => "Usuarios: rangos",
            AppPermissionNames.UserSuspend => "Usuarios: suspender",
            AppPermissionNames.UserReactivate => "Usuarios: reactivar",
            AppPermissionNames.UserTerminate => "Usuarios: dar de baja",
            AppPermissionNames.CustomerSuspend => "Clientes: suspender",
            AppPermissionNames.AdminTableEdit => "Administración avanzada",
            AppPermissionNames.ReportRead => "Informes: consultar",
            _ => permission,
        };

    public static async Task<bool> ConfirmAsync(Window owner, string message)
    {
        var accepted = false;
        var ok = new Button { Content = "Sí", Classes = { "primary" }, MinWidth = 88 };
        var cancel = new Button { Content = "No", MinWidth = 88 };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 14 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(buttons);
        var dialog = new Window
        {
            Title = BrandTheme.ShortName,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = BrandTheme.Brush(BrandTheme.Background),
            Content = panel,
        };
        ok.Click += (_, _) =>
        {
            accepted = true;
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(owner);
        return accepted;
    }
}
