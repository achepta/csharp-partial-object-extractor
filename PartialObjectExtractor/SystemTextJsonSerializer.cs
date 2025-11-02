using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace PartialObjectExtractor;

public class SystemTextJsonSerializer(JsonSerializerOptions? options = null) : IJsonSerializer {
    private readonly JsonSerializerOptions options = options ?? new JsonSerializerOptions();

    public JsonNode? Serialize(object? value) {
        return value is null ? null : JsonSerializer.SerializeToNode(value, options);
    }

    public string GetJsonPropertyName(PropertyInfo property) {
        var jsonProp = property.GetCustomAttribute<JsonPropertyNameAttribute>();
        if (jsonProp is not null) {
            return jsonProp.Name;
        }

        return options.PropertyNamingPolicy == JsonNamingPolicy.CamelCase 
            ? JsonNamingPolicy.CamelCase.ConvertName(property.Name) 
            : property.Name;
    }
}