namespace CrediSoft.UITests;

/// <summary>
/// Define la colección "App" — todos los tests que la usan comparten una sola instancia
/// de AppFixture (y por tanto un solo proceso de la app lanzado).
/// </summary>
[CollectionDefinition("App")]
public class AppCollection : ICollectionFixture<AppFixture> { }
