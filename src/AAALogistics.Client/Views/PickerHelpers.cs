using Avalonia.Controls;
using AAALogistics.Contracts;

namespace AAALogistics.Client.Views;

internal static class PickerHelpers
{
    public static ComboBox Create(double width = 280) =>
        new() { Width = width, MinHeight = 28 };

    public static void BindMaterials(ComboBox box, IEnumerable<Services.MaterialRow> rows)
    {
        BindItems(
            box,
            rows.Select(row =>
            {
                var name = string.IsNullOrWhiteSpace(row.DisplayName)
                    ? row.Description
                    : row.DisplayName;
                return new PickerItem(row.Id, $"{row.Code} — {name}");
            }));
    }

    public static void BindWarehouses(ComboBox box, IEnumerable<Services.WarehouseRow> rows) =>
        BindItems(box, rows.Select(row => new PickerItem(row.Id, $"{row.Code} — {row.Name}")));

    public static void BindParties(ComboBox box, IEnumerable<Services.PartyRow> rows) =>
        BindItems(box, rows.Select(row => new PickerItem(row.Id, $"{row.Name} ({row.TaxId})")));

    public static void BindUnits(ComboBox box, IEnumerable<Services.UnitRow> rows) =>
        BindItems(box, rows.Select(row => new PickerItem(row.Id, $"{row.Code} — {row.Name}")));

    public static void BindLocations(
        ComboBox box,
        IEnumerable<LocationNodeResponse> rows) =>
        BindItems(box, rows.Select(row => new PickerItem(row.Id, $"{row.Code} — {row.Name}")));

    public static void BindPurchaseOrders(ComboBox box, IEnumerable<Services.PurchaseOrderRow> rows) =>
        BindItems(
            box,
            rows.Select(row => new PickerItem(
                row.Id,
                $"{row.Number} — {OfficeUi.StatusToEs(row.Status)}")));

    public static void BindSalesOrders(ComboBox box, IEnumerable<Services.SalesOrderRow> rows) =>
        BindItems(
            box,
            rows.Select(row => new PickerItem(
                row.Id,
                $"{row.Number} — {OfficeUi.StatusToEs(row.Status)}")));

    public static void BindDeliveries(ComboBox box, IEnumerable<DeliverySummaryResponse> rows) =>
        BindItems(
            box,
            rows.Select(row =>
            {
                var so = string.IsNullOrWhiteSpace(row.SalesOrderNumber)
                    ? string.Empty
                    : $" · {row.SalesOrderNumber}";
                return new PickerItem(
                    row.Id,
                    $"{row.Number}{so} · {row.DocumentDate:yyyy-MM-dd}");
            }));

    public static void BindUsers(ComboBox box, IEnumerable<InternalUserResponse> rows) =>
        BindItems(
            box,
            rows.Select(row => new PickerItem(
                row.Id,
                $"{row.UserName} ({string.Join(", ", row.Roles)})")));

    public static void BindGlAccounts(ComboBox box, IEnumerable<GlAccountResponse> rows) =>
        BindItems(box, rows.Select(row => new PickerItem(row.Id, $"{row.Code} — {row.Name}")));

    public static void BindCompanies(ComboBox box, IEnumerable<CompanyResponse> rows) =>
        BindItems(box, rows.Select(row => new PickerItem(row.Id, $"{row.Code} — {row.LegalName}")));

    public static void BindPlants(ComboBox box, IEnumerable<PlantResponse> rows) =>
        BindItems(box, rows.Select(row => new PickerItem(row.Id, $"{row.Code} — {row.Name}")));

    public static void BindConnectors(ComboBox box, IEnumerable<IntegrationConnectorResponse> rows) =>
        BindItems(box, rows.Select(row => new PickerItem(row.Id, $"{row.Code} — {row.Name}")));

    public static void BindPlugins(ComboBox box, IEnumerable<PluginRegistrationResponse> rows) =>
        BindItems(box, rows.Select(row => new PickerItem(row.Id, $"{row.Code} — {row.HookPoint}")));

    public static void BindLots(ComboBox box, IEnumerable<LotResponse> rows, bool includeEmpty = false)
    {
        var items = rows.Select(row =>
        {
            var label = row.ExpiryDate is { } expiry
                ? $"{row.Code} · caduca {expiry:yyyy-MM-dd}"
                : row.Code;
            return new PickerItem(row.Id, label);
        });
        if (includeEmpty)
        {
            items = new[] { new PickerItem(Guid.Empty, "(sin lote)") }.Concat(items);
        }

        BindItems(box, items);
    }

    public static void BindItems(ComboBox box, IEnumerable<PickerItem> items)
    {
        var list = items.ToList();
        var previous = SelectedId(box);
        box.ItemsSource = list;
        box.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(PickerItem.Label));
        if (previous is Guid id)
        {
            var match = list.FindIndex(item => item.Id == id);
            if (match >= 0)
            {
                box.SelectedIndex = match;
                return;
            }
        }

        if (box.SelectedIndex < 0 && list.Count > 0)
        {
            box.SelectedIndex = 0;
        }
    }

    public static bool TryGetId(ComboBox box, out Guid id)
    {
        if (box.SelectedItem is PickerItem item)
        {
            id = item.Id;
            return true;
        }

        id = default;
        return false;
    }

    public static Guid? SelectedId(ComboBox box) =>
        TryGetId(box, out var id) ? id : null;

    public static void SelectId(ComboBox box, Guid id)
    {
        if (box.ItemsSource is not System.Collections.IEnumerable source)
        {
            return;
        }

        var index = 0;
        foreach (var item in source)
        {
            if (item is PickerItem picker && picker.Id == id)
            {
                box.SelectedIndex = index;
                return;
            }

            index++;
        }
    }

    internal sealed record PickerItem(Guid Id, string Label);
}
