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
      writer.WriteValue(((IUnique)value).Id);
    }
  }

  /// <summary>
  /// Used to convert collections of models to/from json using auto-porting
  /// </summary>
  public class PortableModelsCollectionJsonConverter<TModel> : JsonConverter<IEnumerable<TModel>> where TModel : IUnique {
    public Universe Universe { get; }

    public PortableModelsCollectionJsonConverter(Universe universe) {
      Universe = universe;
    }


    public override IEnumerable<TModel> ReadJson(JsonReader reader, Type objectType, [AllowNull] IEnumerable<TModel> existingValue, bool hasExistingValue, JsonSerializer serializer) {
      foreach (var item in JArray.Load(reader)) {
        if (item.Type == JTokenType.String) {
          yield return (TModel)Universe.GetModData().GetModelPorter(objectType).LoadByKey(item.Value<string>());
        } else if (item.Type == JTokenType.Object) {
          yield return (TModel)IModel.FromJson(item as JObject, objectType, Universe);
        }
      }
    }

    public override void WriteJson(JsonWriter writer, [AllowNull] IEnumerable<TModel> value, JsonSerializer serializer) {
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
  public class PortableModelsDictionaryWithKeysJsonConverter<TModel> : JsonConverter<IReadOnlyDictionary<string, TModel>> where TModel : IUnique {
    public Universe Universe { get; }

    public PortableModelsDictionaryWithKeysJsonConverter(Universe universe) {
      Universe = universe;
    }

    public override IReadOnlyDictionary<string, TModel> ReadJson(JsonReader reader, Type objectType, [AllowNull] IReadOnlyDictionary<string, TModel> existingValue, bool hasExistingValue, JsonSerializer serializer) {
      Dictionary<string, TModel> models = new();
      foreach (var item in JObject.Load(reader)) {
        TModel model;
        if (item.Value.Type == JTokenType.String) {
          model = (TModel)Universe.GetModData().GetModelPorter(typeof(TModel)).LoadByKey(item.Value.Value<string>());
        }
        else if (item.Value.Type == JTokenType.Object) {
          model = (TModel)IModel.FromJson(item.Value as JObject, typeof(TModel), Universe);
        }
        else throw new JsonException();

        if (model is not null) {
          models.Add(item.Key, model);
        }
      }

      return models;
    }

    public override void WriteJson(JsonWriter writer, [AllowNull] IReadOnlyDictionary<string, TModel> value, JsonSerializer serializer) {
      writer.WriteStartObject();
      foreach (var item in value) {
        writer.WritePropertyName(item.Key);
        writer.WriteValue(item.Value.Id);
      }
      writer.WriteEndObject();
    }
  }

  /// <summary>
  /// Used to convert collections of models to/from json using auto-porting
  /// </summary>
  public class PortableModelsDictionaryJsonConverter<TModel> : JsonConverter<IReadOnlyDictionary<string, TModel>> where TModel : IUnique {
    public Universe Universe { get; }

    public PortableModelsDictionaryJsonConverter(Universe universe) {
      Universe = universe;
    }

    public override IReadOnlyDictionary<string, TModel> ReadJson(JsonReader reader, Type objectType, [AllowNull] IReadOnlyDictionary<string, TModel> existingValue, bool hasExistingValue, JsonSerializer serializer) {
      Dictionary<string, TModel> models = new();
      foreach (var item in JArray.Load(reader)) {
        TModel model;
        if (item.Type == JTokenType.String) {
          model = (TModel)Universe.GetModData().GetModelPorter(typeof(TModel)).LoadByKey(item.Value<string>());
        } else if (item.Type == JTokenType.Object) {
          model = (TModel)IModel.FromJson(item as JObject, typeof(TModel), Universe);
        } else throw new JsonException();

        if (model is not null) {
          models.Add(model, m => m.Id);
        }
      }

      return models;
    }

    public override void WriteJson(JsonWriter writer, [AllowNull] IReadOnlyDictionary<string, TModel> value, JsonSerializer serializer) {
      writer.WriteStartArray();
      foreach (var item in value) {
        writer.WriteValue(item.Value.Id);
      }
      writer.WriteEndArray();
    }
  }
}
