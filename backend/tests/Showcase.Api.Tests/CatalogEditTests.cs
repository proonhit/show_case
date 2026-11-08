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
