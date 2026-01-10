using System.Text.Json.Serialization;
using OutlineManager.API.DTOs;
using OutlineManager.API.Models;

namespace OutlineManager.API;

[JsonSerializable(typeof(User))]
[JsonSerializable(typeof(List<User>))]
[JsonSerializable(typeof(IEnumerable<User>))]
[JsonSerializable(typeof(Client))]
[JsonSerializable(typeof(List<Client>))]
[JsonSerializable(typeof(IEnumerable<Client>))]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(CreateClientRequest))]
[JsonSerializable(typeof(UpdateClientRequest))]
[JsonSerializable(typeof(ClientResponse))]
[JsonSerializable(typeof(IEnumerable<ClientResponse>))]
[JsonSerializable(typeof(IEnumerable<LoginRequest>))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
