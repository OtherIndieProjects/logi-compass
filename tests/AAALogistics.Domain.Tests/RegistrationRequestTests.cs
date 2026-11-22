using AAALogistics.Domain.Sales;

namespace AAALogistics.Domain.Tests;

public sealed class RegistrationRequestTests
{
    [Fact]
    public void New_Request_Stays_Pending_And_Normalizes_Codes()
    {
        var created = new DateTimeOffset(2026, 10, 31, 11, 0, 0, TimeSpan.Zero);
        var request = new RegistrationRequest(
            "Cliente Demo",
            "b12345678",
            "Ana Pérez",
            "ana@example.com",
            "ana",
            "Calle 1",
            "Madrid",
            "28001",
            "es",
            created);

        Assert.Equal(RegistrationRequestStatus.Pending, request.Status);
        Assert.Equal("B12345678", request.TaxId);
        Assert.Equal("ES", request.CountryCode);
        Assert.Equal(created, request.CreatedUtc);
        Assert.Null(request.CustomerId);
        Assert.Null(request.RejectionReason);
    }

    [Fact]
    public void Approve_Reject_And_Expire_Follow_The_Pending_State()
    {
        var created = new DateTimeOffset(2026, 10, 31, 11, 0, 0, TimeSpan.Zero);
        var request = new RegistrationRequest(
            "Cliente Demo",
            "B12345678",
            "Ana Pérez",
            "ana@example.com",
            "ana",
            "Calle 1",
            "Madrid",
            "28001",
            "ES",
            created);
        var customerId = Guid.CreateVersion7();
        request.RecordLegalVersion("2026-10");
        request.Approve(customerId, created.AddMinutes(5));

        Assert.Equal(RegistrationRequestStatus.Approved, request.Status);
        Assert.Equal(customerId, request.CustomerId);
        Assert.Equal("2026-10", request.LegalVersion);
        Assert.Throws<InvalidOperationException>(
            () => request.Reject("tarde", created.AddMinutes(6)));

        var rejected = new RegistrationRequest(
            "Cliente Demo",
            "B12345678",
            "Ana Pérez",
            "ana@example.com",
            "ana",
            "Calle 1",
            "Madrid",
            "28001",
            "ES",
            created);
        rejected.Reject("documentación incompleta", created.AddMinutes(1));
        Assert.Equal(RegistrationRequestStatus.Rejected, rejected.Status);
        Assert.Equal("documentación incompleta", rejected.RejectionReason);

        var expired = new RegistrationRequest(
            "Cliente Demo",
            "B12345678",
            "Ana Pérez",
            "ana@example.com",
            "ana",
            "Calle 1",
            "Madrid",
            "28001",
            "ES",
            created);
        expired.Expire(created.AddDays(30));
        Assert.Equal(RegistrationRequestStatus.Expired, expired.Status);
    }

    [Fact]
    public void Invalid_Email_Is_Rejected()
    {
        var error = Assert.Throws<ArgumentException>(() => new RegistrationRequest(
            "Cliente Demo",
            "B123",
            "Ana",
            "sin-correo",
            "ana",
            "Calle 1",
            "Madrid",
            "28001",
            "ES",
            DateTimeOffset.UnixEpoch));

        Assert.Contains("correo", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
