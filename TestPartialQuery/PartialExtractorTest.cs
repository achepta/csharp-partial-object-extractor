
// dotnet test -c Release  --verbosity normal  --collect:"XPlat Code Coverage" --results-directory ./coverage
// dotnet tool install -g dotnet-reportgenerator-globaltool
// reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coverage/coveragereport"

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PartialObjectExtractor;

namespace TestPartialQuery;

public class PartialExtractorTest {
    private readonly PartialExtractor extractor = new();

    #region test data

    private class TestData {
        public List<string> Items { get; set; }
        public List<Product> Products { get; set; }
        public Parent Parent { get; set; }
        public Parent AnotherParent { get; set; }
        public int[][][] NestedArrays { get; set; }
        public DeepNesting Deep { get; set; }
    }

    private class Product {
        public string Name { get; set; }
        public decimal Price { get; set; }
        public List<string> Tags { get; set; }
        public Category Category { get; set; }
    }

    private class Category {
        public string Name { get; set; }
        public int Level { get; set; }
    }

    private class Parent {
        public string Child1 { get; set; }
        public string Child2 { get; set; }
    }

    private class DeepNesting {
        public Level1 Level1 { get; set; }
    }

    private class Level1 {
        public List<Level2> Items { get; set; }
        public string Value { get; set; }
    }

    private class Level2 {
        public string Value { get; set; }
        public List<Level3> Nested { get; set; }
    }

    private class Level3 {
        public string Value { get; set; }
    }

    private TestData testData;

    [SetUp]
    public void Setup() {
        testData = new TestData {
            Items = ["first", "second", "third", "fourth", "fifth", "sixth"],
            Products = [
                new() {
                    Name = "Product1",
                    Price = 10,
                    Tags = ["new", "sale", "popular"],
                    Category = new() { Name = "Electronics", Level = 1 }
                },
                new() {
                    Name = "Product2",
                    Price = 20,
                    Tags = ["featured", "premium"],
                    Category = new() { Name = "Electronics", Level = 2 }
                },
                new() {
                    Name = "Product3",
                    Price = 30,
                    Tags = ["basic"],
                    Category = new() { Name = "Home", Level = 1 }
                },
                new() {
                    Name = "Product4",
                    Price = 40,
                    Tags = ["luxury", "exclusive"],
                    Category = new() { Name = "Home", Level = 3 }
                }
            ],
            Parent = new() { Child1 = "ParentChild1", Child2 = "ParentChild2" },
            AnotherParent = new() { Child1 = "AnotherChild1", Child2 = "AnotherChild2" },
            NestedArrays = [[[1, 2], [3, 4]], [[5, 6]]],
            Deep = new() {
                Level1 = new() {
                    Value = "L1Value",
                    Items = [
                        new() {
                            Value = "L2Value1",
                            Nested = [
                                new() { Value = "L3Value1" },
                                new() { Value = "L3Value2" }
                            ]
                        },
                        new() {
                            Value = "L2Value2",
                            Nested = [
                                new() { Value = "L3Value3" }
                            ]
                        }
                    ]
                }
            }
        };
    }

    private static JsonObject ParseJsonObject(string json) {
        return JsonNode.Parse(json)!.AsObject();
    }

    private static bool JsonEquals(JsonObject? a, JsonObject? b) {
        return JsonNode.DeepEquals(a, b);
    }

    #endregion

    #region test recursive

    [Test]
    public void RecursiveDescent_SingleProperty() {
        var result = extractor.ExtractPaths(testData, ["$..Child1"]);
        var expected = ParseJsonObject("""
                                     {
                                       "Parent": { "Child1": "ParentChild1" },
                                       "AnotherParent": { "Child1": "AnotherChild1" }
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }

    [Test]
    public void RecursiveDescent_DeepNesting() {
        var result = extractor.ExtractPaths(testData, ["$..Value"]);
        var expected = ParseJsonObject("""
                                     {
                                       "Deep": {
                                         "Level1": {
                                           "Value": "L1Value",
                                           "Items": [
                                             {
                                               "Value": "L2Value1",
                                               "Nested": [
                                                 { "Value": "L3Value1" },
                                                 { "Value": "L3Value2" }
                                               ]
                                             },
                                             {
                                               "Value": "L2Value2",
                                               "Nested": [
                                                 { "Value": "L3Value3" }
                                               ]
                                             }
                                           ]
                                         }
                                       }
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }

    [Test]
    public void RecursiveDescent_DoubleRecursive() {
        // This should find Level1 at any depth, then Value at any depth under those Level1s
        var result = extractor.ExtractPaths(testData, ["$..Level1..Value"]);
        var expected = ParseJsonObject("""
                                     {
                                       "Deep": {
                                         "Level1": {
                                           "Value": "L1Value",
                                           "Items": [
                                             {
                                               "Value": "L2Value1",
                                               "Nested": [
                                                 { "Value": "L3Value1" },
                                                 { "Value": "L3Value2" }
                                               ]
                                             },
                                             {
                                               "Value": "L2Value2",
                                               "Nested": [
                                                 { "Value": "L3Value3" }
                                               ]
                                             }
                                           ]
                                         }
                                       }
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }

    #endregion

    #region test wildcard

    [Test]
    public void Wildcard_OnObject() {
        var result = extractor.ExtractPaths(testData, ["$.Parent[*]"]);
        var expected = ParseJsonObject("""
                                     {
                                       "Parent": {
                                         "Child1": "ParentChild1",
                                         "Child2": "ParentChild2"
                                       }
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }

    [Test]
    public void Wildcard_OnArray() {
        var result = extractor.ExtractPaths(testData, ["$..Tags[*]"]);
        var expected = ParseJsonObject("""
                                     {
                                       "Products": [
                                         { "Tags": ["new", "sale", "popular"] },
                                         { "Tags": ["featured", "premium"] },
                                         { "Tags": ["basic"] },
                                         { "Tags": ["luxury", "exclusive"] }
                                       ]
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }

    #endregion

    #region multiselect
    
    [Test]
    public void Multiselect_WithRecursiveDescent() {
        var result = extractor.ExtractPaths(testData, ["$..['Child1', 'Child2']"]);
        var expected = ParseJsonObject("""
                                     {
                                       "Parent": {
                                         "Child1": "ParentChild1",
                                         "Child2": "ParentChild2"
                                       },
                                       "AnotherParent": {
                                         "Child1": "AnotherChild1",
                                         "Child2": "AnotherChild2"
                                       }
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }

    [Test]
    public void Multiselect_AfterWildcard() {
        var result = extractor.ExtractPaths(testData, ["$.Products[*]['Name', 'Price']"]);
        var expected = ParseJsonObject("""
                                     {
                                       "Products": [
                                         { "Name": "Product1", "Price": 10 },
                                         { "Name": "Product2", "Price": 20 },
                                         { "Name": "Product3", "Price": 30 },
                                         { "Name": "Product4", "Price": 40 }
                                       ]
                                     }
                                     """);
        
        Assert.That(JsonEquals(result, expected), Is.True);
    }
    
    #endregion

    #region nested arrays

    [Test]
    public void NestedArrays_MultipleIndices() {
        var result = extractor.ExtractPaths(testData, ["$.NestedArrays[0][0][1]"]);
        var expected = ParseJsonObject("""
                                     {
                                       "NestedArrays": [
                                         [
                                           [null, 2]
                                         ]
                                       ]
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }

    [Test]
    public void NestedArrays_WildcardAtMultipleLevels() {
        var result = extractor.ExtractPaths(testData, ["$.NestedArrays[*][0][0]"]);
        var expected = ParseJsonObject("""
                                     {
                                       "NestedArrays": [
                                         [
                                           [1]
                                         ],
                                         [
                                           [5]
                                         ]
                                       ]
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }

    [Test]
    public void NestedArrays_SliceInNested() {
        var result = extractor.ExtractPaths(testData, ["$.NestedArrays[0][0][:2]"]);
        var expected = ParseJsonObject("""
                                     {
                                       "NestedArrays": [
                                         [
                                           [1, 2]
                                         ]
                                       ]
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }
    
    #endregion

    #region edge cases

    [Test]
    public void EdgeCase_EmptyPathList() {
        var result = extractor.ExtractPaths(testData, []);
        Assert.That(JsonEquals(result, new JsonObject()), Is.True);
    }

    [Test]
    public void EdgeCase_NullSource() {
        var result = extractor.ExtractPaths<object>(null, ["$.Property"]);
        Assert.That(JsonEquals(result, new JsonObject()), Is.True);
    }

    [Test]
    public void EdgeCase_NonExistentProperty() {
        var result = extractor.ExtractPaths(testData, ["$.NonExistent"]);
        Assert.That(JsonEquals(result, new JsonObject()), Is.True);
    }

    [Test]
    public void EdgeCase_NonExistentNestedProperty() {
        var result = extractor.ExtractPaths(testData, ["$.Parent.NonExistent"]);
        Assert.That(JsonEquals(result, new JsonObject()), Is.True);
    }

    [Test]
    public void EdgeCase_ArrayIndexOutOfBounds() {
        var result = extractor.ExtractPaths(testData, ["$.Items[999]"]);
        Assert.That(JsonEquals(result, new JsonObject()), Is.True);
    }

    [Test]
    public void EdgeCase_NegativeIndexLargerThanArray() {
        var result = extractor.ExtractPaths(testData, ["$.Items[-999]"]);
        Assert.That(JsonEquals(result, new JsonObject()), Is.True);
    }

    [Test]
    public void EdgeCase_RecursiveDescentNoMatch() {
        var result = extractor.ExtractPaths(testData, ["$..NonExistentProperty"]);
        Assert.That(JsonEquals(result, new JsonObject()), Is.True);
    }

    [Test]
    public void EdgeCase_MultiselectWithNonExistent() {
        var result = extractor.ExtractPaths(testData, ["$.Parent['Child1', 'NonExistent']"]);
        var expected = ParseJsonObject("""
                                     {
                                       "Parent": {
                                         "Child1": "ParentChild1"
                                       }
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }

    [Test]
    public void InvalidPath_ThrowsException() {
        Assert.Throws<ArgumentException>(() =>
            extractor.ExtractPaths(testData, ["$.Items["]));
    }

    [Test]
    public void InvalidPath_UnclosedBracket_ThrowsException() {
        Assert.Throws<ArgumentException>(() =>
            extractor.ExtractPaths(testData, ["$.Items[0"]));
    }

    [Test]
    public void InvalidPath_InvalidInteger_ThrowsException() {
        Assert.Throws<ArgumentException>(() =>
            extractor.ExtractPaths(testData, ["$.Items[abc]"]));
    }
    
    #endregion

    #region case sensitivity

    [Test]
    public void CaseSensitivity_LowercaseProperty() {
        var result = extractor.ExtractPaths(testData, ["$.parent.child1"]);
        var expected = ParseJsonObject("""
                                     {
                                       "Parent": {
                                         "Child1": "ParentChild1"
                                       }
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }

    [Test]
    public void CaseSensitivity_MixedCase() {
        var result = extractor.ExtractPaths(testData, ["$.pRoDuCtS[0].nAmE"]);
        var expected = ParseJsonObject("""
                                     {
                                       "Products": [
                                         { "Name": "Product1" }
                                       ]
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }
    
    #endregion

    #region serialization settings

    [Test]
    public void JsonSettings_CamelCaseOutput() {
        var options = new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        var camelExtractor = new PartialExtractor(options);

        var result = camelExtractor.ExtractPaths(testData, ["$.Parent.Child1"]);
        var expected = ParseJsonObject("""
                                     {
                                       "parent": {
                                         "child1": "ParentChild1"
                                       }
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }

    [Test]
    public void JsonSettings_CustomJsonPropertyAttribute() {
        var obj = new {
            CustomData = new CustomNamed { InternalName = "test", DisplayName = "display" }
        };

        var result = extractor.ExtractPaths(obj, ["$.CustomData.display_name"]);
        var expected = ParseJsonObject("""
                                     {
                                       "CustomData": {
                                         "display_name": "display"
                                       }
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }

    private class CustomNamed {
        public string InternalName { get; set; }

        [JsonPropertyName("display_name")] public string DisplayName { get; set; }
    }
    
    #endregion

    #region empty collections

    [Test]
    public void EmptyCollections_WildcardOnEmptyArray() {
        var obj = new { Items = new List<string>() };
        var result = extractor.ExtractPaths(obj, ["$.Items[*]"]);
        var expected = ParseJsonObject("""
                                     {
                                       "Items": []
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }

    [Test]
    public void EmptyCollections_SliceOnEmptyArray() {
        var obj = new { Items = new List<string>() };
        var result = extractor.ExtractPaths(obj, ["$.Items[0:5]"]);
        var expected = ParseJsonObject("""
                                     {
                                       "Items": []
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }

    [Test]
    public void EmptyCollections_RecursiveOnEmptyStructure() {
        var obj = new { Empty = new { } };
        var result = extractor.ExtractPaths(obj, ["$..Property"]);
        Assert.That(JsonEquals(result, new JsonObject()), Is.True);
    }
    
    #endregion

    #region whitespace handling

    [Test]
    public void Whitespace_InBracketNotation() {
        var result = extractor.ExtractPaths(testData, ["$[ 'Parent' ][ 'Child1' ]"]);
        var expected = ParseJsonObject("""
                                     {
                                       "Parent": {
                                         "Child1": "ParentChild1"
                                       }
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }

    [Test]
    public void Whitespace_InMultiselect() {
        var result = extractor.ExtractPaths(testData, ["$.Parent[ 'Child1' , 'Child2' ]"]);
        var expected = ParseJsonObject("""
                                     {
                                       "Parent": {
                                         "Child1": "ParentChild1",
                                         "Child2": "ParentChild2"
                                       }
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }

    [Test]
    public void Whitespace_InSlice() {
        var result = extractor.ExtractPaths(testData, ["$.Items[ 1 : 3 ]"]);
        var expected = ParseJsonObject("""
                                     {
                                       "Items": [
                                         null,
                                         "second",
                                         "third"
                                       ]
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }

    #endregion
    
    #region slice edge cases

    [Test]
    public void Slice_FullArray() {
        var result = extractor.ExtractPaths(testData, ["$.Items[:]"]);
        var expected = ParseJsonObject("""
                                     {
                                       "Items": [
                                         "first",
                                         "second",
                                         "third",
                                         "fourth",
                                         "fifth",
                                         "sixth"
                                       ]
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }

    [Test]
    public void Slice_NegativeRange() {
        var result = extractor.ExtractPaths(testData, ["$.Items[-3:-1]"]);
        var expected = ParseJsonObject("""
                                     {
                                       "Items": [
                                         null,
                                         null,
                                         null,
                                         "fourth",
                                         "fifth"
                                       ]
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }

    [Test]
    public void Slice_BeyondBounds() {
        var result = extractor.ExtractPaths(testData, ["$.Items[4:100]"]);
        var expected = ParseJsonObject("""
                                     {
                                       "Items": [
                                         null,
                                         null,
                                         null,
                                         null,
                                         "fifth",
                                         "sixth"
                                       ]
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }
    
    [Test]
    public void InvalidSlice_StartGreaterThanEnd() {
        var result = extractor.ExtractPaths(testData, ["$.Items[5:2]"]);
        var expected = ParseJsonObject("""
                                     {
                                       "Items": []
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }

    [Test]
    public void EmptySlice() {
        var result = extractor.ExtractPaths(testData, ["$.Items[2:2]"]);
        var expected = ParseJsonObject("""
                                     {
                                       "Items": []
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }
    
    #endregion

    #region null preservation

    [Test]
    public void NullValues_InArray() {
        var obj = new { Items = new[] { "first", "second", null} };
        var result = extractor.ExtractPaths(obj, ["$.Items[*]"]);
        var expected = ParseJsonObject("""
                                     {
                                       "Items": ["first", "second", null]
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }

    [Test]
    public void NullValues_InObject() {
        var obj = new { Data = new { Name = "test", Value = (string)null } };
        var result = extractor.ExtractPaths(obj, ["$.Data[*]"]);
        var expected = ParseJsonObject("""
                                     {
                                       "Data": {
                                         "Name": "test",
                                         "Value": null
                                       }
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }
    
    #endregion

    #region mixed notation

    [Test]
    public void MixedNotation_DotAndBracket() {
        var result = extractor.ExtractPaths(testData, ["$.Parent['Child1'].ToString()"]);
        // This should fail gracefully - ToString() is not a property
        Assert.That(JsonEquals(result, new JsonObject()), Is.True);
    }

    [Test]
    public void MixedNotation_BracketThenDot() {
        var result = extractor.ExtractPaths(testData, ["$['Parent'].Child1"]);
        var expected = ParseJsonObject("""
                                     {
                                       "Parent": {
                                         "Child1": "ParentChild1"
                                       }
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }

    [Test]
    public void MixedNotation_Complex() {
        var result = extractor.ExtractPaths(testData, ["$.Products[0]['Category'].Name"]);
        var expected = ParseJsonObject("""
                                     {
                                       "Products": [
                                         {
                                           "Category": {
                                             "Name": "Electronics"
                                           }
                                         }
                                       ]
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }
    
    #endregion

    #region multiple paths
    
    [Test]
    public void Combination_OverlappingPaths() {
        var result = extractor.ExtractPaths(testData, [
            "$.Parent.Child1",
            "$.Parent.Child2",
            "$.Parent"
        ]);
        var expected = ParseJsonObject("""
                                     {
                                       "Parent": {
                                         "Child1": "ParentChild1",
                                         "Child2": "ParentChild2"
                                       }
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }

    [Test]
    public void Combination_DifferentPathsSameLeaf() {
        var result = extractor.ExtractPaths(testData, [
            "$.Parent.Child1",
            "$..Child1"
        ]);
        var expected = ParseJsonObject("""
                                     {
                                       "Parent": {
                                         "Child1": "ParentChild1"
                                       },
                                       "AnotherParent": {
                                         "Child1": "AnotherChild1"
                                       }
                                     }
                                     """);
       
        Assert.That(JsonEquals(result, expected), Is.True);
    }

    [Test]
    public void Combination_ArrayIndicesFromMultiplePaths() {
        var result = extractor.ExtractPaths(testData, [
            "$.Items[0]",
            "$.Items[2]",
            "$.Items[4]"
        ]);
        var expected = ParseJsonObject("""
                                     {
                                       "Items": [
                                         "first",
                                         null,
                                         "third",
                                         null,
                                         "fifth"
                                       ]
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }

    [Test]
    public void Combination_RecursiveWithMultipleOperations() {
        var result = extractor.ExtractPaths(testData, [
            "$..Products[0,2].Name",
            "$..Category.Level"
        ]);
        var expected = ParseJsonObject("""
                                     {
                                       "Products": [
                                         { "Name": "Product1", "Category": { "Level": 1 } },
                                         { "Category": { "Level": 2 } },
                                         { "Name": "Product3", "Category": { "Level": 1 } },
                                         { "Category": { "Level": 3 } }
                                       ]
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }

    [Test]
    public void Combination_AllPathTypes() {
        var result = extractor.ExtractPaths(testData, [
            "$.Items[0]",
            "$.Items[1:3]",
            "$.Products[*].Name",
            "$.Products[0,1]['Name', 'Price']",
            "$..Child1",
            "$.Parent[*]"
        ]);

        var expected = ParseJsonObject("""
                                     {
                                       "Items": [
                                         "first",
                                         "second",
                                         "third"
                                       ],
                                       "Products": [
                                         { "Name": "Product1", "Price": 10 },
                                         { "Name": "Product2", "Price": 20 },
                                         { "Name": "Product3" },
                                         { "Name": "Product4" }
                                       ],
                                       "Parent": {
                                         "Child1": "ParentChild1",
                                         "Child2": "ParentChild2"
                                       },
                                       "AnotherParent": {
                                         "Child1": "AnotherChild1"
                                       }
                                     }
                                     """);
        Assert.That(JsonEquals(result, expected), Is.True);
    }
    
    #endregion
}