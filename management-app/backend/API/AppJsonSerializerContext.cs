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
[JsonSerializable(typeof(ConfigResponse))]
[JsonSerializable(typeof(TransportConfig))]
[JsonSerializable(typeof(ShadowsocksConfig))]
[JsonSerializable(typeof(EndpointConfig))]
[JsonSerializable(typeof(ClientUsageResponse))]
[JsonSerializable(typeof(HourlyUsageResponse))]
[JsonSerializable(typeof(List<HourlyUsageResponse>))]
[JsonSerializable(typeof(HourlyDataPoint))]
[JsonSerializable(typeof(List<HourlyDataPoint>))]
[JsonSerializable(typeof(DailyUsageResponse))]
[JsonSerializable(typeof(List<DailyUsageResponse>))]
[JsonSerializable(typeof(DailyDataPoint))]
[JsonSerializable(typeof(List<DailyDataPoint>))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
