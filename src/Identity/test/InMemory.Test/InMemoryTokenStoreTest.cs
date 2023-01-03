// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity.InMemory.Test;
using Microsoft.AspNetCore.Identity.Test;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Identity.InMemory;

public class InMemoryTokenStoreTest : IClassFixture<InMemoryUserStoreTest.Fixture>
{
    /// <summary>
    /// Configure the service collection used for tests.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="context"></param>
    protected virtual void SetupIdentityServices(IServiceCollection services, object context)
        => SetupBuilder(services, context);

    /// <summary>
    /// Configure the service collection used for tests.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="context"></param>
    protected virtual IdentityBuilder SetupBuilder(IServiceCollection services, object context)
    {
        services.AddHttpContextAccessor();
        // An example of what the expected schema looks like
        // "Authentication": {
        //     "Schemes": {
        //       "Identity.Bearer": {
        //         "Audiences": [ "", ""]
        //         "Issuer": "",
        // An example of what the expected signing keys (JWKs) looks like
        //"SigningCredentials": {
        //  "kty": "oct",
        //  "alg": "HS256",
        //  "kid": "randomguid",
        //  "k": "(G+KbPeShVmYq3t6w9z$C&F)J@McQfTj"
        //}
        //       }
        //     }
        //   }

        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Schemes:Identity.Bearer:Issuer"] = "dotnet-user-jwts",
                ["Authentication:Schemes:Identity.Bearer:Audiences:0:Value"] = "whateverAudience",
                ["Authentication:Schemes:Identity.Bearer:SigningCredentials:kty"] = "oct",
                ["Authentication:Schemes:Identity.Bearer:SigningCredentials:alg"] = "HS256",
                ["Authentication:Schemes:Identity.Bearer:SigningCredentials:kid"] = "someguid",
            })
            .Build());

        services.AddAuthentication();
        services.AddDataProtection();
        services.AddSingleton<IDataProtectionProvider, EphemeralDataProtectionProvider>();
        var builder = services.AddDefaultIdentityBearer<PocoUser>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.User.AllowedUserNameCharacters = null;
        }).AddDefaultTokenProviders();
        services.AddSingleton(_ => (ITokenStore<IdentityToken>)context);
        AddUserStore(services, context);
        services.AddLogging();
        services.AddSingleton<ILogger<UserManager<PocoUser>>>(new TestLogger<UserManager<PocoUser>>());
        return builder;
    }

    protected void AddUserStore(IServiceCollection services, object context = null)
        => services.AddSingleton<IUserStore<PocoUser>>((InMemoryUserStore<PocoUser>)context);

    protected void SetUserPasswordHash(PocoUser user, string hashedPassword)
        => user.PasswordHash = hashedPassword;

    protected PocoUser CreateTestUser(string namePrefix = "", string email = "", string phoneNumber = "",
        bool lockoutEnabled = false, DateTimeOffset? lockoutEnd = default, bool useNamePrefixAsUserName = false)
    {
        return new PocoUser
        {
            UserName = useNamePrefixAsUserName ? namePrefix : string.Format(CultureInfo.InvariantCulture, "{0}{1}", namePrefix, Guid.NewGuid()),
            Email = email,
            PhoneNumber = phoneNumber,
            LockoutEnabled = lockoutEnabled,
            LockoutEnd = lockoutEnd
        };
    }

    protected Expression<Func<PocoUser, bool>> UserNameEqualsPredicate(string userName) => u => u.UserName == userName;

    protected Expression<Func<PocoUser, bool>> UserNameStartsWithPredicate(string userName) => u => u.UserName.StartsWith(userName, StringComparison.Ordinal);

    protected object CreateTestContext()
    {
        return new InMemoryTokenStore<PocoUser, PocoRole>();
    }

    /// <summary>
    /// Creates the user manager used for tests.
    /// </summary>
    /// <param name="context">The context that will be passed into the store, typically a db context.</param>
    /// <param name="services">The service collection to use, optional.</param>
    /// <param name="configureServices">Delegate used to configure the services, optional.</param>
    /// <returns>The user manager to use for tests.</returns>
    protected virtual TokenManager<PocoUser> CreateManager(object context = null, IServiceCollection services = null, Action<IServiceCollection> configureServices = null)
    {
        if (services == null)
        {
            services = new ServiceCollection();
        }
        if (context == null)
        {
            context = CreateTestContext();
        }
        SetupIdentityServices(services, context);
        configureServices?.Invoke(services);
        return services.BuildServiceProvider().GetService<TokenManager<PocoUser>>();
    }

    /// <summary>
    /// Test.
    /// </summary>
    /// <returns>Task</returns>
    [Fact]
    public async Task CanRefreshTokens()
    {
        var manager = CreateManager();
        var user = CreateTestUser();
        IdentityResultAssert.IsSuccess(await manager.UserManager.CreateAsync(user));

        var token = await manager.GetRefreshTokenAsync(user);
        Assert.NotNull(token);

        (var access, var refresh) = await manager.RefreshTokensAsync(token);

        Assert.NotNull(access);
        Assert.NotNull(refresh);
    }

    /// <summary>
    /// Test.
    /// </summary>
    /// <returns>Task</returns>
    [Fact]
    public async Task ExpiredRefreshTokensFails()
    {
        var clock = new TestClock();
        var manager = CreateManager(configureServices: s => s.AddSingleton<ISystemClock>(clock));
        var user = CreateTestUser();
        IdentityResultAssert.IsSuccess(await manager.UserManager.CreateAsync(user));

        var token = await manager.GetRefreshTokenAsync(user);
        Assert.NotNull(token);

        // Advance clock past expiration
        clock.UtcNow = DateTime.UtcNow.AddDays(2);

        (var access, var refresh) = await manager.RefreshTokensAsync(token);

        Assert.Null(access);
        Assert.Null(refresh);
    }

    /// <summary>
    /// Test.
    /// </summary>
    /// <returns>Task</returns>
    [Fact]
    public async Task RevokedRefreshTokenFails()
    {
        var manager = CreateManager();
        var user = CreateTestUser();
        IdentityResultAssert.IsSuccess(await manager.UserManager.CreateAsync(user));

        var token = await manager.GetRefreshTokenAsync(user);
        Assert.NotNull(token);

        await manager.RevokeRefreshAsync(user, token);

        (var access, var refresh) = await manager.RefreshTokensAsync(token);

        Assert.Null(access);
        Assert.Null(refresh);
    }

    /// <summary>
    /// Test.
    /// </summary>
    /// <returns>Task</returns>
    [Fact]
    public async Task DeleteUserRemovesRefreshToken()
    {
        var manager = CreateManager();
        var user = CreateTestUser();
        IdentityResultAssert.IsSuccess(await manager.UserManager.CreateAsync(user));

        var token = await manager.GetRefreshTokenAsync(user);
        Assert.NotNull(token);

        IdentityResultAssert.IsSuccess(await manager.UserManager.DeleteAsync(user));
        var userId = await manager.UserManager.GetUserIdAsync(user);
        Assert.Null(await manager.UserManager.FindByIdAsync(userId));
        Assert.Null(await manager.Store.FindAsync("", token, CancellationToken.None));
    }
}