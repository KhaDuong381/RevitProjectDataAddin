using Autodesk.Revit.DB;
using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RevitProjectDataAddin
{
    public static class JsonHelper
    {
        private static JsonSerializerOptions _options;

        public static JsonSerializerOptions Options
        {
            get
            {
                if (_options == null)
                {
                    _options = CreateOptions();
                }
                return _options;
            }
        }

        private static JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // ✅ THÊM DÒNG NÀY
            };
            options.Converters.Add(new ElementIdConverter());
            return options;
        }
    }

    public class ElementIdConverter : JsonConverter<ElementId>
    {
        public override ElementId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            int intValue = reader.GetInt32();
            return new ElementId(intValue);
        }

        public override void Write(Utf8JsonWriter writer, ElementId value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value.IntegerValue);
        }
    }
}
