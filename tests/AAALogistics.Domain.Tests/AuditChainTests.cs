using AAALogistics.Domain.Auditing;

namespace AAALogistics.Domain.Tests;

public sealed class AuditChainTests
{
    [Fact]
    public void Two_Entries_Verify_And_Tampering_Fails()
    {
        var first = Create(
            1,
            AuditChainIntegrity.GenesisHash,
            "{\"name\":\"uno\"}");
        var second = Create(2, first.EntryHash, "{\"name\":\"dos\"}");

        var valid = AuditChainIntegrity.Verify([first, second]);
        var tampered = AuditChainIntegrity.Verify(
            [first, second with { EncryptedNewJson = "{\"name\":\"alterado\"}" }]);

        Assert.True(valid.IsValid, valid.Message);
        Assert.False(tampered.IsValid);
        Assert.Equal(2, tampered.InvalidSequence);
    }

    private static AuditChainValue Create(
        long sequence,
        string previousHash,
        string newJson)
    {
        var unsigned = new AuditChainValue(
            sequence,
            previousHash,
            string.Empty,
            "materials",
            Guid.CreateVersion7().ToString("D"),
            "INSERT",
            null,
            newJson,
            Guid.CreateVersion7(),
            "127.0.0.1",
            DateTimeOffset.UtcNow);
        return unsigned with
        {
            EntryHash = AuditChainIntegrity.ComputeHash(unsigned),
        };
    }
}
