using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Text.Json.Nodes;
using Newtonsoft.Json.Linq;
using PartialObjectExtractor;

namespace TestPartialQuery;

public class NewtonsoftSerializerTest {
    [Test]
    public void NewtonsoftSerializer_BasicExtraction() {
        var serializer = new NewtonsoftJsonSerializer();
        var extractor = new PartialExtractor(serializer);

        var data = new {
            User = new {
                Name = "John",
                Email = "john@example.com",
                Age = 30
            }
        };

        var result = extractor.ExtractPaths(data, ["$.User.Name", "$.User.Email"]);
        var expected = ParseJsonObject("""
                                       {
                                         "User": {
                                           "Name": "John",
                                           "Email": "john@example.com"
                                         }
                                       }
                                       """);

        Assert.That(JsonEquals(result, expected), Is.True);
    }

    [Test]
    public void NewtonsoftSerializer_CamelCaseNaming() {
        var settings = new JsonSerializerSettings {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };
        var serializer = new NewtonsoftJsonSerializer(settings);
        var extractor = new PartialExtractor(serializer);

        var data = new {
            Parent = new { Child1 = "value1", Child2 = "value2" }
        };

        var result = extractor.ExtractPaths(data, ["$.Parent.Child1"]);
        var expected = ParseJsonObject("""
                                       {
                                         "parent": {
                                           "child1": "value1"
                                         }
                                       }
                                       """);

        Assert.That(JsonEquals(result, expected), Is.True);
    }

    [Test]
    public void NewtonsoftSerializer_JsonPropertyAttribute() {
        var serializer = new NewtonsoftJsonSerializer();
        var extractor = new PartialExtractor(serializer);

        var obj = new {
            CustomData = new NewtonsoftCustomNamed { 
                InternalName = "test", 
                DisplayName = "display" 
            }
        };

        var result = extractor.ExtractPaths(obj, ["$.CustomData.display_name"]);
        var expected = ParseJsonObject("""
                                       {
                                         "CustomData": {
                                           "display_name": "display"
                                         }
                                       }
                                       """);
        Console.WriteLine(result);
        Assert.That(JsonEquals(result, expected), Is.True);
    }

    [Test]
    public void NewtonsoftSerializer_ComplexObjects() {
        var serializer = new NewtonsoftJsonSerializer();
        var extractor = new PartialExtractor(serializer);

        var data = new {
            Products = new[] {
                new { Name = "Product1", Price = 10.5m },
                new { Name = "Product2", Price = 20.0m },
                new { Name = "Product3", Price = 30.25m }
            }
        };

        var result = extractor.ExtractPaths(data, ["$.Products[0].Name", "$.Products[1].Price"]);
        var expected = ParseJsonObject("""
                                       {
                                         "Products": [
                                           { "Name": "Product1" },
                                           { "Price": 20.0 }
                                         ]
                                       }
                                       """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }

    private class NewtonsoftCustomNamed {
        public string InternalName { get; set; }

        [JsonProperty("display_name")] 
        public string DisplayName { get; set; }
    }

    private static JsonObject ParseJsonObject(string json) {
        return JsonNode.Parse(json)!.AsObject();
    }

    private static bool JsonEquals(JsonObject? a, JsonObject? b) {
        return JsonNode.DeepEquals(a, b);
    }
    
    private class NewtonsoftJsonSerializer(JsonSerializerSettings? settings = null) : IJsonSerializer {
        private readonly JsonSerializer serializer = JsonSerializer.Create(settings ?? new JsonSerializerSettings());

        public JsonNode? Serialize(object? value) {
            if (value is null) {
                return null;
            }

            var jToken = JToken.FromObject(value, serializer);
            return JsonNode.Parse(jToken.ToString(Formatting.None));
        }

        public string GetJsonPropertyName(PropertyInfo property) {
            var jsonProp = property.GetCustomAttribute<JsonPropertyAttribute>();
            if (jsonProp?.PropertyName != null) {
                return jsonProp.PropertyName;
            }

            if (serializer.ContractResolver is CamelCasePropertyNamesContractResolver) {
                return ToCamelCase(property.Name);
            }

            return property.Name;
        }

        private static string ToCamelCase(string str) =>
            string.IsNullOrEmpty(str) || char.IsLower(str[0]) ? str : char.ToLower(str[0]) + str[1..];
    }
}