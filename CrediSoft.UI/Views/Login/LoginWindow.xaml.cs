using CrediSoft.Core.Services;
using CrediSoft.UI.Views.Main;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Input;

namespace CrediSoft.UI.Views.Login;

public partial class LoginWindow : Window
{
    private readonly AuthService _authService;

    public LoginWindow()
    {
        InitializeComponent();
        _authService = App.Services.GetRequiredService<AuthService>();
        TxtUsuario.Focus();
    }

    private async void BtnIngresar_Click(object sender, RoutedEventArgs e)
    {
        await IntentarLogin();
    }

    private async void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            await IntentarLogin();
    }

    private async Task IntentarLogin()
    {
        TxtError.Visibility = Visibility.Collapsed;
        BtnIngresar.IsEnabled = false;
        BtnIngresar.Content = "Verificando...";

        try
        {
            if (!int.TryParse(TxtLocal.Text.Trim(), out var idLocal))
            {
                MostrarError("Ingrese un número de local válido.");
                return;
            }

            var resultado = await _authService.LoginAsync(
                TxtUsuario.Text.Trim(),
                TxtContrasena.Password,
                idLocal);

            if (resultado.Exitoso)
            {
                var main = new MainWindow();
                main.Show();
                Close();
            }
            else
            {
                MostrarError(resultado.MensajeError);
                TxtContrasena.Clear();
                TxtContrasena.Focus();
            }
        }
        catch (Exception ex)
        {
            MostrarError($"Error de conexión: {ex.Message}");
        }
        finally
        {
            BtnIngresar.IsEnabled = true;
            BtnIngresar.Content = "INGRESAR";
        }
    }

    private void MostrarError(string mensaje)
    {
        TxtError.Text = mensaje;
        TxtError.Visibility = Visibility.Visible;
    }
}
