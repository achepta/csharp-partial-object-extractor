using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Collections;
using System.Reflection;
using System.Text;

namespace PartialObjectExtractor;

public class PartialExtractor(JsonSerializerSettings? settings = null) {
    private readonly JsonSerializer serializer = JsonSerializer.Create(settings ?? new());

    public JObject ExtractPaths<T>(T source, List<string> jsonPaths) {
        if (source is null) {
            return new JObject();
        }

        var result = new JObject();
        foreach (var path in jsonPaths) {
            var segments = new PathParser(path).Parse();
            var extractedValues = ExtractValuesForPath(source, segments);
            foreach (var extracted in extractedValues) {
                BuildResultStructure(result, extracted);
            }
        }

        return result;
    }

    private List<ExtractedValue> ExtractValuesForPath(object source, List<PathSegment> segments) {
        var results = new List<ExtractedValue>();
        ExtractRecursive(source, segments, 0, [], results);
        return results;
    }

    private void ExtractRecursive(object? current, List<PathSegment> segments, int segmentIndex,
        List<PathStep> currentPath, List<ExtractedValue> results) {
        if (current is null) {
            return;
        }

        if (segmentIndex >= segments.Count) {
            results.Add(new(currentPath.ToList(), current));
            return;
        }

        _ = segments[segmentIndex].Type switch {
            SegmentType.Property => HandlePropertySegment(current, segments, segmentIndex, currentPath, results),
            SegmentType.MultiIndex => HandleMultiIndexSegment(current, segments, segmentIndex, currentPath, results),
            SegmentType.Wildcard => HandleWildcardSegment(current, segments, segmentIndex, currentPath, results),
            SegmentType.Slice => HandleSliceSegment(current, segments, segmentIndex, currentPath, results),
            SegmentType.MultiField => HandleMultiFieldSegment(current, segments, segmentIndex, currentPath, results),
            SegmentType.RecursiveDescent => HandleRecursiveDescentSegment(current, segments, segmentIndex, currentPath, results),
            _ => 0
        };
    }

    private int HandlePropertySegment(object current, List<PathSegment> segments, int segmentIndex,
        List<PathStep> currentPath, List<ExtractedValue> results) {
        var hits = results.Count;
        var segment = segments[segmentIndex];

        if (segment.Name is not null && GetProperty(current.GetType(), segment.Name) is { CanRead: true } property) {
            var value = GetPropertyValue(current, property);
            var newPath = currentPath.ToList();
            newPath.Add(new PropertyStep(GetJsonPropertyName(property)));
            ExtractRecursive(value, segments, segmentIndex + 1, newPath, results);
        }

        return results.Count - hits;
    }

    private int HandleRecursiveDescentSegment(object current, List<PathSegment> segments, int segmentIndex,
        List<PathStep> currentPath, List<ExtractedValue> results) {
        var hits = results.Count;
        if (segmentIndex + 1 < segments.Count) {
            SearchRecursively(current, segments, segmentIndex + 1, currentPath, results);
        }

        return results.Count - hits;
    }

    private void SearchRecursively(object? current, List<PathSegment> segments, int segmentIndex,
        List<PathStep> currentPath, List<ExtractedValue> results) {
        if (current is null || segmentIndex >= segments.Count) {
            return;
        }

        var targetSegment = segments[segmentIndex];

        var type = current.GetType();
        
        if (targetSegment.Type is not SegmentType.Property) {
            ExtractRecursive(current, segments, segmentIndex, currentPath, results);
        }
        else if (targetSegment.Type is SegmentType.Property) {
            if (!IsSearchableType(type) || targetSegment.Name is null) {
                return;
            }

            if (GetProperty(type, targetSegment.Name) is { CanRead: true } property) {
                var value = GetPropertyValue(current, property);
                var newPath = currentPath.ToList();
                newPath.Add(new PropertyStep(GetJsonPropertyName(property)));
                ExtractRecursive(value, segments, segmentIndex + 1, newPath, results);
            }
        }

        switch (current) {
            case IList list:
                for (var i = 0; i < list.Count; i++) {
                    var newPath = currentPath.ToList();
                    newPath.Add(new IndexStep(i));
                    SearchRecursively(list[i], segments, segmentIndex, newPath, results);
                }

                break;
            case IDictionary dict:
                foreach (DictionaryEntry entry in dict) {
                    if (entry.Key.ToString() is { } key) {
                        var newPath = currentPath.ToList();
                        newPath.Add(new PropertyStep(key));
                        SearchRecursively(entry.Value, segments, segmentIndex, newPath, results);
                    }
                }

                break;
            default:
                if (!IsSearchableType(type)) {
                    return;
                }

                foreach (var prop in GetProperties(type).Where(p => p.CanRead && p.GetIndexParameters().Length == 0)) {
                    try {
                        if (GetPropertyValue(current, prop) is { } value) {
                            var newPath = currentPath.ToList();
                            newPath.Add(new PropertyStep(GetJsonPropertyName(prop)));
                            SearchRecursively(value, segments, segmentIndex, newPath, results);
                        }
                    }
                    catch {
                        /* ignored */
                    }
                }

                break;
        }
    }

    private int HandleMultiFieldSegment(object current, List<PathSegment> segments, int segmentIndex,
        List<PathStep> currentPath, List<ExtractedValue> results) {
        var hits = results.Count;
        var segment = segments[segmentIndex];
        var type = current.GetType();
        var isLast = segmentIndex == segments.Count - 1;

        foreach (var fieldName in segment.FieldNames ?? []) {
            if (GetProperty(type, fieldName) is not { CanRead: true } property) {
                continue;
            }

            var value = GetPropertyValue(current, property);
            var newPath = currentPath.ToList();
            newPath.Add(new PropertyStep(GetJsonPropertyName(property)));

            if (isLast) {
                results.Add(new(newPath, value));
            }
            else {
                ExtractRecursive(value, segments, segmentIndex + 1, newPath, results);
            }
        }

        return results.Count - hits;
    }

    private int HandleWildcardSegment(object current, List<PathSegment> segments, int segmentIndex,
        List<PathStep> currentPath, List<ExtractedValue> results) {
        var hits = results.Count;
        var isLast = segmentIndex == segments.Count - 1;

        switch (current) {
            case IList list:
                if (list.Count == 0 && isLast) {
                    results.Add(new(currentPath.ToList(), list));
                }
                else {
                    for (int i = 0; i < list.Count; i++) {
                        var newPath = currentPath.ToList();
                        newPath.Add(new IndexStep(i));
                        
                        if (isLast) {
                            results.Add(new(newPath, list[i]));
                        }
                        else {
                            ExtractRecursive(list[i], segments, segmentIndex + 1, newPath, results);
                        }
                    }
                }
                break;
            case IDictionary dict:
                foreach (DictionaryEntry entry in dict) {
                    if (entry.Key.ToString() is { } key) {
                        var newPath = currentPath.ToList();
                        newPath.Add(new PropertyStep(key));
                        
                        if (isLast) {
                            results.Add(new(newPath, entry.Value));
                        }
                        else {
                            ExtractRecursive(entry.Value, segments, segmentIndex + 1, newPath, results);
                        }
                    }
                }
                break;
            default:
                foreach (var property in GetProperties(current.GetType()).Where(p => p.CanRead && p.GetIndexParameters().Length == 0)) {
                    var value = GetPropertyValue(current, property);
                    var newPath = currentPath.ToList();
                    newPath.Add(new PropertyStep(GetJsonPropertyName(property)));
                    
                    if (isLast) {
                        results.Add(new(newPath, value));
                    }
                    else {
                        ExtractRecursive(value, segments, segmentIndex + 1, newPath, results);
                    }
                }
                break;
        }

        return results.Count - hits;
    }

    private int HandleMultiIndexSegment(object current, List<PathSegment> segments, int segmentIndex,
        List<PathStep> currentPath, List<ExtractedValue> results) {
        var hits = results.Count;
        if (current is not IList list) {
            return 0;
        }

        foreach (var index in segments[segmentIndex].Indices ?? []) {
            var actualIndex = ResolveIndex(index, list.Count);
            if (actualIndex < 0 || actualIndex >= list.Count) {
                continue;
            }

            var newPath = currentPath.ToList();
            newPath.Add(new IndexStep(actualIndex));
            ExtractRecursive(list[actualIndex], segments, segmentIndex + 1, newPath, results);
        }

        return results.Count - hits;
    }

    private int HandleSliceSegment(object current, List<PathSegment> segments, int segmentIndex,
        List<PathStep> currentPath, List<ExtractedValue> results) {
        var hits = results.Count;
        if (current is not IList list) {
            return 0;
        }

        var segment = segments[segmentIndex];
        var listCount = list.Count;

        var start = Math.Clamp(segment.SliceStart.HasValue ? ResolveIndex(segment.SliceStart.Value, listCount) : 0, 0, listCount);
        var end = Math.Clamp(segment.SliceEnd.HasValue ? ResolveIndex(segment.SliceEnd.Value, listCount) : listCount, 0, listCount);

        if (start >= end && segmentIndex == segments.Count - 1) {
            results.Add(new(currentPath.ToList(), new List<object>()));
            return results.Count - hits;
        }

        for (int i = start; i < end; i++) {
            var newPath = currentPath.ToList();
            newPath.Add(new IndexStep(i));
            ExtractRecursive(list[i], segments, segmentIndex + 1, newPath, results);
        }

        return results.Count - hits;
    }

    private static int ResolveIndex(int index, int listCount) => index < 0 ? listCount + index : index;

    private static bool IsSearchableType(Type type) =>
        !type.IsPrimitive && type != typeof(string) && type != typeof(decimal);

    private void BuildResultStructure(JObject result, ExtractedValue extractedValue) {
        if (extractedValue.Path.Count == 0) {
            return;
        }

        JToken current = result;
        for (int i = 0; i < extractedValue.Path.Count; i++) {
            var step = extractedValue.Path[i];
            var isLast = i == extractedValue.Path.Count - 1;

            switch (step) {
                case PropertyStep { Name: var name }:
                    if (isLast) {
                        current[name] = extractedValue.Value is null ? null : JToken.FromObject(extractedValue.Value, serializer);
                    }
                    else {
                        current[name] ??= extractedValue.Path[i + 1] is IndexStep ? new JArray() : new JObject();
                        current = current[name]!;
                    }

                    break;

                case IndexStep { Index: var index }:
                    var arr = (JArray)current;
                    while (arr.Count <= index) {
                        arr.Add(JValue.CreateNull());
                    }

                    if (isLast) {
                        arr[index] = JToken.FromObject(extractedValue.Value ?? new { }, serializer);
                    }
                    else {
                        if (arr[index]!.Type is JTokenType.Null) {
                            arr[index] = extractedValue.Path[i + 1] is IndexStep ? new JArray() : new JObject();
                        }

                        current = arr[index]!;
                    }

                    break;
            }
        }
    }

    private object? GetPropertyValue(object obj, PropertyInfo property) {
        try {
            return property.GetIndexParameters().Length > 0 ? null : property.GetValue(obj);
        }
        catch {
            return null;
        }
    }

    private PropertyInfo? GetProperty(Type type, string name) {
        if (type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) is { } prop) {
            return prop;
        }

        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p => p.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName == name);
    }

    private IEnumerable<PropertyInfo> GetProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0);

    private string GetJsonPropertyName(PropertyInfo property) {
        var jsonProp = property.GetCustomAttribute<JsonPropertyAttribute>();
        return jsonProp?.PropertyName ?? (serializer.ContractResolver is CamelCasePropertyNamesContractResolver
            ? ToCamelCase(property.Name)
            : property.Name);
    }

    private static string ToCamelCase(string str) =>
        string.IsNullOrEmpty(str) || char.IsLower(str[0]) ? str : char.ToLower(str[0]) + str[1..];

    private class PathParser(string input) {
        private int position;
        private readonly int length = input.Length;

        public List<PathSegment> Parse() {
            var segments = new List<PathSegment>();
            if (Peek() is '$') {
                position++;
            }

            while (position < length) {
                if (Peek() is '.') {
                    position++;
                    if (Peek() is '.') {
                        position++;
                        segments.Add(new() { Type = SegmentType.RecursiveDescent });
                    }

                    continue;
                }

                if (ParseSegment() is { } segment) {
                    segments.Add(segment);
                    while (Peek() is '[' && ParseBracketNotation() is { } bracketSegment) {
                        segments.Add(bracketSegment);
                    }
                }
            }

            return segments;
        }

        private PathSegment? ParseSegment() {
            if (position >= length) {
                return null;
            }

            return Peek() is '[' ? ParseBracketNotation() : ParseProperty();
        }

        private PathSegment? ParseProperty() {
            var start = position;
            while (position < length && Peek() is not ('.' or '[')) {
                position++;
            }

            return position > start ? new() { Type = SegmentType.Property, Name = input[start..position] } : null;
        }

        private PathSegment? ParseBracketNotation() {
            position++;
            SkipWhitespace();

            return Peek() switch {
                '\'' or '"' => ParseMultiField(),
                '*' => ParseWildcard(),
                _ => ParseNumericBracket()
            };
        }

        private PathSegment ParseWildcard() {
            position++;
            SkipWhitespace();
            ExpectChar(']');
            return new() { Type = SegmentType.Wildcard };
        }

        private PathSegment ParseMultiField() {
            var fields = new List<string>();
            while (position < length && Peek() is not ']') {
                SkipWhitespace();
                if (Peek() is '\'' or '"') {
                    fields.Add(ParseQuotedString(Peek()));
                }

                SkipWhitespace();
                if (Peek() is ',') {
                    position++;
                }
                else if (Peek() is ']') {
                    break;
                }
            }

            ExpectChar(']');
            return new() { Type = SegmentType.MultiField, FieldNames = fields };
        }

        private string ParseQuotedString(char quoteChar) {
            position++;
            var sb = new StringBuilder();
            while (position < length) {
                var ch = input[position];
                if (ch == quoteChar) {
                    position++;
                    break;
                }

                if (ch is '\\' && position + 1 < length) {
                    position++;
                    sb.Append(input[position]);
                }
                else {
                    sb.Append(ch);
                }

                position++;
            }

            return sb.ToString();
        }

        private PathSegment? ParseNumericBracket() {
            var indices = new List<int>();
            int? sliceStart = null, sliceEnd = null;
            var isSlice = false;

            while (position < length && Peek() is not ']') {
                SkipWhitespace();
                if (Peek() is ':') {
                    isSlice = true;
                    position++;
                    SkipWhitespace();
                    if (Peek() is not (']' or ',')) {
                        sliceEnd = ParseInteger();
                    }
                }
                else if (Peek() is not ']') {
                    var num = ParseInteger();
                    SkipWhitespace();

                    if (Peek() is ':') {
                        isSlice = true;
                        sliceStart = num;
                        position++;
                        SkipWhitespace();
                        if (Peek() is not ']') {
                            sliceEnd = ParseInteger();
                        }
                    }
                    else if (Peek() is ',') {
                        indices.Add(num);
                        position++;
                    }
                    else {
                        if (!isSlice) {
                            indices.Add(num);
                        }
                        else {
                            sliceEnd = num;
                        }
                    }
                }

                SkipWhitespace();
            }

            ExpectChar(']');

            return isSlice
                ? new() { Type = SegmentType.Slice, SliceStart = sliceStart, SliceEnd = sliceEnd }
                : indices.Count > 0
                    ? new() { Type = SegmentType.MultiIndex, Indices = indices }
                    : null;
        }

        private int ParseInteger() {
            var sb = new StringBuilder();
            if (Peek() is '-') {
                sb.Append('-');
                position++;
            }

            while (char.IsDigit(Peek())) {
                sb.Append(Peek());
                position++;
            }

            if (sb.Length is 0 || sb.ToString() is "-") {
                throw new ArgumentException($"Expected integer at position {position}");
            }

            return int.Parse(sb.ToString());
        }

        private void SkipWhitespace() {
            while (char.IsWhiteSpace(Peek())) {
                position++;
            }
        }

        private char Peek() => position < length ? input[position] : '\0';

        private void ExpectChar(char expected) {
            if (Peek() == expected) {
                position++;
            }
            else {
                throw new ArgumentException($"Expected '{expected}' at position {position}, found '{Peek()}'");
            }
        }
    }

    private abstract record PathStep;

    private record PropertyStep(string Name) : PathStep;

    private record IndexStep(int Index) : PathStep;

    private class PathSegment {
        public required SegmentType Type { get; init; }
        public string? Name { get; init; }
        public List<int>? Indices { get; init; }
        public int? SliceStart { get; init; }
        public int? SliceEnd { get; init; }
        public List<string>? FieldNames { get; init; }
    }

    private enum SegmentType {
        Property,
        Wildcard,
        MultiIndex,
        Slice,
        MultiField,
        RecursiveDescent
    }

    private record ExtractedValue(List<PathStep> Path, object? Value);
}