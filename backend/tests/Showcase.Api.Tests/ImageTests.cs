using System.Net;
using System.Net.Http.Json;
using Showcase.Application;

namespace Showcase.Api.Tests;

[Collection(ApiCollection.Name)]
public class ImageTests(ApiFactory factory)
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static async Task<Guid> CreateModelAsync(HttpClient owner)
    {
        var slug = $"img-{Guid.NewGuid():N}"[..14];
        var res = await owner.PostAsJsonAsync("/api/admin/models", new UpsertModelRequest(slug, null, null, null, null, [new TranslationDto("en", "Images", null)]));
        return (await res.Content.ReadFromJsonAsync<ModelSummaryDto>())!.Id;
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient c, Guid modelId, string kind, byte[] bytes)
    {
        var form = new MultipartFormDataContent { { new ByteArrayContent(bytes), "file", "shot.png" }, { new StringContent(kind), "kind" } };
        return c.PostAsync($"/api/admin/models/{modelId}/images", form);
    }

    private static async Task<ModelDetailDto> DetailAsync(HttpClient owner, Guid id) =>
        (await owner.GetFromJsonAsync<ModelDetailDto>($"/api/admin/models/{id}"))!;

    [Fact]
    public async Task Viewer_settings_default_then_save()
    {
        var owner = await factory.CreateOwnerClientAsync();
        var id = await CreateModelAsync(owner);
        var defaults = (await DetailAsync(owner, id)).ViewerSettings;
        Assert.True(defaults.AutoRotate);
        Assert.Equal("final", defaults.DefaultMode);
        Assert.Null(defaults.CameraPosition);

        var wanted = new ViewerSettingsDto(false, 30, "#202020", "wireframe", [1, 2, 3], [0, 0.5, 0]);
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync($"/api/admin/models/{id}/viewer", wanted)).StatusCode);
        var saved = (await DetailAsync(owner, id)).ViewerSettings;
        Assert.Equal(("wireframe", "#202020", false, 30d), (saved.DefaultMode, saved.Background, saved.AutoRotate, saved.AutoRotateSpeed));
        Assert.Equal(new[] { 1d, 2, 3 }, saved.CameraPosition);

        var bad = wanted with { DefaultMode = "sparkle", CameraTarget = null };
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync($"/api/admin/models/{id}/viewer", bad)).StatusCode);
    }

    [Fact]
    public async Task Images_upload_caption_reorder_delete_and_single_thumbnail()
    {
        var owner = await factory.CreateOwnerClientAsync();
        var id = await CreateModelAsync(owner);

        Assert.Equal(HttpStatusCode.Created, (await UploadAsync(owner, id, "Thumbnail", Png)).StatusCode);
        var render = (await (await UploadAsync(owner, id, "Render", Png)).Content.ReadFromJsonAsync<ModelImageDto>())!;
        var process = (await (await UploadAsync(owner, id, "Process", Png)).Content.ReadFromJsonAsync<ModelImageDto>())!;
        // A second thumbnail replaces the first.
        var thumb = (await (await UploadAsync(owner, id, "Thumbnail", Png)).Content.ReadFromJsonAsync<ModelImageDto>())!;

        var detail = await DetailAsync(owner, id);
        Assert.Equal(3, detail.Images.Count);
        Assert.Equal(thumb.Id, detail.Images[0].Id);
        var summary = (await owner.GetFromJsonAsync<List<ModelSummaryDto>>("/api/admin/models"))!.Single(m => m.Id == id);
        Assert.Equal(thumb.Url, summary.ThumbnailUrl);

        // Stored as WebP, with a small copy.
        var img = await owner.GetAsync(render.Url);
        Assert.Equal("image/webp", img.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(render.SmallUrl);
        Assert.Equal("image/webp", (await owner.GetAsync(render.SmallUrl)).Content.Headers.ContentType?.MediaType);

        var captioned = await owner.PatchAsJsonAsync($"/api/admin/images/{render.Id}", new UpdateImageRequest("Final render", "Ảnh render"));
        Assert.Equal("Ảnh render", (await captioned.Content.ReadFromJsonAsync<ModelImageDto>())!.CaptionVi);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.PutAsJsonAsync($"/api/admin/models/{id}/images/order", new ReorderRequest([process.Id, render.Id]))).StatusCode);
        var order = (await DetailAsync(owner, id)).Images.Where(i => i.Kind != "Thumbnail").Select(i => i.Id);
        Assert.Equal(new[] { process.Id, render.Id }, order);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/admin/images/{process.Id}")).StatusCode);
        Assert.Equal(2, (await DetailAsync(owner, id)).Images.Count);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(process.Url)).StatusCode);
    }

    [Fact]
    public async Task Bad_images_are_refused()
    {
        var owner = await factory.CreateOwnerClientAsync();
        var id = await CreateModelAsync(owner);
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(owner, id, "Render", "%PDF-1.4 nope"u8.ToArray())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(owner, id, "Banner", Png)).StatusCode);
        // Right first bytes, broken image.
        byte[] broken = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. new byte[64]];
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(owner, id, "Render", broken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await UploadAsync(factory.CreateClient(), id, "Render", Png)).StatusCode);
    }
}

[Collection(ApiCollection.Name)]
public class PublicShowcaseTests(ApiFactory factory)
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    [Fact]
    public async Task Published_model_exposes_files_and_images_drafts_do_not()
    {
        var owner = await factory.CreateOwnerClientAsync();
        var anon = factory.CreateClient();
        var slug = $"pub-{Guid.NewGuid():N}"[..14];
        var created = await owner.PostAsJsonAsync("/api/admin/models", new UpsertModelRequest(slug, null, "Blender", 12, null, [new TranslationDto("en", "Public", null)]));
        var id = (await created.Content.ReadFromJsonAsync<ModelSummaryDto>())!.Id;
        var form = new MultipartFormDataContent { { new ByteArrayContent(Png), "file", "t.png" }, { new StringContent("Render"), "kind" } };
        var image = (await (await owner.PostAsync($"/api/admin/models/{id}/images", form)).Content.ReadFromJsonAsync<ModelImageDto>())!;

        // Draft: nothing public, not even the image by id.
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/public/models/{slug}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/public/images/{image.Id}")).StatusCode);

        await owner.PatchAsJsonAsync($"/api/admin/models/{id}/status", new UpdateModelStatusRequest("Published", false));
        var pub = (await anon.GetFromJsonAsync<PublicModelDto>($"/api/public/models/{slug}"))!;
        Assert.Equal("Blender", pub.Software);
        Assert.Null(pub.ModelUrl); // no file uploaded yet
        var img = Assert.Single(pub.Images);
        Assert.Equal($"/api/public/images/{image.Id}", img.Url);
        Assert.Equal("image/webp", (await anon.GetAsync(img.Url)).Content.Headers.ContentType?.MediaType);
        Assert.EndsWith("?size=small", img.SmallUrl);
        Assert.Contains((await anon.GetFromJsonAsync<List<PublicModelDto>>("/api/public/models"))!, m => m.Slug == slug);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/public/models/{slug}/model.glb")).StatusCode);
    }
}
