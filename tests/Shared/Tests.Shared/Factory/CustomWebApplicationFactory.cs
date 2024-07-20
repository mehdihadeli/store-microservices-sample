using BuildingBlocks.Security.Extensions;
using BuildingBlocks.Security.Jwt;
using BuildingBlocks.Web.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Serilog;
using Serilog.Events;
using Tests.Shared.Auth;
using WebMotions.Fake.Authentication.JwtBearer;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Tests.Shared.Factory;

// https://bartwullems.blogspot.com/2022/01/net-6-minimal-apiintegration-testing.html
// https://milestone.topics.it/2021/04/28/you-wanna-test-http.html
public class CustomWebApplicationFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>, IAsyncLifetime
    where TEntryPoint : class
{
    private ITestOutputHelper? _outputHelper;
    private Action<IWebHostBuilder>? _customWebHostBuilder;
    private Action<IHostBuilder>? _customHostBuilder;
    private Action<HostBuilderContext, IConfigurationBuilder>? _configureAppConfigurations;
    private Action<IServiceCollection>? _testServices;
    private readonly Dictionary<string, string?> _inMemoryConfigs = new();

    public Action<IConfiguration>? ConfigurationAction { get; set; }
    public Action<IServiceCollection>? TestConfigureServices { get; set; }
    public Action<HostBuilderContext, IConfigurationBuilder>? TestConfigureApp { get; set; }

    public ILogger Logger => Services.GetRequiredService<ILogger<CustomWebApplicationFactory<TEntryPoint>>>();

    public void ClearOutputHelper() => _outputHelper = null;

    public void SetOutputHelper(ITestOutputHelper value) => _outputHelper = value;

    public CustomWebApplicationFactory<TEntryPoint> WithTestServices(Action<IServiceCollection> services)
    {
        _testServices += services;

        return this;
    }

    public CustomWebApplicationFactory<TEntryPoint> WithConfigureAppConfigurations(
        Action<HostBuilderContext, IConfigurationBuilder> builder
    )
    {
        _configureAppConfigurations += builder;

        return this;
    }

    public new CustomWebApplicationFactory<TEntryPoint> WithWebHostBuilder(Action<IWebHostBuilder> builder)
    {
        _customWebHostBuilder = builder;

        return this;
    }

    public CustomWebApplicationFactory<TEntryPoint> WithHostBuilder(Action<IHostBuilder> builder)
    {
        _customHostBuilder = builder;

        return this;
    }

    // https://github.com/davidfowl/TodoApi/
    // https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests
    // https://andrewlock.net/converting-integration-tests-to-net-core-3/
    // https://andrewlock.net/exploring-dotnet-6-part-6-supporting-integration-tests-with-webapplicationfactory-in-dotnet-6/
    // https://github.com/dotnet/aspnetcore/pull/33462
    // https://github.com/dotnet/aspnetcore/issues/33846
    // https://milestone.topics.it/2021/04/28/you-wanna-test-http.html
    // https://timdeschryver.dev/blog/refactor-functional-tests-to-support-minimal-web-apis
    // https://timdeschryver.dev/blog/how-to-test-your-csharp-web-api
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("test");
        builder.UseContentRoot(".");

        // UseSerilog on WebHostBuilder is absolute so we should use IHostBuilder
        builder.UseSerilog(
            (ctx, loggerConfiguration) =>
            {
                //https://github.com/trbenning/serilog-sinks-xunit
                if (_outputHelper is not null)
                {
                    loggerConfiguration.WriteTo.TestOutput(
                        _outputHelper,
                        LogEventLevel.Information,
                        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level} - {Message:lj}{NewLine}{Exception}"
                    );
                }
            }
        );

        builder.UseDefaultServiceProvider(
            (env, c) =>
            {
                // Handling Captive Dependency Problem
                // https://ankitvijay.net/2020/03/17/net-core-and-di-beware-of-captive-dependency/
                // https://blog.ploeh.dk/2014/06/02/captive-dependency/
                if (env.HostingEnvironment.IsTest() || env.HostingEnvironment.IsDevelopment())
                    c.ValidateScopes = true;
            }
        );

        // //https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/
        // //https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-providers#json-configuration-provider
        builder.ConfigureAppConfiguration(
            (hostingContext, configurationBuilder) =>
            {
                // configurationBuilder.Sources.Clear();
                // IHostEnvironment env = hostingContext.HostingEnvironment;
                //
                // configurationBuilder
                //     .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                //     .AddJsonFile($"appsettings.{env.EnvironmentName}.json", true, true)
                //     .AddJsonFile("integrationappsettings.json", true, true);
                //
                // var integrationConfig = configurationBuilder.Build();
                //
                // configurationBuilder.AddConfiguration(integrationConfig);

                //// add in-memory configuration instead of using appestings.json and override existing settings and it is accessible via IOptions and Configuration
                //// https://blog.markvincze.com/overriding-configuration-in-asp-net-core-integration-tests/
                configurationBuilder.AddInMemoryCollection(_inMemoryConfigs);

                ConfigurationAction?.Invoke(hostingContext.Configuration);
                _configureAppConfigurations?.Invoke(hostingContext, configurationBuilder);
                TestConfigureApp?.Invoke(hostingContext, configurationBuilder);
            }
        );

        _customHostBuilder?.Invoke(builder);

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // test services will call after registering all application services in program.cs and can override them with `Replace` or `Remove` dependencies
        builder.ConfigureTestServices(services =>
        {
            //// https://andrewlock.net/converting-integration-tests-to-net-core-3/
            //// Don't run IHostedServices when running as a test
            // services.RemoveAll(typeof(IHostedService));

            // TODO: Web could use this in E2E test for running another service during our test
            // https://milestone.topics.it/2021/11/10/http-client-factory-in-integration-testing.html
            // services.Replace(new ServiceDescriptor(typeof(IHttpClientFactory),
            //     new DelegateHttpClientFactory(ClientProvider)));

            //// https://blog.joaograssi.com/posts/2021/asp-net-core-testing-permission-protected-api-endpoints/
            //// This helper just supports jwt Scheme, and for Identity server Scheme will crash so we should disable AddIdentityServer()
            // services.TryAddScoped(_ => CreateAnonymouslyUserMock());
            // services.ReplaceSingleton(CreateCustomTestHttpContextAccessorMock);
            // services.AddTestAuthentication();

            // Or
            // add authentication using a fake jwt bearer - we can use SetAdminUser method to set authenticate user to existing HttContextAccessor
            // https://github.com/webmotions/fake-authentication-jwtbearer
            // https://github.com/webmotions/fake-authentication-jwtbearer/issues/14
            services
                // will skip registering dependencies if exists previously, but will override authentication option inner configure delegate through Configure<AuthenticationOptions>
                .AddAuthentication(options =>
                {
                    // choosing `FakeBearer` scheme (instead of exiting default scheme of application) as default in runtime for authentication and authorization middleware
                    options.DefaultAuthenticateScheme = FakeJwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = FakeJwtBearerDefaults.AuthenticationScheme;
                })
                .AddFakeJwtBearer(c =>
                {
                    // for working fake token this should be set to jwt
                    c.BearerValueType = FakeJwtBearerBearerValueType.Jwt;
                })
                .Services.AddCustomAuthorization(
                    rolePolicies: new List<RolePolicy>
                    {
                        new(Constants.Users.Admin.Role, new List<string> { Constants.Users.Admin.Role }),
                        new(Constants.Users.NormalUser.Role, new List<string> { Constants.Users.NormalUser.Role }),
                    },
                    scheme: FakeJwtBearerDefaults.AuthenticationScheme
                );

            _testServices?.Invoke(services);
            TestConfigureServices?.Invoke(services);
        });

        // //https://github.com/dotnet/aspnetcore/issues/45372
        // wb.Configure(x =>
        // {
        // });

        _customWebHostBuilder?.Invoke(builder);

        base.ConfigureWebHost(builder);
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
    }

    public void AddOverrideInMemoryConfig(string key, string value)
    {
        // overriding app configs with using in-memory configs
        // add in-memory configuration instead of using appestings.json and override existing settings and it is accessible via IOptions and Configuration
        // https://blog.markvincze.com/overriding-configuration-in-asp-net-core-integration-tests/
        _inMemoryConfigs.Add(key, value);
    }

    public void AddOverrideInMemoryConfig(IDictionary<string, string> inMemConfigs)
    {
        // overriding app configs with using in-memory configs
        // add in-memory configuration instead of using appestings.json and override existing settings and it is accessible via IOptions and Configuration
        // https://blog.markvincze.com/overriding-configuration-in-asp-net-core-integration-tests/
        inMemConfigs.ToList().ForEach(x => _inMemoryConfigs.Add(x.Key, x.Value));
    }

    public void AddOverrideEnvKeyValue(string key, string value)
    {
        // overriding app configs with using environments
        Environment.SetEnvironmentVariable(key, value);
    }

    public void AddOverrideEnvKeyValues(IDictionary<string, string> keyValues)
    {
        foreach (var (key, value) in keyValues)
        {
            // overriding app configs with using environments
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static IHttpContextAccessor CreateCustomTestHttpContextAccessorMock(IServiceProvider serviceProvider)
    {
        var httpContextAccessorMock = Substitute.For<IHttpContextAccessor>();
        using var scope = serviceProvider.CreateScope();
        httpContextAccessorMock.HttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider, };

        httpContextAccessorMock.HttpContext.Request.Host = new HostString("localhost", 5000);
        httpContextAccessorMock.HttpContext.Request.Scheme = "http";
        var res = httpContextAccessorMock.HttpContext
            .AuthenticateAsync(Constants.AuthConstants.Scheme)
            .GetAwaiter()
            .GetResult();
        httpContextAccessorMock.HttpContext.User = res.Ticket?.Principal!;
        return httpContextAccessorMock;
    }
}
