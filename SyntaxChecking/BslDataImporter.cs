using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeDictionary.SyntaxChecking
{
    public class BslDataImporter
    {
        public int MethodsImported { get; private set; }
        public int PropertiesImported { get; private set; }
        public int TypesImported { get; private set; }

        public void ImportFromFolder(string folderPath)
        {
            var methodsPath = Path.Combine(folderPath, "global-methods.json");
            var propertiesPath = Path.Combine(folderPath, "global-properties.json");
            var typesPath = Path.Combine(folderPath, "types.json");

            var hasMethods = File.Exists(methodsPath);
            var hasProperties = File.Exists(propertiesPath);
            var hasTypes = File.Exists(typesPath);

            if (!hasMethods && !hasProperties && !hasTypes)
                throw new FileNotFoundException(
                    "Не найдены файлы импорта (global-methods.json, global-properties.json, types.json) в папке: " + folderPath);

            if (hasMethods)
            {
                var json = File.ReadAllText(methodsPath);
                var methods = JsonSerializer.Deserialize<List<JsonGlobalMethod>>(json)
                    ?? new List<JsonGlobalMethod>();
                BslGlobalContext.ImportGlobalMethods(methods);
                MethodsImported = methods.Count;
            }

            if (hasProperties)
            {
                var json = File.ReadAllText(propertiesPath);
                var props = JsonSerializer.Deserialize<List<JsonGlobalProperty>>(json)
                    ?? new List<JsonGlobalProperty>();
                BslGlobalContext.ImportGlobalProperties(props);
                PropertiesImported = props.Count;
            }

            if (hasTypes)
            {
                var json = File.ReadAllText(typesPath);
                var types = JsonSerializer.Deserialize<List<JsonTypeDefinition>>(json)
                    ?? new List<JsonTypeDefinition>();
                BslTypeSystem.ImportFromJson(types);
                TypesImported = types.Count;
            }
        }
    }

    // JSON models for global-methods.json
    public class JsonGlobalMethod
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("name_en")]
        public string? NameEn { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("signature")]
        public List<JsonMethodSignature>? Signature { get; set; }

        [JsonPropertyName("return")]
        public string? Return { get; set; }
    }

    public class JsonMethodSignature
    {
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("params")]
        public List<JsonParamDefinition>? Params { get; set; }
    }

    public class JsonParamDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("required")]
        public bool Required { get; set; }
    }

    // JSON models for global-properties.json
    public class JsonGlobalProperty
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("name_en")]
        public string? NameEn { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("readonly")]
        public bool ReadOnly { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }

    // JSON models for types.json
    public class JsonTypeDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("methods")]
        public List<JsonTypeMethod>? Methods { get; set; }

        [JsonPropertyName("properties")]
        public List<JsonTypeProperty>? Properties { get; set; }

        [JsonPropertyName("constructors")]
        public List<JsonSignature>? Constructors { get; set; }
    }

    public class JsonTypeMethod
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("return")]
        public string? Return { get; set; }

        [JsonPropertyName("returnTypeDefinition")]
        public JsonReturnTypeDefinition? ReturnTypeDefinition { get; set; }

        [JsonPropertyName("params")]
        public List<JsonParamDefinition>? Params { get; set; }

        [JsonPropertyName("signature")]
        public List<JsonSignature>? Signature { get; set; }
    }

    public class JsonReturnTypeDefinition
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    public class JsonTypeProperty
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("readonly")]
        public bool ReadOnly { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }

    public class JsonSignature
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("params")]
        public List<JsonParamDefinition>? Params { get; set; }
    }
}
