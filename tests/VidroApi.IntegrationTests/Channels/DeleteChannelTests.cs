using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using VidroApi.IntegrationTests.Common;

namespace VidroApi.IntegrationTests.Channels;

public class DeleteChannelTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerOptions.Default)
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task DeleteChannel_AsOwner_Returns204()
    {
        var accessToken = await SignUpAndGetAccessToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var channelId = await CreateChannel("My Channel");

        var response = await _client.DeleteAsync($"/v1/channels/{channelId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteChannel_ChannelNoLongerExistsAfterDeletion()
    {
        var accessToken = await SignUpAndGetAccessToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var channelId = await CreateChannel("My Channel");

        await _client.DeleteAsync($"/v1/channels/{channelId}");

        _client.DefaultRequestHeaders.Authorization = null;
        var getResponse = await _client.GetAsync($"/v1/channels/{channelId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteChannel_WithoutAuth_Returns401()
    {
        var accessToken = await SignUpAndGetAccessToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var channelId = await CreateChannel("My Channel");

        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.DeleteAsync($"/v1/channels/{channelId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteChannel_WhenNotOwner_Returns403WithExpectedCode()
    {
        var ownerToken = await SignUpAndGetAccessToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var channelId = await CreateChannel("Owner Channel");

        var otherToken = await SignUpAndGetAccessToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
        var response = await _client.DeleteAsync($"/v1/channels/{channelId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.GetProperty("code").GetString().Should().Be("channel.not_owner");
    }

    [Fact]
    public async Task DeleteChannel_WithNonExistentId_Returns404()
    {
        var accessToken = await SignUpAndGetAccessToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _client.DeleteAsync($"/v1/channels/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<string> SignUpAndGetAccessToken()
    {
        var email = $"user_{Guid.NewGuid():N}@example.com";
        var password = "StrongPass1!";

        await _client.PostAsJsonAsync("/v1/auth/signup", new
        {
            username = $"usr{Guid.NewGuid():N}"[..15],
            email,
            password
        });

        var signInResponse = await _client.PostAsJsonAsync("/v1/auth/signin", new { email, password });
        var body = await signInResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("data").GetProperty("accessToken").GetString()!;
    }

    private async Task<string> CreateChannel(string name)
    {
        var response = await _client.PostAsJsonAsync("/v1/channels", new { name });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("data").GetProperty("channelId").GetString()!;
    }
}
