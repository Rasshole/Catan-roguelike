using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace CatanRoguelike.Core.Progression
{
    /// <summary>
    /// Versioned JSON persistence for meta progression. Path-agnostic for EditMode tests.
    /// </summary>
    public static class MetaSave
    {
        public const int CurrentFormatVersion = 1;

        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters = { new StringEnumConverter(new CamelCaseNamingStrategy()) }
        };

        public static string Serialize(MetaProgression progression)
        {
            var doc = new MetaSaveDocument
            {
                FormatVersion = CurrentFormatVersion,
                Stars = progression.Stars,
                UnlockedIds = progression.UnlockedIds.ToList(),
                AwardedRunKeys = progression.AwardedRunKeys.ToList()
            };
            return JsonConvert.SerializeObject(doc, JsonSettings);
        }

        public static MetaProgression Load(string json)
        {
            var doc = JsonConvert.DeserializeObject<MetaSaveDocument>(json, JsonSettings);
            if (doc == null)
                throw new InvalidOperationException("Meta JSON was empty or invalid.");

            if (doc.FormatVersion != CurrentFormatVersion)
                throw new InvalidOperationException(
                    $"Unsupported meta format version {doc.FormatVersion}. Expected {CurrentFormatVersion}.");

            return MetaProgression.FromSave(doc);
        }

        public static MetaSaveDocument Parse(string json)
        {
            var doc = JsonConvert.DeserializeObject<MetaSaveDocument>(json, JsonSettings);
            if (doc == null)
                throw new InvalidOperationException("Meta JSON was empty or invalid.");
            return doc;
        }
    }
}
