using Microsoft.AspNetCore.Identity;
using RentMate.Tests.Helpers;

namespace RentMate.Tests.Services;

public class ProfileCompletionServiceTests
{
    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    [Fact]
    public async Task FullProfile_Returns100()
    {
        var user = EntityFactory.CreateUser(
            firstName: "John", lastName: "Doe", city: "Ljubljana",
            profilePictureUrl: "https://pic.jpg", bio: "Hello",
            isPhoneVerified: true, isGovernmentIdVerified: true,
            hasPaymentMethodAdded: true);

        var um = CreateUserManagerMock();
        um.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);
        var sut = new ProfileCompletionService(um.Object);

        var result = await sut.GetCompletionStatusAsync(user.Id);

        Assert.Equal(100, result.Percentage);
        Assert.Empty(result.Tips);
    }

    [Fact]
    public async Task EmptyProfile_Returns0()
    {
        var user = EntityFactory.CreateUser(
            firstName: null, lastName: null, city: null,
            profilePictureUrl: null, bio: null,
            isPhoneVerified: false, isGovernmentIdVerified: false,
            hasPaymentMethodAdded: false);

        var um = CreateUserManagerMock();
        um.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);
        var sut = new ProfileCompletionService(um.Object);

        var result = await sut.GetCompletionStatusAsync(user.Id);

        Assert.Equal(0, result.Percentage);
        Assert.Equal(7, result.Tips.Count);
    }

    [Fact]
    public async Task NameOnly_Returns15()
    {
        var user = EntityFactory.CreateUser(
            firstName: "John", lastName: "Doe", city: null,
            profilePictureUrl: null, bio: null,
            isPhoneVerified: false, isGovernmentIdVerified: false,
            hasPaymentMethodAdded: false);

        var um = CreateUserManagerMock();
        um.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);
        var sut = new ProfileCompletionService(um.Object);

        var result = await sut.GetCompletionStatusAsync(user.Id);

        Assert.Equal(15, result.Percentage);
    }

    [Fact]
    public async Task LocationOnly_Returns20()
    {
        var user = EntityFactory.CreateUser(
            firstName: null, lastName: null, city: "Maribor",
            profilePictureUrl: null, bio: null,
            isPhoneVerified: false, isGovernmentIdVerified: false,
            hasPaymentMethodAdded: false);

        var um = CreateUserManagerMock();
        um.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);
        var sut = new ProfileCompletionService(um.Object);

        var result = await sut.GetCompletionStatusAsync(user.Id);

        Assert.Equal(20, result.Percentage);
    }

    [Fact]
    public async Task UserNotFound_Returns0()
    {
        var um = CreateUserManagerMock();
        um.Setup(m => m.FindByIdAsync("missing")).ReturnsAsync((ApplicationUser?)null);
        var sut = new ProfileCompletionService(um.Object);

        var result = await sut.GetCompletionStatusAsync("missing");

        Assert.Equal(0, result.Percentage);
    }

    [Fact]
    public async Task TipsOrderedByPriority()
    {
        var user = EntityFactory.CreateUser(
            firstName: null, lastName: null, city: null,
            profilePictureUrl: null, bio: null,
            isPhoneVerified: false, isGovernmentIdVerified: false,
            hasPaymentMethodAdded: false);

        var um = CreateUserManagerMock();
        um.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);
        var sut = new ProfileCompletionService(um.Object);

        var result = await sut.GetCompletionStatusAsync(user.Id);

        Assert.Contains("name", result.Tips[0].Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("location", result.Tips[1].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BoolFlags_MatchUserProperties()
    {
        var user = EntityFactory.CreateUser(
            firstName: "A", lastName: "B", city: "C",
            profilePictureUrl: null, bio: "D",
            isPhoneVerified: true, isGovernmentIdVerified: false,
            hasPaymentMethodAdded: true, onboardingCompleted: true);

        var um = CreateUserManagerMock();
        um.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);
        var sut = new ProfileCompletionService(um.Object);

        var result = await sut.GetCompletionStatusAsync(user.Id);

        Assert.True(result.HasName);
        Assert.True(result.HasLocation);
        Assert.False(result.HasProfilePicture);
        Assert.True(result.IsPhoneVerified);
        Assert.False(result.IsGovernmentIdVerified);
        Assert.True(result.HasPaymentMethod);
        Assert.True(result.HasBio);
        Assert.True(result.OnboardingCompleted);
    }
}
