using AAALogistics.Integrations.Sap;

namespace AAALogistics.Domain.Tests;

public sealed class SapODataParseTests
{
    [Fact]
    public void ParseMaterials_Reads_OData_V4_Value_Array()
    {
        const string json = """
            {
              "value": [
                { "Product": "MAT-1", "ProductDescription": "Tornillo" },
                { "Product": "MAT-2", "Description": "Tuerca" }
              ]
            }
            """;

        var rows = SapODataClient.ParseMaterials(json);

        Assert.Equal(2, rows.Count);
        Assert.Equal("MAT-1", rows[0].MaterialNumber);
        Assert.Equal("Tornillo", rows[0].Description);
        Assert.Equal("MAT-2", rows[1].MaterialNumber);
    }

    [Fact]
    public void ParseMaterials_Reads_Classic_D_Results()
    {
        const string json = """
            {
              "d": {
                "results": [
                  { "Material": "1000", "MaterialDescription": "Demo" }
                ]
              }
            }
            """;

        var rows = SapODataClient.ParseMaterials(json);

        Assert.Single(rows);
        Assert.Equal("1000", rows[0].MaterialNumber);
        Assert.Equal("Demo", rows[0].Description);
    }
}
