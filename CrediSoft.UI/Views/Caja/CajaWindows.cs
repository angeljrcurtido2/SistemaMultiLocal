using CrediSoft.Core.Services;
using CrediSoft.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CrediSoft.UI.Views.Caja;

// ── Apertura de Caja ──────────────────────────────────────────────────────────

public class CajaAperturaWindow : Window
{
    private readonly ICajaRepository _caja;
    private readonly ISessionService _session;

    public CajaAperturaWindow()
    {
        _caja = App.Services.GetRequiredService<ICajaRepository>();
        _session = SessionService.Instance;
        Title = "Apertura de Caja";
        Width = 420; Height = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
    }

    private TextBox _txtMonto = null!;

    private void BuildUI()
    {
        var panel = new StackPanel { Margin = new Thickness(24) };

        panel.Children.Add(new TextBlock
        {
            Text = $"Local: {_session.LocalActual?.NombreLocal}",
            FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 16)
        });

        panel.Children.Add(new TextBlock { Text = "Monto base (fondo de caja):", FontSize = 13, Margin = new Thickness(0, 0, 0, 6) });
        _txtMonto = new TextBox { Padding = new Thickness(6, 4, 6, 4), FontSize = 15, FontWeight = FontWeights.Bold };
        panel.Children.Add(_txtMonto);

        panel.Children.Add(new TextBlock
        {
            Text = $"Fecha: {DateTime.Now:dd/MM/yyyy HH:mm}",
            FontSize = 12, Foreground = System.Windows.Media.Brushes.Gray,
            Margin = new Thickness(0, 12, 0, 0)
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"Usuario: {_session.UsuarioActual?.NombreUsuario}",
            FontSize = 12, Foreground = System.Windows.Media.Brushes.Gray,
            Margin = new Thickness(0, 4, 0, 16)
        });

        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var btnAbrir = new Button { Content = "✅ Abrir Caja", Width = 120, Height = 34, Margin = new Thickness(0, 0, 8, 0),
                                    Background = System.Windows.Media.Brushes.Green,
                                    Foreground = System.Windows.Media.Brushes.White };
        btnAbrir.Click += OnAbrirCaja;
        var btnCancelar = new Button { Content = "Cancelar", Width = 80, Height = 34 };
        btnCancelar.Click += (_, _) => Close();
        btnPanel.Children.Add(btnAbrir);
        btnPanel.Children.Add(btnCancelar);
        panel.Children.Add(btnPanel);

        Content = panel;
        _txtMonto.Focus();
    }

    private async void OnAbrirCaja(object s, RoutedEventArgs e)
    {
        var cajaAbierta = await _caja.ObtenerCajaAbiertaAsync(_session.LocalActual!.IdLocal);
        if (cajaAbierta != null)
        { MessageBox.Show("Ya hay una caja abierta en este local.", "Aviso"); return; }

        if (!decimal.TryParse(_txtMonto.Text.Replace(",", "").Replace(".", ""), out var monto) || monto < 0)
        { MessageBox.Show("Ingrese un monto válido.", "Error"); return; }

        await _caja.AbrirCajaAsync(_session.LocalActual.IdLocal, _session.UsuarioActual!.IdUsuario, monto);
        MessageBox.Show($"Caja abierta correctamente.\nFondo: Gs. {monto:N0}", "OK");
        Close();
    }
}

// ── Cierre de Caja ────────────────────────────────────────────────────────────

public class CajaCierreWindow : Window
{
    private readonly ICajaRepository _caja;
    private readonly ISessionService _session;

    public CajaCierreWindow()
    {
        _caja = App.Services.GetRequiredService<ICajaRepository>();
        _session = SessionService.Instance;
        Title = "Cierre de Caja";
        Width = 460; Height = 380;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        BuildUI();
    }

    private TextBox _txtMontoCierre = null!, _txtObs = null!;
    private TextBlock _txtResumen = null!;
    private int _idMaster;

    private async void BuildUI()
    {
        var cajaAbierta = await _caja.ObtenerCajaAbiertaAsync(_session.LocalActual!.IdLocal);
        if (cajaAbierta == null)
        {
            Content = new TextBlock { Text = "No hay caja abierta en este local.", Margin = new Thickness(24), FontSize = 14 };
            return;
        }
        _idMaster = cajaAbierta.IdMaster;

        var panel = new StackPanel { Margin = new Thickness(24) };
        _txtResumen = new TextBlock
        {
            Text = $"Caja abierta: {cajaAbierta.FechaApertura:dd/MM/yyyy HH:mm}\n" +
                   $"Fondo base: Gs. {cajaAbierta.MontoBase:N0}",
            FontSize = 13, Margin = new Thickness(0, 0, 0, 16),
            Foreground = System.Windows.Media.Brushes.DarkGreen
        };
        panel.Children.Add(_txtResumen);

        panel.Children.Add(new TextBlock { Text = "Monto contado (efectivo real):", Margin = new Thickness(0, 0, 0, 4) });
        _txtMontoCierre = new TextBox { Padding = new Thickness(6, 4, 6, 4), FontSize = 15, FontWeight = FontWeights.Bold };
        panel.Children.Add(_txtMontoCierre);

        panel.Children.Add(new TextBlock { Text = "Observaciones:", Margin = new Thickness(0, 12, 0, 4) });
        _txtObs = new TextBox { Height = 60, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Padding = new Thickness(4) };
        panel.Children.Add(_txtObs);

        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var btnCerrar = new Button { Content = "🔒 Cerrar Caja", Width = 120, Height = 34, Margin = new Thickness(0, 0, 8, 0),
                                     Background = System.Windows.Media.Brushes.DarkRed,
                                     Foreground = System.Windows.Media.Brushes.White };
        btnCerrar.Click += OnCerrarCaja;
        var btnCancelar = new Button { Content = "Cancelar", Width = 80, Height = 34 };
        btnCancelar.Click += (_, _) => Close();
        btnPanel.Children.Add(btnCerrar);
        btnPanel.Children.Add(btnCancelar);
        panel.Children.Add(btnPanel);
        Content = panel;
    }

    private async void OnCerrarCaja(object s, RoutedEventArgs e)
    {
        if (!decimal.TryParse(_txtMontoCierre.Text.Replace(",", "").Replace(".", ""), out var monto))
        { MessageBox.Show("Ingrese el monto contado.", "Error"); return; }
        if (MessageBox.Show("¿Confirmar cierre de caja?", "Confirmar",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        await _caja.CerrarCajaAsync(_idMaster, _session.UsuarioActual!.IdUsuario, monto, _txtObs.Text);
        MessageBox.Show("Caja cerrada correctamente.", "OK");
        Close();
    }
}

// ── Arqueo de Caja ────────────────────────────────────────────────────────────

public class CajaArqueoWindow : Window
{
    public CajaArqueoWindow()
    {
        Title = "Arqueo de Caja";
        Width = 500; Height = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        var btn = new Button { Content = "Cerrar", Width = 80, Height = 32, Margin = new Thickness(16) };
        btn.Click += (_, _) => Close();
        Content = new StackPanel
        {
            Children = {
                new TextBlock { Text = "Arqueo de Caja — en desarrollo", FontSize = 14, Margin = new Thickness(24, 24, 24, 12) },
                btn
            }
        };
    }
}

// ── Registrar movimiento de caja ─────────────────────────────────────────────

public class CajaRegistrarWindow : Window
{
    public CajaRegistrarWindow()
    {
        Title = "Registrar Movimiento de Caja";
        Width = 460; Height = 340;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        var btn = new Button { Content = "Cerrar", Width = 80, Height = 32, Margin = new Thickness(16) };
        btn.Click += (_, _) => Close();
        Content = new StackPanel
        {
            Children = {
                new TextBlock { Text = "Registrar movimiento — en desarrollo", FontSize = 14, Margin = new Thickness(24, 24, 24, 12) },
                btn
            }
        };
    }
}
