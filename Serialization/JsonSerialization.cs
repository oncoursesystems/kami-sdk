using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace OnCourse.Kami.Serialization;

public static class JsonSerialization
{
    public static JsonSerializerSettings GetDefaultSerializerSettings()
    {
        var converters = new JsonConverterCollection
        {
            new StringEnumConverter()
        };

        var settings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
            Converters = converters,
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy()
            }
        };

        return settings;
    }
}
