using System.Net;
using System.Net.Http.Json;
using Showcase.Application;

namespace Showcase.Api.Tests;

[Collection(ApiCollection.Name)]
public class CatalogTests(ApiFactory factory)
{
    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 9)];

    [Fact]
    public async Task Category_with_invalid_slug_is_rejected()
    {
        var client = await factory.CreateOwnerClientAsync();
        var res = await client.PostAsJsonAsync("/api/admin/categories",
            new UpsertCategoryRequest("Props", "Đồ vật", "Not A Slug", 0));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Created_category_is_listed_publicly_and_slug_must_be_unique()
    {
        var client = await factory.CreateOwnerClientAsync();
        var slug = Unique("props");
        var created = await client.PostAsJsonAsync("/api/admin/categories", new UpsertCategoryRequest("Props", "Đồ vật", slug, 1));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var duplicate = await client.PostAsJsonAsync("/api/admin/categories", new UpsertCategoryRequest("Props 2", "Đồ vật 2", slug, 2));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var list = await factory.CreateClient().GetFromJsonAsync<List<CategoryDto>>("/api/public/categories");
        Assert.Contains(list!, c => c.Slug == slug && c.NameVi == "Đồ vật");
    }

    [Fact]
    public async Task Model_requires_an_english_title()
    {
        var client = await factory.CreateOwnerClientAsync();
        var res = await client.PostAsJsonAsync("/api/admin/models", new UpsertModelRequest(
            Unique("helmet"), null, null, null, null, [new TranslationDto("vi", "Mũ giáp", null)]));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Draft_model_is_hidden_until_published()
    {
        var owner = await factory.CreateOwnerClientAsync();
        var anon = factory.CreateClient();
        var slug = Unique("helmet");

        var res = await owner.PostAsJsonAsync("/api/admin/models", new UpsertModelRequest(
            slug, null, "Blender", 36, new DateOnly(2026, 3, 15),
            [new TranslationDto("en", "Sci-fi Helmet", "Game-ready helmet."), new TranslationDto("vi", "Mũ giáp sci-fi", null)]));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var model = (await res.Content.ReadFromJsonAsync<ModelSummaryDto>())!;
        Assert.Equal("Draft", model.Status);
        Assert.Equal(2, model.Translations.Count);

        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/public/models/{slug}")).StatusCode);

        var publish = await owner.PatchAsJsonAsync($"/api/admin/models/{model.Id}/status", new UpdateModelStatusRequest("Published", true));
        Assert.Equal(HttpStatusCode.NoContent, publish.StatusCode);

        var published = await anon.GetFromJsonAsync<PublicModelDto>($"/api/public/models/{slug}");
        Assert.True(published!.IsFeatured);
        Assert.Contains(published.Translations, t => t.Locale == "vi" && t.Title == "Mũ giáp sci-fi");
    }

    [Fact]
    public async Task Deleted_model_disappears_but_its_slug_stays_reserved()
    {
        var owner = await factory.CreateOwnerClientAsync();
        var slug = Unique("radio");
        var request = new UpsertModelRequest(slug, null, null, null, null, [new TranslationDto("en", "Retro Radio", null)]);

        var model = (await (await owner.PostAsJsonAsync("/api/admin/models", request)).Content.ReadFromJsonAsync<ModelSummaryDto>())!;
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/admin/models/{model.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/admin/models/{model.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync("/api/admin/models", request)).StatusCode);
    }
}
