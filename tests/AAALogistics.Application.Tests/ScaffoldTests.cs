using AAALogistics.Application.Authentication;

namespace AAALogistics.Application.Tests;

public sealed class AuthenticationContractTests
{
    [Fact]
    public void Authenticated_User_Preserves_Audience_Relevant_Data()
    {
        var userId = Guid.CreateVersion7();
        var customerId = Guid.CreateVersion7();

        var user = new AuthenticatedUser(
            userId,
            "cliente",
            customerId,
            ["Cliente"],
            ["SalesOrder.Create"],
            "stamp");

        Assert.Equal(userId, user.Id);
        Assert.Equal(customerId, user.CustomerId);
        Assert.Contains("Cliente", user.Roles);
        Assert.NotEqual(
            AuthorizationPolicyNames.InternalApi,
            AuthorizationPolicyNames.PortalApi);
    }
}
