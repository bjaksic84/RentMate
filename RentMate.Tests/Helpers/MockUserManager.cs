using Microsoft.AspNetCore.Identity;
using RentMate.Models.Domain;

namespace RentMate.Tests.Helpers;

/// <summary>
/// Builds a <see cref="Mock{UserManager}"/> for <see cref="ApplicationUser"/>.
/// All UserManager members used by services are virtual, so individual tests
/// set up only the methods they exercise.
/// </summary>
public static class MockUserManager
{
    public static Mock<UserManager<ApplicationUser>> Create()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var mgr = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        return mgr;
    }
}
