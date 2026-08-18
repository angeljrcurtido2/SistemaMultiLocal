namespace CrediSoft.UITests;

/// <summary>
/// Test 1 — Login.
/// </summary>
[Collection("App")]
public class LoginTests
{
    private readonly AppFixture _fx;
    public LoginTests(AppFixture fx) => _fx = fx;

    [Fact]
    public void T01_Login_CredencialesCorrectas_AbreMainWindow()
    {
        var main = _fx.Login();

        var status = main.FindFirstDescendant(c => c.ByAutomationId("TxtStatusUsuario"));
        Assert.NotNull(status);

        // TextBlock WPF: el texto llega en .Name, no en ValuePattern
        var texto = status.Name ?? "";
        Assert.False(string.IsNullOrWhiteSpace(texto),
            "TxtStatusUsuario debe mostrar el nombre del usuario logueado.");
    }
}
