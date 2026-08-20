using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Application.Abstractions;
using Orbit.Application.WorkItems;
using Orbit.Domain.Configuration;
using Orbit.Domain.WorkItems;
using Orbit.Infrastructure.Messaging;
using Orbit.Infrastructure.Persistence;

namespace Orbit.IntegrationTests;

/// <summary>
/// Exercises the real presign -> PUT -> confirm -> list -> download -> delete flow against the
/// actual local MinIO container (not a mock), proving the AWS SDK presigned URLs it mints are
/// genuinely usable by a bare HttpClient the way a browser would use them.
/// </summary>
public sealed class AttachmentTests : IClassFixture<OrbitApiFactory>
{
    private readonly OrbitApiFactory _factory;
    private readonly HttpClient _client;
    private static readonly HttpClient RawHttpClient = new();

    public AttachmentTests(OrbitApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Work item creation validates against the tenant's seeded type registry
    /// (§13.5 "stable work-item type registry"); a bare <c>X-Tenant-Id</c> header used directly
    /// against a fresh random tenant, without going through workspace bootstrap, has no registry
    /// rows yet, so tests seed them the same way <c>WorkspaceProvisioningRepository</c> does.
    /// </summary>
    private async Task SeedWorkItemTypeRegistry(Guid tenantId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrbitDbContext>();
        await dbContext.WorkItemTypeDefinitions.AddRangeAsync(
            WorkItemTypeDefinition.CreateSoftwareDefaults(tenantId, DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task PresignUploadConfirmListDownloadDelete_RoundTripsThroughRealMinIo()
    {
        var tenantId = Guid.NewGuid();
        await SeedWorkItemTypeRegistry(tenantId);
        var workItemId = await CreateWorkItem(tenantId);
        var fileBytes = Encoding.UTF8.GetBytes("attachment integration test payload");

        var presign = await Send<PresignedAttachmentUploadDto>(
            HttpMethod.Post, tenantId, $"/api/v1/work-items/{workItemId}/attachments/presign",
            new { fileName = "notes.txt", contentType = "text/plain", sizeBytes = fileBytes.Length });

        using var putContent = new ByteArrayContent(fileBytes);
        putContent.Headers.Add("Content-Type", "text/plain");
        var putResponse = await RawHttpClient.PutAsync(presign!.UploadUrl, putContent);
        Assert.True(putResponse.IsSuccessStatusCode, $"PUT to presigned URL failed: {putResponse.StatusCode}. URL was: {presign.UploadUrl}");

        var confirmed = await Send<WorkItemAttachmentDto>(
            HttpMethod.Post, tenantId, $"/api/v1/work-items/{workItemId}/attachments",
            new { fileName = "notes.txt", contentType = "text/plain", sizeBytes = fileBytes.Length, objectKey = presign.ObjectKey });
        Assert.Equal("notes.txt", confirmed!.FileName);
        Assert.Equal(AttachmentScanStatus.Pending, confirmed.ScanStatus);
        Assert.Null(confirmed.DownloadUrl);

        // Confirm only queues the scan (§ malware/quarantine scanning) - it does not run inline, so
        // drive the same worker pipeline Orbit.Worker polls, using a fake scanner in place of clamd.
        await RunAttachmentScanProcessorAsync(new FakeCleanScanner());

        var list = await Send<WorkItemAttachmentDto[]>(
            HttpMethod.Get, tenantId, $"/api/v1/work-items/{workItemId}/attachments");
        var listed = Assert.Single(list!);
        Assert.Equal(confirmed.Id, listed.Id);
        Assert.Equal(AttachmentScanStatus.Clean, listed.ScanStatus);
        Assert.NotNull(listed.DownloadUrl);

        var downloaded = await RawHttpClient.GetAsync(listed.DownloadUrl!);
        Assert.True(downloaded.IsSuccessStatusCode, $"GET from presigned download URL failed: {downloaded.StatusCode}");
        Assert.Equal("attachment integration test payload", await downloaded.Content.ReadAsStringAsync());

        using var deleteRequest = new HttpRequestMessage(
            HttpMethod.Delete, $"/api/v1/work-items/{workItemId}/attachments/{confirmed.Id}");
        deleteRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var deleteResponse = await _client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var listAfterDelete = await Send<WorkItemAttachmentDto[]>(
            HttpMethod.Get, tenantId, $"/api/v1/work-items/{workItemId}/attachments");
        Assert.Empty(listAfterDelete!);
    }

    [Fact]
    public async Task Confirm_RejectsAnObjectKeyMintedForADifferentWorkItem()
    {
        var tenantId = Guid.NewGuid();
        await SeedWorkItemTypeRegistry(tenantId);
        var workItemAId = await CreateWorkItem(tenantId, "AAA");
        var workItemBId = await CreateWorkItem(tenantId, "BBB");

        var presignForA = await Send<PresignedAttachmentUploadDto>(
            HttpMethod.Post, tenantId, $"/api/v1/work-items/{workItemAId}/attachments/presign",
            new { fileName = "notes.txt", contentType = "text/plain", sizeBytes = 4 });

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/work-items/{workItemBId}/attachments")
        {
            Content = JsonContent.Create(new
            {
                fileName = "notes.txt",
                contentType = "text/plain",
                sizeBytes = 4,
                objectKey = presignForA!.ObjectKey,
            }),
        };
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Proves the scan-status state machine end to end: a Pending attachment's dedicated download
    /// endpoint returns 409 (still scanning) and it is withheld from the list; once
    /// <see cref="AttachmentScanProcessor"/> flags it Infected (via a fake scanner standing in for
    /// clamd), the download endpoint 404s (existence-hiding) and it disappears from the list too.
    /// </summary>
    [Fact]
    public async Task Download_IsGatedByScanStatus_AndInfectedAttachmentsAreExcludedFromListings()
    {
        var tenantId = Guid.NewGuid();
        await SeedWorkItemTypeRegistry(tenantId);
        var workItemId = await CreateWorkItem(tenantId);
        var fileBytes = Encoding.UTF8.GetBytes("eicar-style test payload");

        var presign = await Send<PresignedAttachmentUploadDto>(
            HttpMethod.Post, tenantId, $"/api/v1/work-items/{workItemId}/attachments/presign",
            new { fileName = "malware.zip", contentType = "application/zip", sizeBytes = fileBytes.Length });
        using var putContent = new ByteArrayContent(fileBytes);
        putContent.Headers.Add("Content-Type", "application/zip");
        await RawHttpClient.PutAsync(presign!.UploadUrl, putContent);

        var confirmed = await Send<WorkItemAttachmentDto>(
            HttpMethod.Post, tenantId, $"/api/v1/work-items/{workItemId}/attachments",
            new { fileName = "malware.zip", contentType = "application/zip", sizeBytes = fileBytes.Length, objectKey = presign.ObjectKey });

        // Still Pending: the dedicated download endpoint refuses with 409, not a URL.
        var pendingResponse = await GetRaw(tenantId, $"/api/v1/work-items/{workItemId}/attachments/{confirmed!.Id}/download");
        Assert.Equal(HttpStatusCode.Conflict, pendingResponse.StatusCode);

        var listWhilePending = await Send<WorkItemAttachmentDto[]>(
            HttpMethod.Get, tenantId, $"/api/v1/work-items/{workItemId}/attachments");
        Assert.Contains(listWhilePending!, a => a.Id == confirmed.Id && a.DownloadUrl == null);

        await RunAttachmentScanProcessorAsync(new FakeInfectedScanner());

        var infectedResponse = await GetRaw(tenantId, $"/api/v1/work-items/{workItemId}/attachments/{confirmed.Id}/download");
        Assert.Equal(HttpStatusCode.NotFound, infectedResponse.StatusCode);

        var listAfterInfected = await Send<WorkItemAttachmentDto[]>(
            HttpMethod.Get, tenantId, $"/api/v1/work-items/{workItemId}/attachments");
        Assert.DoesNotContain(listAfterInfected!, a => a.Id == confirmed.Id);
    }

    private async Task<HttpResponseMessage> GetRaw(Guid tenantId, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        return await _client.SendAsync(request);
    }

    private async Task RunAttachmentScanProcessorAsync(IAttachmentScanner scanner)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrbitDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorageService>();
        var processor = new AttachmentScanProcessor(
            dbContext, scanner, storage, TimeProvider.System, NullLogger<AttachmentScanProcessor>.Instance);
        await processor.ProcessPendingAsync(CancellationToken.None);
    }

    /// <summary>Fake IAttachmentScanner standing in for clamd - always reports Clean.</summary>
    private sealed class FakeCleanScanner : IAttachmentScanner
    {
        public Task<AttachmentScanResult> ScanAsync(Stream content, string fileName, CancellationToken cancellationToken) =>
            Task.FromResult(AttachmentScanResult.Clean());
    }

    /// <summary>Fake IAttachmentScanner standing in for clamd - always reports Infected.</summary>
    private sealed class FakeInfectedScanner : IAttachmentScanner
    {
        public Task<AttachmentScanResult> ScanAsync(Stream content, string fileName, CancellationToken cancellationToken) =>
            Task.FromResult(AttachmentScanResult.Infected("Test-Signature"));
    }

    private async Task<Guid> CreateWorkItem(Guid tenantId, string projectKey = "ATT")
    {
        using var projectRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/projects")
        {
            Content = JsonContent.Create(new { key = projectKey, name = "Attachments project" }),
        };
        projectRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var projectResponse = await _client.SendAsync(projectRequest);
        projectResponse.EnsureSuccessStatusCode();
        var project = await projectResponse.Content.ReadFromJsonAsync<ProjectDto>();

        using var workItemRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/work-items")
        {
            Content = JsonContent.Create(new
            {
                projectId = project!.Id,
                summary = "Card with an attachment",
                description = (string?)null,
                type = "Task",
                priority = "Medium",
            }),
        };
        workItemRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var workItemResponse = await _client.SendAsync(workItemRequest);
        Assert.True(workItemResponse.IsSuccessStatusCode, await workItemResponse.Content.ReadAsStringAsync());
        // Deserialize only the id: WorkItemDto's enum properties use the API's configured
        // string-enum JSON converter, which this bare HttpClient doesn't have registered.
        var workItem = await workItemResponse.Content.ReadFromJsonAsync<WorkItemIdDto>();
        return workItem!.Id;
    }

    private sealed record WorkItemIdDto(Guid Id);

    // WorkItemAttachmentDto.ScanStatus uses the API's configured string-enum JSON converter, which
    // this bare HttpClient doesn't have registered by default - register it here too.
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web)
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        };

    private async Task<T?> Send<T>(HttpMethod method, Guid tenantId, string path, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    private sealed record ProjectDto(Guid Id, string Key, string Name);
}
