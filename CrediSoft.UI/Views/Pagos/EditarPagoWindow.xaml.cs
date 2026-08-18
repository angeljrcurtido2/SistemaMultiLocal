using CrediSoft.Data.Repositories;
using CrediSoft.UI.Views.Shared;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace CrediSoft.UI.Views.Pagos;

public partial class EditarPagoWindow : Window
{
    private readonly IPagoRepository   _pagos;
    private FuncionarioInfo?           _funcionario;
    private HistorialPagoItem?         _registro;
    private bool                       _calculando;
    private List<FuncionarioInfo>      _todosFuncionarios = new();

    public EditarPagoWindow()
    {
        InitializeComponent();
        _pagos = App.Services.GetRequiredService<IPagoRepository>();

        var hoy = DateTime.Today;
        DpDesde.SelectedDate = new DateTime(hoy.Year, hoy.Month, 1);
        DpHasta.SelectedDate = hoy;

        LimpiarFormulario();
        Loaded += async (_, _) =>
        {
            _todosFuncionarios = (await _pagos.ListarFuncionariosAsync()).ToList();
            RegistrarFormatoMiles(
                TxtSalario, TxtPlus, TxtHorasExtras, TxtBonificacion, TxtOtrasComisiones,
                TxtIps, TxtCuotas, TxtMultas, TxtOtros, TxtEquis);
        };
    }

    private void RegistrarFormatoMiles(params TextBox[] campos)
    {
        foreach (var tb in campos)
        {
            tb.GotFocus  += OnMontoGotFocus;
            tb.LostFocus += OnMontoLostFocus;
        }
    }

    // ── BÚSQUEDA ──────────────────────────────────
    private void OnCiKeyDown(object s, KeyEventArgs e) { if (e.Key == Key.Enter) OnBuscar(s, new RoutedEventArgs()); }

    private async void OnBuscar(object s, RoutedEventArgs e)
    {
        var ci = TxtCi.Text.Trim();
        if (string.IsNullOrEmpty(ci)) return;

        _funcionario = await _pagos.BuscarFuncionarioPorCiAsync(ci);
        if (_funcionario == null)
        {
            MessageBox.Show("Funcionario no encontrado.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        await CargarHistorial();
    }

    private async void OnListar(object s, RoutedEventArgs e)
    {
        if (_todosFuncionarios.Count == 0)
            _todosFuncionarios = (await _pagos.ListarFuncionariosAsync()).ToList();

        var modal = new SeleccionarFuncionarioModal(_todosFuncionarios, _funcionario) { Owner = this };
        if (modal.ShowDialog() != true || modal.FuncionarioSeleccionado == null) return;

        _funcionario  = modal.FuncionarioSeleccionado;
        TxtCi.Text    = _funcionario.Ci;
        await CargarHistorial();
    }

    private async void OnFiltroChanged(object s, SelectionChangedEventArgs e)
    {
        if (_funcionario != null) await CargarHistorial();
    }

    private async Task CargarHistorial()
    {
        if (_funcionario == null || DpDesde.SelectedDate == null || DpHasta.SelectedDate == null) return;

        var items = (await _pagos.ObtenerHistorialPagosAsync(
            _funcionario.IdUsuario,
            DpDesde.SelectedDate.Value,
            DpHasta.SelectedDate.Value)).ToList();

        GridHistorial.ItemsSource = items;
        TxtContadorHistorial.Text = $"{items.Count} registro(s)";
        LimpiarFormulario();
    }

    // ── SELECCIÓN EN GRILLA ────────────────────────
    private void OnHistorialSeleccionado(object s, SelectionChangedEventArgs e)
    {
        if (GridHistorial.SelectedItem is not HistorialPagoItem item) return;
        CargarRegistroEnFormulario(item);
    }

    private void CargarRegistroEnFormulario(HistorialPagoItem item)
    {
        _registro   = item;
        _calculando = true;

        TxtSalario.Text          = item.Salario.ToString("N", _fmtGs);
        TxtPorcVenta.Text        = item.PorcVenta.ToString("0.##");
        TxtTotalVenta.Text       = item.Venta.ToString("N", _fmtGs);
        TxtPorcCobranza.Text     = item.PorcCobranza.ToString("0.##");
        TxtTotalCobranza.Text    = item.Cobranza.ToString("N", _fmtGs);
        TxtPlus.Text             = item.Plus.ToString("N", _fmtGs);
        TxtHorasExtras.Text      = item.HorasExtras.ToString("N", _fmtGs);
        TxtBonificacion.Text     = item.Bonificacion.ToString("N", _fmtGs);
        TxtOtrasComisiones.Text  = item.OtrasComisiones.ToString("N", _fmtGs);
        TxtAusencias.Text        = item.Ausencias.ToString("N", _fmtGs);
        TxtAdelantos.Text        = item.Adelantos.ToString("N", _fmtGs);
        TxtIps.Text              = item.Ips.ToString("N", _fmtGs);
        TxtCuotas.Text           = item.Cuotas.ToString("N", _fmtGs);
        TxtMultas.Text           = item.Multas.ToString("N", _fmtGs);
        TxtOtros.Text            = item.Otros.ToString("N", _fmtGs);
        TxtEquis.Text            = item.Equis.ToString("N", _fmtGs);
        TxtNotaAsignacion.Text   = item.NotaAsignacion;
        TxtNotaEgreso.Text       = item.NotaEgreso;

        _calculando = false;
        RecalcularTotales();
        BtnGuardar.IsEnabled = true;
    }

    // ── RECÁLCULO ─────────────────────────────────
    private void OnCampoChanged(object s, TextChangedEventArgs e) { if (!_calculando) RecalcularTotales(); }

    private static readonly System.Globalization.NumberFormatInfo _fmtGs = new()
    {
        NumberGroupSeparator = ".",
        NumberDecimalSeparator = ",",
        NumberDecimalDigits = 0
    };

    private void OnMontoGotFocus(object s, RoutedEventArgs e)
    {
        if (s is not TextBox tb) return;
        var val = ParseGs(tb.Text);
        tb.Text = val == 0 ? "" : val.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        tb.SelectAll();
    }

    private void OnMontoLostFocus(object s, RoutedEventArgs e)
    {
        if (s is not TextBox tb) return;
        var val = ParseGs(tb.Text);
        _calculando = true;
        tb.Text = val.ToString("N", _fmtGs);
        _calculando = false;
        RecalcularTotales();
    }
    private void OnNotaAsigChanged(object s, TextChangedEventArgs e)
        => TxtContadorAsig.Text = $"{TxtNotaAsignacion.Text.Length} / 150";
    private void OnNotaEgresoChanged(object s, TextChangedEventArgs e)
        => TxtContadorEgreso.Text = $"{TxtNotaEgreso.Text.Length} / 150";

    private void RecalcularTotales()
    {
        if (_calculando) return;
        _calculando = true;

        var ventaBase = ParseGs(TxtTotalVenta.Text);
        var cobBase   = ParseGs(TxtTotalCobranza.Text);
        var pV = ParseGs(TxtPorcVenta.Text);
        var pC = ParseGs(TxtPorcCobranza.Text);

        var comVenta    = Math.Round(ventaBase * pV / 100m, 0);
        var comCobranza = Math.Round(cobBase   * pC / 100m, 0);

        var totalIngresos = ParseGs(TxtSalario.Text) + comVenta + comCobranza
                          + ParseGs(TxtPlus.Text) + ParseGs(TxtHorasExtras.Text)
                          + ParseGs(TxtBonificacion.Text) + ParseGs(TxtOtrasComisiones.Text);

        var totalEgresos  = ParseGs(TxtAusencias.Text) + ParseGs(TxtAdelantos.Text)
                          + ParseGs(TxtIps.Text) + ParseGs(TxtCuotas.Text)
                          + ParseGs(TxtMultas.Text) + ParseGs(TxtOtros.Text)
                          + ParseGs(TxtEquis.Text);

        var neto = totalIngresos - totalEgresos;

        TxtTotalIngresos.Text = totalIngresos.ToString("N", _fmtGs);
        TxtTotalEgresos.Text  = totalEgresos.ToString("N", _fmtGs);
        TxtNeto.Text          = neto.ToString("N", _fmtGs);
        TxtNeto.Foreground    = new System.Windows.Media.SolidColorBrush(
            neto >= 0 ? System.Windows.Media.Color.FromRgb(0x2E, 0x7D, 0x32)
                      : System.Windows.Media.Color.FromRgb(0xC6, 0x28, 0x28));

        _calculando = false;
    }

    // ── GUARDAR ───────────────────────────────────
    private async void OnGuardar(object s, RoutedEventArgs e)
    {
        if (_registro == null || _funcionario == null) return;

        var auth = new AutorizacionAdminModal(
            $"Modificar pago #{_registro.IdHpf} — {_funcionario.Nombre}") { Owner = this };
        if (auth.ShowDialog() != true) return;

        // Advertencia: la edición no corrige el egreso de caja
        if (!MostrarAdvertenciaCaja(_registro.IdHpf, _funcionario.Nombre)) return;

        var neto = ParseGs(TxtNeto.Text);
        var ventaBase = ParseGs(TxtTotalVenta.Text);
        var cobBase   = ParseGs(TxtTotalCobranza.Text);
        var pV = ParseGs(TxtPorcVenta.Text);
        var pC = ParseGs(TxtPorcCobranza.Text);

        var prm = new GuardarPagoParams(
            IdGps:           0,
            IdUsuario:       _funcionario.IdUsuario,
            IdLocal:         (byte)_funcionario.IdLocal,
            Salario:         ParseGs(TxtSalario.Text),
            Venta:           ventaBase,
            PorcVenta:       pV,
            TotalVenta:      Math.Round(ventaBase * pV / 100m, 0),
            Cobranza:        cobBase,
            PorcCobranza:    pC,
            TotalCobranza:   Math.Round(cobBase * pC / 100m, 0),
            Plus:            ParseGs(TxtPlus.Text),
            HorasExtras:     ParseGs(TxtHorasExtras.Text),
            Bonificacion:    ParseGs(TxtBonificacion.Text),
            OtrasComisiones: ParseGs(TxtOtrasComisiones.Text),
            TotalIngresos:   ParseGs(TxtTotalIngresos.Text),
            Ausencias:       ParseGs(TxtAusencias.Text),
            Adelantos:       ParseGs(TxtAdelantos.Text),
            Ips:             ParseGs(TxtIps.Text),
            Cuotas:          ParseGs(TxtCuotas.Text),
            Multas:          ParseGs(TxtMultas.Text),
            Otros:           ParseGs(TxtOtros.Text),
            Equis:           ParseGs(TxtEquis.Text),
            TotalEgresos:    ParseGs(TxtTotalEgresos.Text),
            Nombre:          "",  Fecha: DateTime.Now,
            NotaAsignacion:  TxtNotaAsignacion.Text.Trim(),
            NotaEgreso:      TxtNotaEgreso.Text.Trim(),
            FormaPago:       "", MontoCaja: 0,
            IdCajero:        0,  IdCajaFisica: 0, IdMaster: 0, Referencia: "");

        var ok = await _pagos.ActualizarPagoAsync(_registro.IdHpf, prm);
        if (ok)
        {
            MessageBox.Show($"Pago #{_registro.IdHpf} actualizado correctamente.",
                "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            await CargarHistorial();
        }
        else
        {
            MessageBox.Show("Error al actualizar el registro.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── HELPERS ───────────────────────────────────
    private void LimpiarFormulario()
    {
        _registro   = null;
        _calculando = true;
        foreach (var tb in new[] { TxtSalario, TxtPorcVenta, TxtTotalVenta, TxtPorcCobranza,
                                   TxtTotalCobranza, TxtPlus, TxtHorasExtras, TxtBonificacion,
                                   TxtOtrasComisiones, TxtTotalIngresos, TxtAusencias, TxtAdelantos,
                                   TxtIps, TxtCuotas, TxtMultas, TxtOtros, TxtEquis,
                                   TxtTotalEgresos, TxtNeto })
            tb.Text = "0";
        TxtNotaAsignacion.Text = "";
        TxtNotaEgreso.Text     = "";
        BtnGuardar.IsEnabled   = false;
        _calculando = false;
    }

    private static decimal ParseGs(string? txt)
    {
        if (string.IsNullOrWhiteSpace(txt)) return 0;
        if (txt.Contains(','))
        {
            var s = txt.Replace(".", "").Replace(",", ".");
            return decimal.TryParse(s, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
        }
        var limpio = System.Text.RegularExpressions.Regex.IsMatch(txt, @"\.\d{3}($|\s)")
            ? txt.Replace(".", "") : txt;
        return decimal.TryParse(limpio, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : 0;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.F5 && BtnGuardar.IsEnabled) OnGuardar(this, new RoutedEventArgs());
        if (e.Key == Key.Escape) Close();
    }

    private void OnCerrar(object s, RoutedEventArgs e) => Close();

    private bool MostrarAdvertenciaCaja(int idHpf, string nombreFuncionario)
    {
        var dlg = new AdvertenciaCajaDialog(idHpf, nombreFuncionario) { Owner = this };
        return dlg.ShowDialog() == true;
    }
}

// ── Dialog de advertencia ────────────────────────────────────────────────────
internal class AdvertenciaCajaDialog : Window
{
    private static SolidColorBrush B(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex));

    public AdvertenciaCajaDialog(int idHpf, string nombreFuncionario)
    {
        Title                 = "Advertencia — Edición de pago";
        Width                 = 520;
        SizeToContent         = SizeToContent.Height;
        ResizeMode            = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background            = B("#FFF8E1");
        FontFamily            = new FontFamily("Segoe UI");
        ShowInTaskbar         = false;

        var root = new DockPanel();

        // ── Header naranja de advertencia ────────────────────────────────
        var header = new Border { Background = B("#E65100"), Padding = new Thickness(22, 16, 22, 16) };
        var hRow   = new DockPanel();

        var icono = new Border {
            Width = 46, Height = 46, CornerRadius = new CornerRadius(23),
            Background = B("#BF360C"), Margin = new Thickness(0, 0, 16, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock {
                Text = "⚠", FontSize = 22,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center }
        };
        DockPanel.SetDock(icono, Dock.Left);
        hRow.Children.Add(icono);

        var hStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        hStack.Children.Add(new TextBlock {
            Text = "EDICIÓN NO SINCRONIZA CON CAJA",
            Foreground = Brushes.White, FontSize = 13, FontWeight = FontWeights.Bold });
        hStack.Children.Add(new TextBlock {
            Text = $"Pago #{idHpf}  ·  {nombreFuncionario}",
            Foreground = B("#FFCCBC"), FontSize = 11, Margin = new Thickness(0, 4, 0, 0) });
        hRow.Children.Add(hStack);
        header.Child = hRow;
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        // ── Footer ───────────────────────────────────────────────────────
        var footer = new Border {
            Background = Brushes.White, BorderBrush = B("#FFCC80"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(20, 14, 20, 14) };
        var footRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

        var btnCancelar = MakeBtn("✕  Cancelar", "#546E7A");
        btnCancelar.Width  = 130;
        btnCancelar.Click += (_, _) => { DialogResult = false; Close(); };

        var btnContinuar = MakeBtn("Entendido, continuar  →", "#E65100");
        btnContinuar.Width  = 200;
        btnContinuar.Margin = new Thickness(12, 0, 0, 0);
        btnContinuar.Click += (_, _) => { DialogResult = true; Close(); };

        footRow.Children.Add(btnCancelar);
        footRow.Children.Add(btnContinuar);
        footer.Child = footRow;
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        // ── Cuerpo ───────────────────────────────────────────────────────
        var body = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };

        // Qué pasa
        body.Children.Add(MakeSeccion("¿Qué ocurre al editar este pago?"));
        body.Children.Add(MakeParrafo(
            "Al guardar los cambios, se actualizarán los montos en el historial de pagos " +
            "(HISTORIALPAGOFUN). Sin embargo, el egreso que se registró en caja al momento " +
            "de confirmar el pago original NO será modificado automáticamente."));

        // Separador
        body.Children.Add(new Border { Height = 1, Background = B("#FFE0B2"), Margin = new Thickness(0, 14, 0, 14) });

        // Ejemplo concreto
        body.Children.Add(MakeSeccion("Ejemplo"));
        var cardEjemplo = new Border {
            Background = Brushes.White, BorderBrush = B("#FFCC80"),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12, 16, 12), Margin = new Thickness(0, 6, 0, 14),
            Effect = new DropShadowEffect { BlurRadius = 6, Opacity = 0.06, ShadowDepth = 1, Direction = 270, Color = Colors.Black }
        };
        cardEjemplo.Child = new TextBlock {
            Text =
                "Pago original confirmado → Gs. 2.000.000 egresó de caja.\n" +
                "Editás el pago → Neto queda en Gs. 1.800.000 en el historial.\n\n" +
                "La caja sigue mostrando el egreso de Gs. 2.000.000 (diferencia de Gs. 200.000).",
            Foreground = B("#4E342E"), FontSize = 12,
            TextWrapping = TextWrapping.Wrap, LineHeight = 22
        };
        body.Children.Add(cardEjemplo);

        // Qué hacer
        body.Children.Add(MakeSeccion("¿Qué debés hacer?"));
        body.Children.Add(MakeParrafo(
            "Si el neto cambió, registrá manualmente el ajuste en caja como egreso o ingreso " +
            "con concepto \"AJUSTE PAGO SALARIO — " + nombreFuncionario + "\" por la diferencia."));

        body.Children.Add(new Border { Height = 1, Background = B("#FFE0B2"), Margin = new Thickness(0, 14, 0, 14) });

        // Aviso final
        var avisoFinal = new Border {
            Background = B("#FFF3E0"), BorderBrush = B("#FFB74D"),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 10, 14, 10)
        };
        avisoFinal.Child = new TextBlock {
            Text = "Si solo corregís las notas o datos sin cambiar el neto, no es necesario ningún ajuste de caja.",
            Foreground = B("#E65100"), FontSize = 12, FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
        body.Children.Add(avisoFinal);

        root.Children.Add(new ScrollViewer {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 420
        });

        Content = root;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { DialogResult = false; Close(); } };
    }

    private static TextBlock MakeSeccion(string txt) => new() {
        Text = txt.ToUpperInvariant(),
        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E65100")),
        FontSize = 10, FontWeight = FontWeights.Bold,
        Margin = new Thickness(0, 0, 0, 6)
    };

    private static TextBlock MakeParrafo(string txt) => new() {
        Text = txt, FontSize = 12,
        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#37474F")),
        TextWrapping = TextWrapping.Wrap, LineHeight = 22,
        Margin = new Thickness(0, 0, 0, 8)
    };

    private static Button MakeBtn(string txt, string bg) => new() {
        Content = txt, Height = 40,
        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg)),
        Foreground = Brushes.White, BorderThickness = new Thickness(0),
        FontSize = 13, FontWeight = FontWeights.SemiBold,
        Cursor = System.Windows.Input.Cursors.Hand
    };
}
