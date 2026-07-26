using System.Text.Json;
using System.Text.Json.Serialization;

namespace PosAdminTool.Agent.IntegrationTests.TestSupport;

/// <summary>Mirrors the Agent's ConfigureHttpJsonOptions (enums as strings) so response deserialization in tests matches what the server actually wrote.</summary>
public static class TestJsonOptions
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
