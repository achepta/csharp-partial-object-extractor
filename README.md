# Partial Object Extractor

A high-performance C# library for extracting partial objects using JSONPath expressions while preserving the original structure. Think GraphQL but with JSONPath syntax.

## Overview

This library allows you to query complex object graphs and extract only the data you need, maintaining the hierarchical structure of the original object. Unlike traditional JSONPath implementations
that return flat arrays of leaf values, this extractor preserves the object structure, making it ideal for API projections and selective serialization.

**Built on Newtonsoft.Json**: This library leverages Newtonsoft.Json for serialization and respects all its conventions, attributes, and settings.

**High Performance**: Uses reflection to traverse objects and extract only requested properties *before* serialization, avoiding the overhead of serializing entire object graphs. Only the extracted
data is converted to JSON, making it significantly faster for large objects when you only need a subset of data.

Usage example in [CloudGbxQuery](https://github.com/achepta/cloud-gbx-query/blob/master/Program.cs#L159)

## Installation

```bash
dotnet add package PartialObjectExtractor
```

```csharp
using PartialObjectExtractor;

var extractor = new PartialExtractor();
```

## Basic Usage

```csharp
var data = new {
    User = new {
        Name = "John",
        Email = "john@example.com",
        Age = 30
    }
};

var result = extractor.ExtractPaths(data, ["$.User.Name", "$.User.Email"]);
Console.WriteLine(expected.ToString(Formatting.Indented));
```

Prints:

```json
{
  "User": {
    "Name": "John",
    "Email": "email@example.com"
  }
}

```

## Features

### 1. Property Access

Access nested properties using dot notation:

```csharp
extractor.ExtractPaths(data, ["$.Parent.Child1"]);
```

**Case Insensitive**: Property names are matched case-insensitively.

```csharp
extractor.ExtractPaths(data, ["$.parent.child1"]);  // Works!
```

### 2. Array Operations

#### Single Index

```csharp
extractor.ExtractPaths(data, ["$.Items[0]"]);
```

#### Multiple Indices

```csharp
extractor.ExtractPaths(data, ["$.Items[0,2,4]"]);
```

#### Negative Indices

```csharp
extractor.ExtractPaths(data, ["$.Items[-1]"]);  // Last item
extractor.ExtractPaths(data, ["$.Items[-3,-1]"]);  // Multiple negative indices
```

#### Array Slicing

```csharp
extractor.ExtractPaths(data, ["$.Items[1:3]"]);   // Items at index 1 and 2
extractor.ExtractPaths(data, ["$.Items[:]"]);     // All items
extractor.ExtractPaths(data, ["$.Items[:5]"]);    // First 5 items
extractor.ExtractPaths(data, ["$.Items[5:]"]);    // From index 5 to end
extractor.ExtractPaths(data, ["$.Items[-3:]"]);    // Last 3
```

### 3. Wildcard Selection

#### Array Wildcard

```csharp
extractor.ExtractPaths(data, ["$.Products[*].Name"]);
// Extracts Name from all products
```

#### Object Wildcard

```csharp
extractor.ExtractPaths(data, ["$.Parent[*]"]);
// Extracts all properties of Parent
```

### 4. Multi-Field Selection

Select multiple fields at once using bracket notation:

```csharp
extractor.ExtractPaths(data, ["$.Products[*]['Name', 'Price']"]);
// Extracts only Name and Price from all products
```

### 5. Recursive Descent

Search for properties at any depth in the structure:

```csharp
extractor.ExtractPaths(data, ["$..Value"]);
// Finds all "Value" properties at any nesting level
```

#### Combined Recursive Operations

```csharp
extractor.ExtractPaths(data, ["$..Level1..Value"]);
// Finds Level1 at any depth, then Value at any depth under it
```

### 6. Nested Arrays

Handle deeply nested array structures:

```csharp
extractor.ExtractPaths(data, ["$.NestedArrays[0][0][1]"]);
extractor.ExtractPaths(data, ["$.NestedArrays[*][0][0]"]);
```

### 7. Multiple Paths

Extract multiple paths in a single operation:

```csharp
var result = extractor.ExtractPaths(data, [
    "$.Items[0]",
    "$.Items[1:3]",
    "$.Products[*].Name",
    "$..Child1",
    "$.Parent[*]"
]);
```

Overlapping paths are automatically merged into a single coherent structure.

### 8. Custom Serialization Settings

Support for custom JSON serialization settings:

```csharp
var settings = new JsonSerializerSettings {
    ContractResolver = new CamelCasePropertyNamesContractResolver()
};
var extractor = new PartialExtractor(settings);
```

Respects `[JsonProperty]` attributes:

```csharp
public class MyClass {
    [JsonProperty("display_name")]
    public string DisplayName { get; set; }
}

extractor.ExtractPaths(obj, ["$.CustomData.display_name"]);
```

More examples with responses in [tests](TestPartialQuery/PartialObjectExtractorTest.cs)

## Supported Path Syntax

| Syntax                            | Description                      | Example                                         |
|-----------------------------------|----------------------------------|-------------------------------------------------|
| `.property`                       | Dot notation property access     | `$.User.Name`                                   |
| `..property`                      | Recursive descent                | `$..Name`                                       |
| `['<property>' (, '<property>')]` | Bracket notation property access | `$['User']['Name']` or `$.User['Name','Email']` |
| `[<number> (, <number>)]`         | Array index or indices           | `$.Items[0]` or `$.Items[0,2,4]`                |
| `[start:end]`                     | Array slice                      | `$.Items[1:5]`                                  |
| `[*]`                             | Wildcard (all elements)          | `$.Products[*]`                                 |

## Edge Cases & Error Handling

- **Non-existent properties**: Returns empty object
- **Array index out of bounds**: Gracefully skipped
- **Invalid paths**: Throws `ArgumentException`
- **Empty collections**: Returns appropriate empty structure
- **Null source**: Returns empty `JObject`

## Performance Considerations

- **Reflection-first approach**: Uses reflection to traverse the object graph and extract only requested properties
- **Lazy serialization**: Only extracted values are serialized to JSON, not the entire source object
- **Efficient for large objects**: When you need a small subset of a large object, this is orders of magnitude faster than serialize-then-query approaches

