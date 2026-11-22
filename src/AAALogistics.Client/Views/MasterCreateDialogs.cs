using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using AAALogistics.Client.Services;
using AAALogistics.Contracts;
using Thickness = Avalonia.Thickness;

namespace AAALogistics.Client.Views;

internal static class MasterCreateDialogs
{
    public static async Task<CreateMaterialRequest?> PromptMaterialAsync(
        Window owner,
        IReadOnlyList<UnitRow> units)
    {
        if (units.Count == 0)
        {
            await AppDialog.ShowMessageAsync(
                owner,
                "Sin unidades",
                "No hay unidades de medida. Cree unidades (o use el seed UN/KG/L/PCS) antes de crear un material.");
            return null;
        }

        var code = OfficeUi.Box("Código corto único, ej. MAT-100", 220);
        var displayName = OfficeUi.Box("Nombre que verá todo el mundo", 280);
        var unitBox = new ComboBox { Width = 220, MinHeight = 28 };
        PickerHelpers.BindUnits(unitBox, units);
        unitBox.SelectedIndex = 0;
        var price = OfficeUi.Box("0", 100);
        price.Text = "0";
        var currency = OfficeUi.Box("EUR", 80);
        currency.Text = "EUR";
        var externalId = OfficeUi.Box("Id externo (opcional)", 220);
        var requiresLot = new CheckBox
        {
            Content = "Requiere lote (FEFO = primero caduca, primero sale)",
            IsChecked = false,
        };

        var result = await ShowFormAsync(
            owner,
            "Nuevo material",
            OfficeUi.LabeledField("Código", code),
            OfficeUi.LabeledField(
                "Nombre",
                displayName,
                helpKey: OfficeHelpTexts.Keys.FieldMaterialName),
            OfficeUi.LabeledField("Unidad", unitBox, helpKey: OfficeHelpTexts.Keys.FieldUnit),
            OfficeUi.LabeledField("Precio", price, helpKey: OfficeHelpTexts.Keys.FieldCatalogPrice),
            OfficeUi.LabeledField("Moneda", currency, helpKey: OfficeHelpTexts.Keys.FieldCurrency),
            OfficeUi.LabeledField("Id externo", externalId, helpKey: OfficeHelpTexts.Keys.FieldExternalId),
            OfficeUi.CheckWithHelp(requiresLot, OfficeHelpTexts.Keys.FieldRequiresLot));

        if (!result)
        {
            return null;
        }

        if (!PickerHelpers.TryGetId(unitBox, out var unitId))
        {
            await AppDialog.ShowMessageAsync(
                owner,
                "Unidad obligatoria",
                "Seleccione una unidad de medida. No se puede guardar el material sin unidad.");
            return null;
        }

        if (!UiHelpers.TryParseDecimal(price.Text, out var priceValue)
            || string.IsNullOrWhiteSpace(code.Text)
            || string.IsNullOrWhiteSpace(displayName.Text))
        {
            return null;
        }

        var name = displayName.Text.Trim();
        return new CreateMaterialRequest(
            code.Text.Trim(),
            name,
            unitId,
            priceValue,
            string.IsNullOrWhiteSpace(currency.Text) ? "EUR" : currency.Text.Trim(),
            string.IsNullOrWhiteSpace(externalId.Text) ? null : externalId.Text.Trim(),
            requiresLot.IsChecked == true,
            name);
    }

    public static async Task<UpdateMaterialRequest?> PromptEditMaterialAsync(
        Window owner,
        MaterialRow existing)
    {
        var displayName = OfficeUi.Box("Nombre visible", 280);
        displayName.Text = string.IsNullOrWhiteSpace(existing.DisplayName)
            ? existing.Description
            : existing.DisplayName;
        var price = OfficeUi.Box("0", 100);
        price.Text = existing.CatalogPrice.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var currency = OfficeUi.Box("EUR", 80);
        currency.Text = existing.Currency;
        var externalId = OfficeUi.Box("Id externo (opcional)", 220);
        externalId.Text = existing.ExternalId ?? string.Empty;
        var requiresLot = new CheckBox
        {
            Content = "Requiere lote (FEFO = primero caduca, primero sale)",
            IsChecked = existing.RequiresLot,
        };
        var isActive = new CheckBox { Content = "Activo", IsChecked = existing.IsActive };

        var result = await ShowFormAsync(
            owner,
            $"Editar material {existing.Code}",
            OfficeUi.Muted($"Unidad: {existing.UnitCode} (no editable aquí)"),
            OfficeUi.LabeledField(
                "Nombre",
                displayName,
                helpKey: OfficeHelpTexts.Keys.FieldMaterialName),
            OfficeUi.LabeledField("Precio", price, helpKey: OfficeHelpTexts.Keys.FieldCatalogPrice),
            OfficeUi.LabeledField("Moneda", currency, helpKey: OfficeHelpTexts.Keys.FieldCurrency),
            OfficeUi.LabeledField("Id externo", externalId, helpKey: OfficeHelpTexts.Keys.FieldExternalId),
            OfficeUi.CheckWithHelp(requiresLot, OfficeHelpTexts.Keys.FieldRequiresLot),
            isActive);

        if (!result
            || !UiHelpers.TryParseDecimal(price.Text, out var priceValue)
            || string.IsNullOrWhiteSpace(displayName.Text))
        {
            return null;
        }

        var name = displayName.Text.Trim();
        return new UpdateMaterialRequest(
            name,
            priceValue,
            string.IsNullOrWhiteSpace(currency.Text) ? "EUR" : currency.Text.Trim(),
            isActive.IsChecked == true,
            string.IsNullOrWhiteSpace(externalId.Text) ? null : externalId.Text.Trim(),
            requiresLot.IsChecked == true,
            name);
    }

    public static async Task<CreateUnitOfMeasureRequest?> PromptUnitAsync(Window owner)
    {
        var code = OfficeUi.Box("Código corto, ej. UN / KG", 140);
        var name = OfficeUi.Box("Nombre de la unidad, ej. Kilogramo", 240);
        var scale = OfficeUi.Box("0", 80);
        scale.Text = "0";
        var result = await ShowFormAsync(
            owner,
            "Nueva unidad de medida",
            OfficeUi.LabeledField("Código", code),
            OfficeUi.LabeledField("Nombre", name),
            OfficeUi.LabeledField("Decimales", scale, helpKey: OfficeHelpTexts.Keys.FieldDecimals));
        if (!result
            || string.IsNullOrWhiteSpace(code.Text)
            || string.IsNullOrWhiteSpace(name.Text))
        {
            return null;
        }

        _ = int.TryParse(scale.Text?.Trim(), out var decimals);
        return new CreateUnitOfMeasureRequest(code.Text.Trim(), name.Text.Trim(), decimals);
    }

    public static async Task<UpdateUnitOfMeasureRequest?> PromptEditUnitAsync(
        Window owner,
        UnitRow existing)
    {
        var name = OfficeUi.Box("Nombre", 220);
        name.Text = existing.Name;
        var scale = OfficeUi.Box("0", 80);
        scale.Text = existing.DecimalScale.ToString();
        var result = await ShowFormAsync(
            owner,
            $"Editar unidad {existing.Code}",
            OfficeUi.LabeledField("Nombre", name),
            OfficeUi.LabeledField("Decimales", scale, helpKey: OfficeHelpTexts.Keys.FieldDecimals));
        if (!result || string.IsNullOrWhiteSpace(name.Text))
        {
            return null;
        }

        _ = int.TryParse(scale.Text?.Trim(), out var decimals);
        return new UpdateUnitOfMeasureRequest(name.Text.Trim(), decimals);
    }

    public static async Task<CreateWarehouseRequest?> PromptWarehouseAsync(Window owner)
    {
        var code = OfficeUi.Box("Código corto único, ej. WH01", 160);
        var name = OfficeUi.Box("Nombre del almacén", 220);
        var kind = CreateSiteKindBox("Generic");
        var result = await ShowFormAsync(
            owner,
            "Nuevo almacén",
            OfficeUi.LabeledField("Código", code),
            OfficeUi.LabeledField("Nombre", name),
            OfficeUi.LabeledField("Tipo", kind, helpKey: OfficeHelpTexts.Keys.FieldWarehouseKind));
        if (!result
            || string.IsNullOrWhiteSpace(code.Text)
            || string.IsNullOrWhiteSpace(name.Text))
        {
            return null;
        }

        return new CreateWarehouseRequest(
            code.Text.Trim(),
            name.Text.Trim(),
            SelectedSiteKind(kind));
    }

    public static async Task<UpdateWarehouseRequest?> PromptEditWarehouseAsync(
        Window owner,
        WarehouseRow existing)
    {
        var name = OfficeUi.Box("Nombre del almacén", 220);
        name.Text = existing.Name;
        var kind = CreateSiteKindBox(existing.SiteKind);
        var result = await ShowFormAsync(
            owner,
            $"Editar almacén {existing.Code}",
            OfficeUi.LabeledField("Nombre", name),
            OfficeUi.LabeledField("Tipo", kind, helpKey: OfficeHelpTexts.Keys.FieldWarehouseKind));
        if (!result || string.IsNullOrWhiteSpace(name.Text))
        {
            return null;
        }

        return new UpdateWarehouseRequest(
            name.Text.Trim(),
            SelectedSiteKind(kind));
    }

    public static async Task<CreatePartyRequest?> PromptPartyAsync(
        Window owner,
        string title,
        bool includeLeadTime = false)
    {
        var name = OfficeUi.Box("Razón social / nombre", 260);
        var taxId = OfficeUi.Box("NIF / CIF", 140);
        var address = OfficeUi.Box("Dirección", 260);
        var address2 = OfficeUi.Box("Dirección 2 (opcional)", 260);
        var city = OfficeUi.Box("Ciudad", 160);
        var region = OfficeUi.Box("Región / provincia", 160);
        var postal = OfficeUi.Box("CP", 80);
        postal.Text = "00000";
        var country = OfficeUi.Box("ES", 60);
        country.Text = "ES";
        var externalId = OfficeUi.Box("Id externo (opcional)", 160);
        var leadTime = OfficeUi.Box("días", 80);
        var fields = new List<Control>
        {
            OfficeUi.LabeledField("Nombre", name),
            OfficeUi.LabeledField("NIF/CIF", taxId, helpKey: OfficeHelpTexts.Keys.FieldTaxIdNif),
            OfficeUi.LabeledField("Dirección", address),
            OfficeUi.LabeledField("Dirección 2", address2),
            OfficeUi.LabeledField("Ciudad", city),
            OfficeUi.LabeledField("Región", region),
            OfficeUi.LabeledField("C.P.", postal),
            OfficeUi.LabeledField("País", country),
            OfficeUi.LabeledField("Id externo", externalId, helpKey: OfficeHelpTexts.Keys.FieldExternalId),
        };
        if (includeLeadTime)
        {
            fields.Add(OfficeUi.LabeledField(
                "Plazo (días)",
                leadTime,
                helpKey: OfficeHelpTexts.Keys.FieldLeadTime));
        }

        var result = await ShowFormAsync(owner, title, fields.ToArray());
        if (!result
            || string.IsNullOrWhiteSpace(name.Text)
            || string.IsNullOrWhiteSpace(taxId.Text)
            || string.IsNullOrWhiteSpace(city.Text))
        {
            return null;
        }

        int? lead = null;
        if (includeLeadTime
            && !string.IsNullOrWhiteSpace(leadTime.Text)
            && int.TryParse(leadTime.Text.Trim(), out var parsedLead))
        {
            lead = parsedLead;
        }

        return new CreatePartyRequest(
            name.Text.Trim(),
            taxId.Text.Trim(),
            string.IsNullOrWhiteSpace(address.Text) ? "N/A" : address.Text.Trim(),
            string.IsNullOrWhiteSpace(address2.Text) ? null : address2.Text.Trim(),
            city.Text.Trim(),
            string.IsNullOrWhiteSpace(region.Text) ? null : region.Text.Trim(),
            string.IsNullOrWhiteSpace(postal.Text) ? "00000" : postal.Text.Trim(),
            string.IsNullOrWhiteSpace(country.Text) ? "ES" : country.Text.Trim(),
            string.IsNullOrWhiteSpace(externalId.Text) ? null : externalId.Text.Trim(),
            lead);
    }

    public static async Task<UpdatePartyRequest?> PromptEditPartyAsync(
        Window owner,
        string title,
        PartyRow existing,
        bool includeLeadTime = false)
    {
        var name = OfficeUi.Box("Razón social / nombre", 260);
        name.Text = existing.Name;
        var address = OfficeUi.Box("Dirección", 260);
        address.Text = existing.AddressLine1;
        var address2 = OfficeUi.Box("Dirección 2 (opcional)", 260);
        address2.Text = existing.AddressLine2 ?? string.Empty;
        var city = OfficeUi.Box("Ciudad", 160);
        city.Text = existing.City;
        var region = OfficeUi.Box("Región / provincia", 160);
        region.Text = existing.Region ?? string.Empty;
        var postal = OfficeUi.Box("CP", 80);
        postal.Text = string.IsNullOrWhiteSpace(existing.PostalCode)
            ? "00000"
            : existing.PostalCode;
        var country = OfficeUi.Box("ES", 60);
        country.Text = existing.CountryCode;
        var externalId = OfficeUi.Box("Id externo (opcional)", 160);
        externalId.Text = existing.ExternalId ?? string.Empty;
        var isActive = new CheckBox { Content = "Activo", IsChecked = existing.IsActive };
        var leadTime = OfficeUi.Box("días", 80);
        leadTime.Text = existing.LeadTimeDays?.ToString() ?? string.Empty;

        var fields = new List<Control>
        {
            OfficeUi.Muted($"NIF/CIF: {existing.TaxId}"),
            OfficeUi.LabeledField("Nombre", name),
            OfficeUi.LabeledField("Dirección", address),
            OfficeUi.LabeledField("Dirección 2", address2),
            OfficeUi.LabeledField("Ciudad", city),
            OfficeUi.LabeledField("Región", region),
            OfficeUi.LabeledField("C.P.", postal),
            OfficeUi.LabeledField("País", country),
            OfficeUi.LabeledField("Id externo", externalId, helpKey: OfficeHelpTexts.Keys.FieldExternalId),
            isActive,
        };
        if (includeLeadTime)
        {
            fields.Add(OfficeUi.LabeledField(
                "Plazo (días)",
                leadTime,
                helpKey: OfficeHelpTexts.Keys.FieldLeadTime));
        }

        var result = await ShowFormAsync(owner, title, fields.ToArray());
        if (!result
            || string.IsNullOrWhiteSpace(name.Text)
            || string.IsNullOrWhiteSpace(city.Text))
        {
            return null;
        }

        int? lead = null;
        if (includeLeadTime
            && !string.IsNullOrWhiteSpace(leadTime.Text)
            && int.TryParse(leadTime.Text.Trim(), out var parsedLead))
        {
            lead = parsedLead;
        }

        return new UpdatePartyRequest(
            name.Text.Trim(),
            string.IsNullOrWhiteSpace(address.Text) ? "N/A" : address.Text.Trim(),
            string.IsNullOrWhiteSpace(address2.Text) ? null : address2.Text.Trim(),
            city.Text.Trim(),
            string.IsNullOrWhiteSpace(region.Text) ? null : region.Text.Trim(),
            string.IsNullOrWhiteSpace(postal.Text) ? "00000" : postal.Text.Trim(),
            string.IsNullOrWhiteSpace(country.Text) ? "ES" : country.Text.Trim(),
            isActive.IsChecked == true,
            string.IsNullOrWhiteSpace(externalId.Text) ? null : externalId.Text.Trim(),
            lead);
    }

    public static async Task<(Guid From, Guid To, decimal Factor)?> PromptUnitConversionAsync(
        Window owner,
        IReadOnlyList<UnitRow> units)
    {
        if (units.Count < 2)
        {
            await AppDialog.ShowMessageAsync(
                owner,
                "Conversiones UoM",
                "Se necesitan al menos dos unidades para definir una conversión.");
            return null;
        }

        var fromBox = new ComboBox { Width = 220, MinHeight = 28 };
        var toBox = new ComboBox { Width = 220, MinHeight = 28 };
        PickerHelpers.BindUnits(fromBox, units);
        PickerHelpers.BindUnits(toBox, units);
        fromBox.SelectedIndex = 0;
        toBox.SelectedIndex = Math.Min(1, units.Count - 1);
        var factor = OfficeUi.Box("1", 100);
        factor.Text = "1";
        var result = await ShowFormAsync(
            owner,
            "Conversión de unidades",
            OfficeUi.LabeledField("Desde", fromBox, helpKey: OfficeHelpTexts.Keys.FieldUomConversion),
            OfficeUi.LabeledField("Hacia", toBox),
            OfficeUi.LabeledField("Factor", factor, helpKey: OfficeHelpTexts.Keys.FieldUomConversion));
        if (!result
            || !PickerHelpers.TryGetId(fromBox, out var from)
            || !PickerHelpers.TryGetId(toBox, out var to)
            || !UiHelpers.TryParseDecimal(factor.Text, out var factorValue)
            || factorValue <= 0)
        {
            return null;
        }

        return (from, to, factorValue);
    }

    private static readonly (string Value, string Label)[] SiteKinds =
    [
        ("Generic", "Genérico (si duda, elija este)"),
        ("Campa", "Campa (exterior / a cielo abierto)"),
        ("Nave", "Nave (cubierta / nave industrial)"),
    ];

    private static ComboBox CreateSiteKindBox(string? selected)
    {
        var kind = new ComboBox
        {
            Width = 280,
            MinHeight = 28,
        };
        foreach (var (value, label) in SiteKinds)
        {
            kind.Items.Add(new ComboBoxItem { Content = label, Tag = value });
        }

        var index = Array.FindIndex(
            SiteKinds,
            entry => string.Equals(entry.Value, selected, StringComparison.OrdinalIgnoreCase));
        kind.SelectedIndex = index >= 0 ? index : 0;
        return kind;
    }

    private static string SelectedSiteKind(ComboBox kind) =>
        kind.SelectedItem is ComboBoxItem { Tag: string value } && !string.IsNullOrWhiteSpace(value)
            ? value
            : "Generic";

    private static async Task<bool> ShowFormAsync(
        Window owner,
        string title,
        params Control[] fields)
    {
        var accepted = false;
        var ok = new Button { Content = "Guardar", Classes = { "primary" }, MinWidth = 96 };
        var cancel = new Button { Content = "Cancelar", MinWidth = 96 };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0),
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        var form = new StackPanel { Spacing = 10, Margin = new Thickness(20) };
        form.Children.Add(OfficeUi.Muted("Complete los campos obligatorios y pulse Guardar."));
        foreach (var field in fields)
        {
            form.Children.Add(field);
        }

        form.Children.Add(buttons);
        var dialog = new Window
        {
            Title = title,
            Width = 560,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = BrandTheme.Brush(BrandTheme.Background),
            Content = form,
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
