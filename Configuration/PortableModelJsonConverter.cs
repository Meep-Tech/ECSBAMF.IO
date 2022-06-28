using Meep.Tech.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Meep.Tech.Data.IO.Configuration {

  /// <summary>
  /// Used to convert individual models to/from json using auto-porting
  /// </summary>
  public class PortableModelJsonConverter : Meep.Tech.Data.Model.JsonConverter {

    public Universe Universe {
      get;
    }

    public PortableModelJsonConverter(Universe universe) {
      Universe = universe;
    }

    public override IModel ReadJson(JsonReader reader, Type objectType, [AllowNull] IModel existingValue, bool hasExistingValue, JsonSerializer serializer) {
      if (reader.TokenType == JsonToken.StartObject) {
        return base.ReadJson(reader, objectType, existingValue, hasExistingValue, serializer);
      }
      else if (reader.TokenType == JsonToken.String) {
        return Universe.GetModData().GetModelPorter(objectType).LoadByKey(reader.ReadAsString());
      }
      else throw new JsonException();
    }

    public override void WriteJson(JsonWriter writer, [AllowNull] IModel value, JsonSerializer serializer) {
      writer.WriteValue((value as IUnique).Id);
    }
  }

  /// <summary>
  /// Used to convert collections of models to/from json using auto-porting
  /// </summary>
  public class PortableModelsCollectionJsonConverter : JsonConverter<IEnumerable<IUnique>> {
    public Universe Universe { get; }

    public PortableModelsCollectionJsonConverter(Universe universe) {
      Universe = universe;
    }


    public override IEnumerable<IUnique> ReadJson(JsonReader reader, Type objectType, [AllowNull] IEnumerable<IUnique> existingValue, bool hasExistingValue, JsonSerializer serializer) {
      foreach (var item in JArray.Load(reader)) {
        if (item.Type == JTokenType.String) {
          yield return (IUnique)IModel.FromJson(item as JObject, objectType, Universe);
        } else if (item.Type == JTokenType.Object) {
          yield return (IUnique)Universe.GetModData().GetModelPorter(objectType).LoadByKey(item.Value<string>());
        }
      }
    }

    public override void WriteJson(JsonWriter writer, [AllowNull] IEnumerable<IUnique> value, JsonSerializer serializer) {
      writer.WriteStartArray();
      foreach (var item in value) {
        writer.WriteValue(item.Id);
      }
      writer.WriteEndArray();
    }
  }

  /// <summary>
  /// Used to convert collections of models to/from json using auto-porting
  /// </summary>
  public class PortableModelsDictionaryJsonConverter : JsonConverter<IReadOnlyDictionary<string, IUnique>> {
    public Universe Universe { get; }

    public PortableModelsDictionaryJsonConverter(Universe universe) {
      Universe = universe;
    }

    public override IReadOnlyDictionary<string, IUnique> ReadJson(JsonReader reader, Type objectType, [AllowNull] IReadOnlyDictionary<string, IUnique> existingValue, bool hasExistingValue, JsonSerializer serializer) {
      Dictionary<string, IUnique> models = new();
      foreach (var item in JArray.Load(reader)) {
        IUnique model = null;
        if (item.Type == JTokenType.String) {
          model = (IUnique)IModel.FromJson(item as JObject, objectType, Universe);
        }
        else if (item.Type == JTokenType.Object) {
          model = (IUnique)Universe.GetModData().GetModelPorter(objectType).LoadByKey(item.Value<string>());
        }

        if (model is not null) {
          models.Add(model, m => m.Id);
        }
      }

      return models;
    }

    public override void WriteJson(JsonWriter writer, [AllowNull] IReadOnlyDictionary<string, IUnique> value, JsonSerializer serializer) {
      writer.WriteStartArray();
      foreach (var item in value) {
        writer.WriteValue(item.Value.Id);
      }
      writer.WriteEndArray();
    }
  }
}
