using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Showcase.Application;

namespace Showcase.Api.Tests;

/// <summary>Drives the tus protocol by hand: POST creates the upload, PATCH sends the bytes.</summary>
[Collection(ApiCollection.Name)]
public class UploadTests(ApiFactory factory)
{
    private static readonly byte[] GlbBytes = [.. "glTF"u8, 2, 0, 0, 0, .. new byte[200]];

    private static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));

    private static async Task<HttpResponseMessage> CreateUploadAsync(HttpClient client, Guid modelId, string fileName, long length)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/admin/uploads") { Content = new ByteArrayContent([]) };
        req.Headers.Add("Tus-Resumable", "1.0.0");
        req.Headers.Add("Upload-Length", length.ToString());
        req.Headers.Add("Upload-Metadata", $"modelId {B64(modelId.ToString())},filename {B64(fileName)}");
        return await client.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> SendBytesAsync(HttpClient client, Uri location, byte[] bytes)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/offset+octet-stream");
        var req = new HttpRequestMessage(HttpMethod.Patch, location) { Content = content };
        req.Headers.Add("Tus-Resumable", "1.0.0");
        req.Headers.Add("Upload-Offset", "0");
        return await client.SendAsync(req);
    }

    private static async Task<Guid> CreateModelAsync(HttpClient owner)
    {
        var slug = $"upload-{Guid.NewGuid():N}"[..15];
        var res = await owner.PostAsJsonAsync("/api/admin/models", new UpsertModelRequest(
            slug, null, null, null, null, [new TranslationDto("en", "Upload test", null)]));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ModelSummaryDto>())!.Id;
    }

    private static async Task<List<ModelFileDto>> FilesAsync(HttpClient owner, Guid modelId) =>
        (await owner.GetFromJsonAsync<List<ModelFileDto>>($"/api/admin/models/{modelId}/files"))!;

    private static async Task UploadAsync(HttpClient owner, Guid modelId, string fileName, byte[] bytes)
    {
        var created = await CreateUploadAsync(owner, modelId, fileName, bytes.Length);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var location = new Uri(owner.BaseAddress!, created.Headers.Location!);
        var patched = await SendBytesAsync(owner, location, bytes);
        Assert.Equal(HttpStatusCode.NoContent, patched.StatusCode);
    }

    [Fact]
    public async Task Valid_glb_is_stored_and_a_new_upload_becomes_the_current_version()
    {
        var owner = await factory.CreateOwnerClientAsync();
        var modelId = await CreateModelAsync(owner);

        await UploadAsync(owner, modelId, "helmet.glb", GlbBytes);
        var first = Assert.Single(await FilesAsync(owner, modelId));
        Assert.Equal("glb", first.Format);
        Assert.Equal("Original", first.Kind);
        Assert.Equal(GlbBytes.Length, first.SizeBytes);
        Assert.True(first.IsCurrent);

        await UploadAsync(owner, modelId, "helmet-v2.GLB", GlbBytes);
        var files = await FilesAsync(owner, modelId);
        Assert.Equal(2, files.Count);
        var current = Assert.Single(files, f => f.IsCurrent);
        Assert.Equal(2, current.Version);
    }

    [Fact]
    public async Task Stored_file_can_be_downloaded_by_the_owner_only()
    {
        var owner = await factory.CreateOwnerClientAsync();
        var modelId = await CreateModelAsync(owner);
        await UploadAsync(owner, modelId, "helmet.glb", GlbBytes);
        var file = Assert.Single(await FilesAsync(owner, modelId));

        var res = await owner.GetAsync($"/api/admin/files/{file.Id}/content");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("model/gltf-binary", res.Content.Headers.ContentType?.MediaType);
        Assert.Equal(GlbBytes, await res.Content.ReadAsByteArrayAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync($"/api/admin/files/{file.Id}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/admin/files/{Guid.NewGuid()}/content")).StatusCode);
    }

    [Fact]
    public async Task File_whose_content_is_not_glb_is_not_recorded()
    {
        var owner = await factory.CreateOwnerClientAsync();
        var modelId = await CreateModelAsync(owner);

        await UploadAsync(owner, modelId, "fake.glb", Encoding.UTF8.GetBytes("<html>not a model</html>"));

        Assert.Empty(await FilesAsync(owner, modelId));
    }

    [Fact]
    public async Task Unsupported_extension_is_rejected()
    {
        var owner = await factory.CreateOwnerClientAsync();
        var modelId = await CreateModelAsync(owner);

        var res = await CreateUploadAsync(owner, modelId, "model.obj", 10);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Upload_for_unknown_model_is_rejected()
    {
        var owner = await factory.CreateOwnerClientAsync();
        var res = await CreateUploadAsync(owner, Guid.NewGuid(), "helmet.glb", 10);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Anonymous_upload_is_unauthorized()
    {
        var res = await CreateUploadAsync(factory.CreateClient(), Guid.NewGuid(), "helmet.glb", 10);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await factory.CreateClient().GetAsync($"/api/admin/models/{Guid.NewGuid()}/files")).StatusCode);
    }

    [Fact]
    public void Magic_bytes_are_checked_per_format()
    {
        Assert.True(UploadRules.MagicMatches("glb", GlbBytes));
        Assert.True(UploadRules.MagicMatches("fbx", "Kaydara FBX Binary  \0"u8));
        Assert.False(UploadRules.MagicMatches("fbx", GlbBytes));
        Assert.Equal("fbx", UploadRules.FormatOf("Robot.FBX"));
        Assert.Null(UploadRules.FormatOf("robot.obj"));
    }
}

[Collection(ApiCollection.Name)]
public class ProcessingJobTests(ApiFactory factory)
{
    [Fact]
    public async Task Upload_creates_a_queued_job_with_the_upload_step_done()
    {
        var owner = await factory.CreateOwnerClientAsync();
        var slug = $"job-{Guid.NewGuid():N}"[..14];
        var created = await owner.PostAsJsonAsync("/api/admin/models", new UpsertModelRequest(slug, null, null, null, null, [new TranslationDto("en", "Job", null)]));
        var modelId = (await created.Content.ReadFromJsonAsync<ModelSummaryDto>())!.Id;
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/admin/models/{modelId}/jobs/latest")).StatusCode);

        byte[] glb = [.. "glTF"u8, 2, 0, 0, 0, .. new byte[40]];
        var post = new HttpRequestMessage(HttpMethod.Post, "/api/admin/uploads") { Content = new ByteArrayContent([]) };
        post.Headers.Add("Tus-Resumable", "1.0.0");
        post.Headers.Add("Upload-Length", glb.Length.ToString());
        post.Headers.Add("Upload-Metadata", $"modelId {Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(modelId.ToString()))},filename {Convert.ToBase64String("a.glb"u8.ToArray())}");
        var location = (await owner.SendAsync(post)).Headers.Location!;
        var content = new ByteArrayContent(glb);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/offset+octet-stream");
        var patch = new HttpRequestMessage(HttpMethod.Patch, location) { Content = content };
        patch.Headers.Add("Tus-Resumable", "1.0.0");
        patch.Headers.Add("Upload-Offset", "0");
        Assert.Equal(HttpStatusCode.NoContent, (await owner.SendAsync(patch)).StatusCode);

        // No worker runs in the API tests: the job waits in the queue.
        var job = (await owner.GetFromJsonAsync<ProcessingJobDto>($"/api/admin/models/{modelId}/jobs/latest"))!;
        Assert.Equal("Queued", job.Status);
        Assert.Equal(PipelineSteps.All, job.Steps.Select(s => s.Name));
        Assert.Equal("done", job.Steps[0].Status);
        Assert.All(job.Steps.Skip(1), s => Assert.Equal("pending", s.Status));
        Assert.Equal(job.Id, (await owner.GetFromJsonAsync<ProcessingJobDto>($"/api/admin/jobs/{job.Id}"))!.Id);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync($"/api/admin/jobs/{job.Id}")).StatusCode);
    }
}
