using CrediSoft.Core.Interfaces;
using CrediSoft.Core.Models;
using CrediSoft.Core.Services;
using CrediSoft.UI.Views.Main;
using CrediSoft.UI.Views.Shared;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CrediSoft.UI.Views.Login;

public partial class LoginWindow : Window
{
    private readonly AuthService        _authService;
    private readonly IUsuarioRepository _usuarios;
    private readonly ILocalRepository   _locales;

    private List<Local> _todosLocales  = new();
    private Local?      _localActual   = null;
    private bool        _mostrandoPass = false;

    // Segoe MDL2 Assets: E7B3 = View (ojo abierto), E7B4 = Hide (ojo tachado)
    private const string IcoView = "";
    private const string IcoHide = "";

    public LoginWindow()
    {
        InitializeComponent();
        CargarIconoVentana();
        _authService = App.Services.GetRequiredService<AuthService>();
        _usuarios    = App.Services.GetRequiredService<IUsuarioRepository>();
        _locales     = App.Services.GetRequiredService<ILocalRepository>();
        TxtUsuario.Focus();
        TxtUsuario.LostFocus += async (_, _) => await AutocompletarLocal();
        MouseLeftButtonDown   += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
        CargarLogo();
        Loaded += async (_, _) =>
        {
            TraerAlFrente();
            _todosLocales = (await _locales.ListarTodosAsync()).ToList();
        };
    }

    // Cuando el proceso se relanza desde el batch del actualizador (sin interaccion del
    // usuario, sin foco propio), Windows puede abrir la ventana minimizada o detras de otras.
    // Forzamos estado normal + activacion + un Topmost momentaneo para garantizar que quede
    // al frente igual que si el usuario hubiera abierto el exe manualmente.
    private void TraerAlFrente()
    {
        WindowState = WindowState.Normal;
        Show();
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void CargarIconoVentana()
    {
        var ruta = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "credimar.ico");
        if (!System.IO.File.Exists(ruta)) return;

        try
        {
            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(ruta, UriKind.Absolute);
            bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            Icon = bmp;
        }
        catch { }
    }

    private void CargarLogo()
    {
        const string nombre = "logotipocredimar2.png";
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var candidatos = new[]
        {
            System.IO.Path.Combine(baseDir, nombre),
            System.IO.Path.Combine(baseDir, "..", nombre),
            System.IO.Path.Combine(baseDir, "..", "..", "..", "..", "..", nombre),
        };
        var ruta = candidatos.FirstOrDefault(System.IO.File.Exists);
        if (ruta == null) return;

        try
        {
            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.UriSource   = new Uri(ruta, UriKind.Absolute);
            bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            ImgLogo.Source = bmp;
        }
        catch { }
    }

    // ── Toggle mostrar/ocultar contraseña ─────────────────────────────────────
    private void OnCerrarApp(object s, RoutedEventArgs e) => Application.Current.Shutdown();

    private void OnTogglePassword(object s, RoutedEventArgs e)
    {
        _mostrandoPass = !_mostrandoPass;
        var ico = BtnTogglePass.Template.FindName("IcoOjo", BtnTogglePass) as TextBlock;

        if (_mostrandoPass)
        {
            TxtContrasenaVisible.Text       = TxtContrasena.Password;
            TxtContrasena.Visibility        = Visibility.Collapsed;
            TxtContrasenaVisible.Visibility = Visibility.Visible;
            TxtContrasenaVisible.CaretIndex = TxtContrasenaVisible.Text.Length;
            if (ico != null) ico.Text = IcoHide;
        }
        else
        {
            TxtContrasena.Password          = TxtContrasenaVisible.Text;
            TxtContrasenaVisible.Visibility = Visibility.Collapsed;
            TxtContrasena.Visibility        = Visibility.Visible;
            TxtContrasena.Focus();
            if (ico != null) ico.Text = IcoView;
        }
    }

    // ── Local (solo informativo) ────────────────────────────────────────────────
    // El local ya no es seleccionable manualmente: se autocompleta desde
    // USUARIOS.LOCAL_USUARIO al escribir el código de usuario (ver AutocompletarLocal) y
    // AuthService.LoginAsync lo resuelve de nuevo internamente al autenticar, ignorando
    // cualquier valor mostrado en pantalla.
    private void SetLocal(Local local)
    {
        _localActual = local;

        if (TxtLocalNombre != null)
        {
            TxtLocalNombre.Text       = local.NombreLocal;
            TxtLocalNombre.Foreground = System.Windows.Media.Brushes.White;
        }
        if (TxtLocalNum != null)
            TxtLocalNum.Text = $"[{local.IdLocal}]";
        if (ChipNum != null)
            ChipNum.Visibility = Visibility.Visible;
    }

    private async Task AutocompletarLocal()
    {
        var codigo = TxtUsuario.Text.Trim();
        if (string.IsNullOrEmpty(codigo)) return;

        var usuario = await _usuarios.BuscarPorCodigoAsync(codigo);
        if (usuario == null) return;

        if (_todosLocales.Count == 0)
            _todosLocales = (await _locales.ListarTodosAsync()).ToList();

        var local = _todosLocales.FirstOrDefault(l => l.IdLocal == usuario.LocalUsuario)
                    ?? await _locales.ObtenerPorIdAsync(usuario.LocalUsuario);
        if (local != null)
            SetLocal(local);
    }

    // ── Login ─────────────────────────────────────────────────────────────────
    private async void BtnIngresar_Click(object sender, RoutedEventArgs e) => await IntentarLogin();

    private async void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await IntentarLogin();
    }

    private async Task IntentarLogin()
    {
        PanelError.Visibility = Visibility.Collapsed;
        BtnIngresar.IsEnabled = false;
        BtnIngresar.Content   = "Verificando...";

        // Si la contraseña está visible, sincronizarla al PasswordBox
        if (_mostrandoPass)
            TxtContrasena.Password = TxtContrasenaVisible.Text;

        try
        {
            var resultado = await _authService.LoginAsync(
                TxtUsuario.Text.Trim(),
                TxtContrasena.Password);

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
                TxtContrasenaVisible.Text = "";
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
            BtnIngresar.Content   = "INGRESAR";
        }
    }

    private void MostrarError(string mensaje)
    {
        TxtError.Text         = mensaje;
        PanelError.Visibility = Visibility.Visible;
    }
}

