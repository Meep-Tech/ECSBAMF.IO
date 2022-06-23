using System;

namespace Meep.Tech.Data.IO {

  /// <summary>
  /// Indicates that when this field is deserialized (via json) it should try to load the models as a collection; first via the cache, then via porting if a string id was provided instead of a json object
  /// </summary>
  [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
  public class AutoPortAttribute : Attribute {}
}
