using Microsoft.AspNetCore.Http.HttpResults;
using OutlineManager.API.DTOs;
using OutlineManager.API.Interfaces;
using OutlineManager.API.Models;

namespace OutlineManager.API.Endpoints;

public static class ClientEndpoints
{
    extension(RouteGroupBuilder group)
    {
        public RouteGroupBuilder MapClientEndpoints()
        {
            group.MapGet("/", GetAllClientsAsync)
                .WithName("GetAllClients")
                .RequireAuthorization();

            group.MapGet("/{id}", GetClientByIdAsync)
                .WithName("GetClientById")
                .RequireAuthorization();

            group.MapPost("/", CreateClientAsync)
                .WithName("CreateClient")
                .RequireAuthorization();

            group.MapPut("/{id}", UpdateClientAsync)
                .WithName("UpdateClient")
                .RequireAuthorization();

            group.MapDelete("/{id}", DeleteClientAsync)
                .WithName("DeleteClient")
                .RequireAuthorization();

            return group;
        }
    }

    private static async Task<Ok<IEnumerable<ClientResponse>>> GetAllClientsAsync(
        IClientRepository clientRepository)
    {
        var clients = await clientRepository.GetAllAsync();
        var response = clients.Select(MapToClientResponse);
        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<ClientResponse>, NotFound>> GetClientByIdAsync(
        string id,
        IClientRepository clientRepository)
    {
        var client = await clientRepository.GetByIdAsync(id);
        
        if (client == null)
            return TypedResults.NotFound();

        return TypedResults.Ok(MapToClientResponse(client));
    }

    private static async Task<Results<Created<ClientResponse>, BadRequest<string>>> CreateClientAsync(
        CreateClientRequest request,
        IClientRepository clientRepository,
        IOutlineSyncService outlineSyncService,
        ILogger<Program> logger)
    {
        if (await clientRepository.ExistsAsync(request.Name))
        {
            return TypedResults.BadRequest("Client name already exists");
        }

        var createdClient = await clientRepository.CreateAsync(request.Name);
        
        // Sync to outline server
        var allClients = await clientRepository.GetAllAsync();
        await outlineSyncService.SyncClientsToOutlineAsync(allClients);
        await outlineSyncService.ReloadOutlineServerAsync();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Created client {ClientId} with name {ClientName}", createdClient.Id, createdClient.Name);
        }

        return TypedResults.Created($"/api/v1/clients/{createdClient.Id}", MapToClientResponse(createdClient));
    }

    private static async Task<Results<Ok<ClientResponse>, NotFound, BadRequest<string>>> UpdateClientAsync(
        string id,
        UpdateClientRequest request,
        IClientRepository clientRepository,
        ILogger<Program> logger)
    {
        var existingClient = await clientRepository.GetByIdAsync(id);
        
        if (existingClient == null)
            return TypedResults.NotFound();

        if (!string.IsNullOrEmpty(request.Name) && 
            request.Name != existingClient.Name)
        {
            if (await clientRepository.ExistsAsync(request.Name))
            {
                return TypedResults.BadRequest("Client name already exists");
            }
            existingClient.Name = request.Name;
        }

        var updatedClient = await clientRepository.UpdateAsync(id, existingClient);
        
        if (updatedClient == null)
            return TypedResults.NotFound();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Updated client {ClientId}", id);
        }

        return TypedResults.Ok(MapToClientResponse(updatedClient));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteClientAsync(
        string id,
        IClientRepository clientRepository,
        IOutlineSyncService outlineSyncService,
        ILogger<Program> logger)
    {
        var deleted = await clientRepository.DeleteAsync(id);
        
        if (!deleted)
            return TypedResults.NotFound();

        // Sync to outline server
        var allClients = await clientRepository.GetAllAsync();
        await outlineSyncService.SyncClientsToOutlineAsync(allClients);
        await outlineSyncService.ReloadOutlineServerAsync();

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Deleted client {ClientId}", id);
        }

        return TypedResults.NoContent();
    }

    private static ClientResponse MapToClientResponse(Client client) => new()
    {
        Id = client.Id,
        Name = client.Name,
        IsActive = client.IsActive,
    };
}
