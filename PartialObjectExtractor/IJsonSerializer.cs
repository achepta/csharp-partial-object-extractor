using System.Reflection;
using System.Text.Json.Nodes;

namespace PartialObjectExtractor;

/// <summary>
/// Interface for serializing extracted values to JsonNode.
/// Implement this interface to provide custom serialization logic.
/// </summary>
public interface IJsonSerializer {
    /// <summary>
    /// Serializes a value to a JsonNode.
    /// </summary>
    /// <param name="value">The value to serialize. May be null.</param>
    /// <returns>A JsonNode representation of the value, or null if the value is null.</returns>
    JsonNode? Serialize(object? value);
    
    /// <summary>
    /// Gets the JSON property name for a given PropertyInfo.
    /// This should respect any serialization attributes and naming policies.
    /// </summary>
    /// <param name="property">The property to get the JSON name for.</param>
    /// <returns>The JSON property name.</returns>
    string GetJsonPropertyName(PropertyInfo property);
}