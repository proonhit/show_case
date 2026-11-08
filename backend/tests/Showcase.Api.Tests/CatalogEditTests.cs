using System.Net;
using System.Net.Http.Json;
using Showcase.Application;

namespace Showcase.Api.Tests;

[Collection(ApiCollection.Name)]
public class CatalogEditTests(ApiFactory factory)
{
    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 9)];

    private static async Task<TagDto> CreateTagAsync(HttpClient owner, string name)
    {
        var res = await owner.PostAsJsonAsync("/api/admin/tags", new UpsertTagRequest(name, name + " (vi)", Unique("tag")));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<TagDto>())!;
    }

    [Fact]
    public async Task Starter_categories_are_seeded()
    {
        var list = await factory.CreateClient().GetFromJsonAsync<List<CategoryDto>>("/api/public/categories");
        Assert.Contains(list!, c => c.Slug == "characters" && c.NameVi == "Nhân vật");
    }

    [Fact]
    public async Task Tag_slug_must_be_unique_and_tags_are_public()
    {
        var owner = await factory.CreateOwnerClientAsync();
        var tag = await CreateTagAsync(owner, "Hard surface");

        var dup = await owner.PostAsJsonAsync("/api/admin/tags", new UpsertTagRequest("Other", "Khác", tag.Slug));
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);

        var renamed = await owner.PutAsJsonAsync($"/api/admin/tags/{tag.Id}", new UpsertTagRequest("Hard-surface", "Bề mặt cứng", tag.Slug));
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);

        var list = await factory.CreateClient().GetFromJsonAsync<List<TagDto>>("/api/public/tags");
        Assert.Contains(list!, t => t.Id == tag.Id && t.NameVi == "Bề mặt cứng");
    }

    [Fact]
    public async Task Model_edit_updates_fields_translations_and_tags()
    {
        var owner = await factory.CreateOwnerClientAsync();
        var lowPoly = await CreateTagAsync(owner, "Low poly");
        var pbr = await CreateTagAsync(owner, "PBR");
        var slug = Unique("robot");

        var created = await owner.PostAsJsonAsync("/api/admin/models", new UpsertModelRequest(
            slug, null, "Maya", 10, null,
            [new TranslationDto("en", "Robot", null), new TranslationDto("vi", "Người máy", null)],
            [lowPoly.Id]));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var summary = (await created.Content.ReadFromJsonAsync<ModelSummaryDto>())!;
        Assert.Equal(new[] { lowPoly.Slug }, summary.TagSlugs);

        var newSlug = Unique("mech");
        var edit = await owner.PutAsJsonAsync($"/api/admin/models/{summary.Id}", new UpsertModelRequest(
            newSlug, null, "Blender", 42, new DateOnly(2026, 10, 1),
            [new TranslationDto("en", "Mech", "Updated.")],
            [pbr.Id]));
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);

        var detail = (await owner.GetFromJsonAsync<ModelDetailDto>($"/api/admin/models/{summary.Id}"))!;
        Assert.Equal(newSlug, detail.Slug);
        Assert.Equal("Blender", detail.Software);
        Assert.Equal(42, detail.HoursSpent);
        var en = Assert.Single(detail.Translations);
        Assert.Equal(("en", "Mech", "Updated."), (en.Locale, en.Title, en.Description));
        Assert.Equal(new[] { pbr.Id }, detail.TagIds);
    }

    [Fact]
    public async Task Model_edit_rejects_a_taken_slug_and_unknown_tags()
    {
        var owner = await factory.CreateOwnerClientAsync();
        var a = Unique("lamp");
        var b = Unique("chair");
        foreach (var s in new[] { a, b })
            (await owner.PostAsJsonAsync("/api/admin/models", new UpsertModelRequest(s, null, null, null, null, [new TranslationDto("en", s, null)]))).EnsureSuccessStatusCode();
        var models = (await owner.GetFromJsonAsync<List<ModelSummaryDto>>("/api/admin/models"))!;
        var id = models.Single(m => m.Slug == b).Id;

        var taken = await owner.PutAsJsonAsync($"/api/admin/models/{id}", new UpsertModelRequest(a, null, null, null, null, [new TranslationDto("en", "x", null)]));
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);

        var badTag = await owner.PutAsJsonAsync($"/api/admin/models/{id}", new UpsertModelRequest(b, null, null, null, null, [new TranslationDto("en", "x", null)], [Guid.NewGuid()]));
        Assert.Equal(HttpStatusCode.BadRequest, badTag.StatusCode);
    }

    [Fact]
    public async Task Profile_round_trips_and_validates_links()
    {
        var owner = await factory.CreateOwnerClientAsync();
        var profile = new ProfileDto("Test Artist", "3D artist", "Hoạ sĩ 3D", null, null,
            new Dictionary<string, string> { ["artstation"] = "https://www.artstation.com/example", ["email"] = "me@example.com" });

        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync("/api/admin/profile", profile)).StatusCode);
        var read = (await factory.CreateClient().GetFromJsonAsync<ProfileDto>("/api/public/profile"))!;
        Assert.Equal("Hoạ sĩ 3D", read.HeadlineVi);
        Assert.Equal("me@example.com", read.Links["email"]);

        var bad = profile with { Links = new() { ["site"] = "javascript:alert(1)" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync("/api/admin/profile", bad)).StatusCode);
    }

    [Fact]
    public async Task Admin_tags_and_profile_need_login()
    {
        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/admin/tags")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/admin/profile")).StatusCode);
    }
}

[Collection(ApiCollection.Name)]
public class StudioTests(ApiFactory factory)
{
    [Fact]
    public async Task Reorder_puts_models_in_the_given_order()
    {
        var owner = await factory.CreateOwnerClientAsync();
        var ids = new List<Guid>();
        foreach (var n in new[] { "one", "two", "three" })
        {
            var slug = $"order-{n}-{Guid.NewGuid():N}"[..20];
            var res = await owner.PostAsJsonAsync("/api/admin/models", new UpsertModelRequest(slug, null, null, null, null, [new TranslationDto("en", n, null)]));
            ids.Add((await res.Content.ReadFromJsonAsync<ModelSummaryDto>())!.Id);
        }

        var wanted = new List<Guid> { ids[2], ids[0], ids[1] };
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PutAsJsonAsync("/api/admin/models/order", new ReorderModelsRequest(wanted))).StatusCode);

        var list = (await owner.GetFromJsonAsync<List<ModelSummaryDto>>("/api/admin/models"))!;
        Assert.Equal(wanted, list.Take(3).Select(m => m.Id));
    }

    [Fact]
    public async Task Stats_and_me_answer_for_the_owner()
    {
        var owner = await factory.CreateOwnerClientAsync();
        var stats = (await owner.GetFromJsonAsync<StudioStatsDto>("/api/admin/stats"))!;
        Assert.Equal(stats.Total, stats.Published + stats.Draft + stats.Archived);
        Assert.Equal("owner", (await owner.GetFromJsonAsync<MeDto>("/api/auth/me"))!.User);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/admin/stats")).StatusCode);
    }
}

[Collection(ApiCollection.Name)]
public class ProfileFileTests(ApiFactory factory)
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. new byte[64]];
    private static readonly byte[] Pdf = [.. "%PDF-1.7\n"u8, .. new byte[64]];

    private static MultipartFormDataContent Form(byte[] bytes, string name)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", name);
        return form;
    }

    [Fact]
    public async Task Avatar_and_cv_upload_replace_and_delete()
    {
        var owner = await factory.CreateOwnerClientAsync();
        var anon = factory.CreateClient();

        var avatar = await owner.PutAsync("/api/admin/profile/avatar", Form(Png, "me.png"));
        Assert.Equal(HttpStatusCode.OK, avatar.StatusCode);
        var profile = (await avatar.Content.ReadFromJsonAsync<ProfileDto>())!;
        Assert.StartsWith("/api/public/profile/avatar?v=", profile.AvatarUrl);

        var img = await anon.GetAsync(profile.AvatarUrl);
        Assert.Equal("image/png", img.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await img.Content.ReadAsByteArrayAsync());

        var cv = (await (await owner.PutAsync("/api/admin/profile/cv", Form(Pdf, "cv.pdf"))).Content.ReadFromJsonAsync<ProfileDto>())!;
        Assert.NotNull(cv.CvUrl);
        Assert.Equal("application/pdf", (await anon.GetAsync(cv.CvUrl)).Content.Headers.ContentType?.MediaType);

        // A new avatar gets a new URL.
        var again = (await (await owner.PutAsync("/api/admin/profile/avatar", Form(Png, "me2.png"))).Content.ReadFromJsonAsync<ProfileDto>())!;
        Assert.NotEqual(profile.AvatarUrl, again.AvatarUrl);

        Assert.Equal(HttpStatusCode.OK, (await owner.DeleteAsync("/api/admin/profile/avatar")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/api/public/profile/avatar")).StatusCode);
    }

    [Fact]
    public async Task Wrong_file_types_are_refused()
    {
        var owner = await factory.CreateOwnerClientAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsync("/api/admin/profile/avatar", Form(Pdf, "me.png"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsync("/api/admin/profile/cv", Form(Png, "cv.pdf"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PutAsync("/api/admin/profile/cv", Form(Pdf, "cv.pdf"))).StatusCode);
    }
}
