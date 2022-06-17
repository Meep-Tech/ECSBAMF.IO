using Meep.Tech.Data.Configuration;

namespace Meep.Tech.Data.IO {

  /// <summary>
  /// Indicates this archetype has built in instructions for creating a model porter.
  /// </summary>
  /// <typeparam name="TModel"></typeparam>
  public interface IHavePortableModel<TModel> 
    : ITrait<IHavePortableModel<TModel>>
    where TModel : IModel
  {

    /// <summary>
    /// Used to create a model impoerter for the given type.
    /// </summary>
    /// <returns></returns>
    ModelPorter<TModel> CreateModelPorter();

    string ITrait<IHavePortableModel<TModel>>.TraitName
      => $"Has Model Import Settings";

    string ITrait<IHavePortableModel<TModel>>.TraitDescription
      => $"This Archetype tree provides instructions on how to import the model type: {typeof(TModel).FullName}";
  }
}
