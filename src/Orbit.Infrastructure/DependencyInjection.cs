using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Application.Abstractions;
using Orbit.Infrastructure.Authorization;
using Orbit.Infrastructure.Email;
using Orbit.Infrastructure.Identity;
using Orbit.Infrastructure.Messaging;
using Orbit.Infrastructure.Persistence;
using Orbit.Infrastructure.Storage;

namespace Orbit.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required.");

        services.AddDbContext<OrbitDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(OrbitDbContext).Assembly.FullName)));
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IWorkItemRepository, WorkItemRepository>();
        services.AddScoped<IWorkItemLinkRepository, WorkItemLinkRepository>();
        services.AddScoped<IWorkItemCommentRepository, WorkItemCommentRepository>();
        services.AddScoped<IAttachmentRepository, AttachmentRepository>();
        services.AddScoped<IWorkItemWatcherRepository, WorkItemWatcherRepository>();
        services.AddScoped<ITenantMembershipRepository, TenantMembershipRepository>();
        services.AddScoped<IProjectRoleRepository, ProjectRoleRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<ITeamMembershipRepository, TeamMembershipRepository>();
        services.AddScoped<IDirectoryGroupRepository, DirectoryGroupRepository>();
        services.AddScoped<IGroupMembershipRepository, GroupMembershipRepository>();
        services.AddScoped<IProjectGroupRoleRepository, ProjectGroupRoleRepository>();
        services.AddScoped<IBoardRepository, BoardRepository>();
        services.AddScoped<ISprintRepository, SprintRepository>();
        services.AddScoped<ISprintMembershipRepository, SprintMembershipRepository>();
        services.AddScoped<ISprintCompletionOperationRepository, SprintCompletionOperationRepository>();
        services.AddScoped<ISprintScopeFactRepository, SprintScopeFactRepository>();
        services.AddScoped<ITenantAuthorization, TenantAuthorization>();
        services.AddScoped<ITenantOwnerLock, TenantOwnerLock>();
        services.AddScoped<IAuthorizationContextCache, AuthorizationContextCache>();
        services.AddScoped<IBootstrapRepository, BootstrapRepository>();
        services.AddScoped<ISignUpRepository, SignUpRepository>();
        services.AddScoped<IWorkspaceProvisioningRepository, WorkspaceProvisioningRepository>();
        services.AddScoped<ISettingsRepository, SettingsRepository>();
        services.AddScoped<IAuthenticationRepository, AuthenticationRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IWorkspaceInvitationRepository, WorkspaceInvitationRepository>();
        services.AddScoped<IWorkItemTypeRepository, WorkItemTypeRepository>();
        services.AddScoped<ICustomFieldRepository, CustomFieldRepository>();
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.Configure<ObjectStorageOptions>(configuration.GetSection(ObjectStorageOptions.SectionName));
        services.AddSingleton<IObjectStorageService, S3ObjectStorageService>();
        services.AddHostedService<ObjectStorageBucketInitializer>();
        services.AddScoped<OutboxEmailProcessor>();
        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.Configure<LocalTokenOptions>(configuration.GetSection(LocalTokenOptions.SectionName));
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddSingleton<IExternalIdentityTokenValidator, ExternalIdentityTokenValidator>();
        services.Configure<GoogleOAuthOptions>(configuration.GetSection(GoogleOAuthOptions.SectionName));
        services.AddHttpClient<IGoogleOAuthClient, GoogleOAuthClient>();
        services.AddSingleton<IGoogleIdTokenValidator, GoogleIdTokenValidator>();
        services.AddSingleton<IOAuthStateCodec, OAuthStateCodec>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<OrbitDbContext>());
        services.AddScoped<RuntimeDatabaseSecurityValidator>();
        services.AddSingleton(TimeProvider.System);

        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddStackExchangeRedisCache(options => options.Configuration = redisConnection);
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        return services;
    }
}
