using OutlineManager.API.DTOs;
using OutlineManager.API.Models;
using System.Net;
using System.Net.Http.Json;

namespace OutlineManager.API.Tests.TestCases;

public class UserInteractsWithClients : TestCaseBase
{
    [Fact]
    public async Task When_GetAllClients_WithNoClients_ReturnsEmptyList()
    {
        var client = _fixture.GetAuthenticatedClient();
        var response = await client.GetAsync("/api/v1/clients/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var clients = await response.Content.ReadFromJsonAsync<List<ClientResponse>>();
        Assert.NotNull(clients);
        Assert.Empty(clients);
    }

    [Fact]
    public async Task When_GetAllClients_WithClients_ReturnsAllClients()
    {
        var client1 = new Client
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Client1",
            Secret = "secret1",
            IsActive = true
        };
        var client2 = new Client
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Client2",
            Secret = "secret2",
            IsActive = false
        };

        await _fixture.SetClient([client1, client2]);

        var client = _fixture.GetAuthenticatedClient();
        var response = await client.GetAsync("/api/v1/clients/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var clients = await response.Content.ReadFromJsonAsync<List<ClientResponse>>();
        Assert.NotNull(clients);
        Assert.Equal(2, clients.Count);
        Assert.Contains(clients, c => c.Name == "Client1" && c.IsActive);
        Assert.Contains(clients, c => c.Name == "Client2" && !c.IsActive);
        
        // Verify usage data is included
        Assert.All(clients, c => Assert.NotNull(c.UsageLast30Days));
    }

    [Fact]
    public async Task When_GetAllClients_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/clients/");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task When_GetClientById_WithExistingClient_ReturnsClient()
    {
        var clientId = Guid.NewGuid().ToString();
        var testClient = new Client
        {
            Id = clientId,
            Name = "TestClient",
            Secret = "secret",
            IsActive = true
        };

        await _fixture.SetClient([testClient]);        

        var client = _fixture.GetAuthenticatedClient();
        var response = await client.GetAsync($"/api/v1/clients/{clientId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var clientResponse = await response.Content.ReadFromJsonAsync<ClientResponse>();
        Assert.NotNull(clientResponse);
        Assert.Equal(clientId, clientResponse.Id);
        Assert.Equal("TestClient", clientResponse.Name);
        Assert.True(clientResponse.IsActive);
        
        // Verify usage data is included
        Assert.NotNull(clientResponse.UsageLast30Days);
    }

    [Fact]
    public async Task When_GetClientById_WithNonExistingClient_ReturnsNotFound()
    {
        var clientId = Guid.NewGuid().ToString();

        var client = _fixture.GetAuthenticatedClient();
        var response = await client.GetAsync($"/api/v1/clients/{clientId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task When_GetClientById_WithoutAuth_ReturnsUnauthorized()
    {
        var clientId = Guid.NewGuid().ToString();

        var client = _fixture.CreateClient();
        var response = await client.GetAsync($"/api/v1/clients/{clientId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task When_CreateClient_WithValidData_ReturnsCreated()
    {
        var request = new CreateClientRequest
        {
            Name = "NewClient"
        };

        var client = _fixture.GetAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/api/v1/clients/", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var clientResponse = await response.Content.ReadFromJsonAsync<ClientResponse>();
        Assert.NotNull(clientResponse);
        Assert.NotEmpty(clientResponse.Id);
        Assert.Equal("NewClient", clientResponse.Name);
        Assert.True(clientResponse.IsActive);
        Assert.NotNull(clientResponse.UsageLast30Days);

        Assert.Contains($"/api/v1/clients/{clientResponse.Id}", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task When_CreateClient_WithDuplicateName_ReturnsBadRequest()
    {
        await _fixture.SetClient([new Client
        {
            Id = Guid.NewGuid().ToString(),
            Name = "ExistingClient",
            Secret = "secret"
        }]);

        var request = new CreateClientRequest
        {
            Name = "ExistingClient"
        };

        var client = _fixture.GetAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/api/v1/clients/", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorMessage = await response.Content.ReadAsStringAsync();
        Assert.Contains("Client name already exists", errorMessage);
    }

    [Fact]
    public async Task When_CreateClient_WithoutAuth_ReturnsUnauthorized()
    {
        var request = new CreateClientRequest
        {
            Name = "NewClient"
        };

        var client = _fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/clients/", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task When_UpdateClient_WithValidData_ReturnsOk()
    {
        var clientId = Guid.NewGuid().ToString();
        await _fixture.SetClient([new Client
        {
            Id = clientId,
            Name = "OriginalName",
            Secret = "secret"
        }]);

        var request = new UpdateClientRequest
        {
            Name = "UpdatedName"
        };

        var client = _fixture.GetAuthenticatedClient();
        var response = await client.PutAsJsonAsync($"/api/v1/clients/{clientId}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var clientResponse = await response.Content.ReadFromJsonAsync<ClientResponse>();
        Assert.NotNull(clientResponse);
        Assert.Equal(clientId, clientResponse.Id);
        Assert.Equal("UpdatedName", clientResponse.Name);
        Assert.NotNull(clientResponse.UsageLast30Days);
    }

    [Fact]
    public async Task When_UpdateClient_WithNonExistingClient_ReturnsNotFound()
    {
        var clientId = Guid.NewGuid().ToString();

        var request = new UpdateClientRequest
        {
            Name = "UpdatedName"
        };

        var client = _fixture.GetAuthenticatedClient();
        var response = await client.PutAsJsonAsync($"/api/v1/clients/{clientId}", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task When_UpdateClient_WithDuplicateName_ReturnsBadRequest()
    {
        var clientId = Guid.NewGuid().ToString();
        await _fixture.SetClient([
            new Client
            {
                Id = clientId,
                Name = "OriginalName",
                Secret = "secret"
            },
            new Client
            {
                Id = Guid.NewGuid().ToString(),
                Name = "ExistingName",
                Secret = "secret2"
            }
        ]);

        var request = new UpdateClientRequest
        {
            Name = "ExistingName"
        };

        var client = _fixture.GetAuthenticatedClient();
        var response = await client.PutAsJsonAsync($"/api/v1/clients/{clientId}", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorMessage = await response.Content.ReadAsStringAsync();
        Assert.Contains("Client name already exists", errorMessage);
    }

    [Fact]
    public async Task When_UpdateClient_WithoutAuth_ReturnsUnauthorized()
    {
        var clientId = Guid.NewGuid().ToString();

        var request = new UpdateClientRequest
        {
            Name = "UpdatedName"
        };

        var client = _fixture.CreateClient();
        var response = await client.PutAsJsonAsync($"/api/v1/clients/{clientId}", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task When_DeleteClient_WithExistingClient_ReturnsNoContent()
    {
        var clientId = Guid.NewGuid().ToString();
        await _fixture.SetClient([new Client
        {
            Id = clientId,
            Name = "ClientToDelete",
            Secret = "secret"
        }]);

        var client = _fixture.GetAuthenticatedClient();
        var response = await client.DeleteAsync($"/api/v1/clients/{clientId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task When_DeleteClient_WithNonExistingClient_ReturnsNotFound()
    {
        var clientId = Guid.NewGuid().ToString();

        var client = _fixture.GetAuthenticatedClient();
        var response = await client.DeleteAsync($"/api/v1/clients/{clientId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task When_DeleteClient_WithoutAuth_ReturnsUnauthorized()
    {
        var clientId = Guid.NewGuid().ToString();

        var client = _fixture.CreateClient();
        var response = await client.DeleteAsync($"/api/v1/clients/{clientId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
