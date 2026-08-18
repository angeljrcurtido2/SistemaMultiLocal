using CrediSoft.Core.Models;
using CrediSoft.Data;
using CrediSoft.Data.Repositories;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace CrediSoft.UI.Views.Maestros;

public partial class ClientesWindow : Window
{
    private readonly IClienteRepository   _repo;
    private readonly IDbConnectionFactory _db;
    private Cliente? _clienteSeleccionado;
    private bool _modoEdicion = false;
    private string _fotoRuta = string.Empty;

    // CI del último cliente registrado (ALTA, no edición) en esta ventana — quien abre
    // ClientesWindow buscando cargar una persona nueva desde otro flujo (ej. BuscadorPersonaWindow,
    // botón "+ Nuevo") lo lee al cerrarse para autoseleccionar a ese cliente sin que el usuario
    // tenga que volver a buscarlo a mano.
    public string? CiUltimoClienteGuardado { get; private set; }

    // Bytes en memoria de cada cara — se unifican al guardar
    private byte[]? _bytesFrontal = null;
    private byte[]? _bytesDorso   = null;

    private static readonly CultureInfo _cult = CultureInfo.GetCultureInfo("es-AR");

    public ClientesWindow()
    {
        InitializeComponent();
        _repo = App.Services.GetRequiredService<IClienteRepository>();
        _db   = App.Services.GetRequiredService<IDbConnectionFactory>();
        AjustarCredMaxNumerico();
        Loaded += async (_, _) => await CargarLocales();
    }

    // Aplica formato de separador de miles (es-AR: punto=miles) al TextBox TxtCredMax
    private void AjustarCredMaxNumerico()
    {
        bool upd = false;

        TxtCredMax.PreviewTextInput += (_, e) =>
        {
            e.Handled = !char.IsDigit(e.Text[0]);
        };

        DataObject.AddPastingHandler(TxtCredMax, (_, e) =>
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                var p = (string)e.DataObject.GetData(DataFormats.Text);
                if (!p.All(char.IsDigit)) e.CancelCommand();
            }
            else e.CancelCommand();
        });

        TxtCredMax.TextChanged += (_, __) =>
        {
            if (upd) return;
            upd = true;

            var caretPos = TxtCredMax.CaretIndex;
            var digitsBeforeCaret = TxtCredMax.Text.Take(caretPos).Count(char.IsDigit);

            var raw = TxtCredMax.Text.Replace(".", "").TrimStart('0');
            if (raw == "") raw = "0";
            var formatted = long.TryParse(raw, out var n)
                ? n.ToString("#,0", _cult)
                : raw;

            TxtCredMax.Text = formatted;

            var newCaret = 0;
            var digits = 0;
            foreach (var c in formatted)
            {
                if (digits == digitsBeforeCaret) break;
                if (char.IsDigit(c)) digits++;
                newCaret++;
            }
            TxtCredMax.CaretIndex = Math.Min(newCaret, formatted.Length);

            upd = false;
        };

        TxtCredMax.LostFocus += (_, __) =>
        {
            if (string.IsNullOrWhiteSpace(TxtCredMax.Text)) TxtCredMax.Text = "0";
        };
    }

    private async Task CargarLocales()
    {
        var locales = await _repo.ObtenerLocalesAsync();
        CboLocal.ItemsSource = locales.Select(l => new { l.Id, l.Nombre }).ToList();
    }

    private async void OnBuscar(object s, RoutedEventArgs e)
    {
        var ci = TxtBuscar.Text.Trim();
        if (ci.Length < 6) {
            MessageBox.Show("Ingrese al menos 6 dígitos de CI para realizar la búsqueda exacta.\nPara buscar por nombre use el botón \"Ver Listado de Clientes\".",
                "Búsqueda por CI", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var c = await _repo.BuscarPorCiAsync(ci);
        if (c == null) { MessageBox.Show($"No se encontró cliente con CI: {ci}", "Buscar", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        CargarClienteEnForm(c);
        TxtClienteSeleccionadoBadge.Text = "👤 " + c.NombreCliente;
        BadgeCliente.Visibility = Visibility.Visible;
    }
    private void OnBuscarKeyDown(object s, KeyEventArgs e) { if (e.Key == Key.Enter) OnBuscar(s, new RoutedEventArgs()); }
    private void OnCiSoloNumeros(object s, System.Windows.Input.TextCompositionEventArgs e)
        => e.Handled = !e.Text.All(char.IsDigit);
    private void OnCiPegado(object s, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            var txt = (string)e.DataObject.GetData(typeof(string));
            if (!txt.All(char.IsDigit)) e.CancelCommand();
        }
        else e.CancelCommand();
    }

    private async void OnAbrirBuscador(object s, RoutedEventArgs e)
    {
        var modal = new BuscadorClienteModal(_repo) { Owner = this };
        if (modal.ShowDialog() != true || modal.ClienteSeleccionado == null) return;
        CargarClienteEnForm(modal.ClienteSeleccionado);
        // Mostrar badge con nombre del cliente seleccionado
        TxtClienteSeleccionadoBadge.Text = "👤 " + modal.ClienteSeleccionado.NombreCliente;
        BadgeCliente.Visibility = Visibility.Visible;
    }

    private void OnGridSelectionChanged(object s, System.Windows.Controls.SelectionChangedEventArgs e) { }

    private async void CargarClienteEnForm(Cliente c)
    {
        var completo = await _repo.BuscarPorIdAsync(c.IdCliente);
        if (completo == null) return;
        _clienteSeleccionado  = completo;
        BtnEditar.IsEnabled      = true;
        BtnEliminar.IsEnabled    = true;
        BtnVistaPrevia.IsEnabled = true;
        BtnImprimirFicha.IsEnabled = true;

        TxtRuc.Text = completo.RucCliente;
        TxtCi.Text = completo.CiCliente;
        TxtNombre.Text = completo.NombreCliente;
        TxtDireccion.Text = completo.DireccionCliente;
        TxtTelefono.Text = completo.TelefonoCliente;
        TxtCiudad.Text = completo.CiudadCliente;
        TxtZona.Text = completo.ZonaCliente;
        TxtRefGeo.Text = completo.ReferenciaGeo;
        TxtEmail.Text = completo.EmailCliente;
        TxtCredMax.Text = completo.CredMax.ToString("N0");
        TxtConyuge.Text = completo.Conyuge;
        TxtCiConyuge.Text = completo.CiConyuge;
        TxtEmpresa.Text = completo.EmpresaLaboral;
        TxtTelLab.Text = completo.TelefonoLaboral;
        TxtDirLab.Text = completo.DireccionLaboral;
        TxtAntiguedad.Text = completo.Antiguedad;
        CboEstado.SelectedIndex = completo.Estado;
        CboSexo.SelectedIndex = completo.Sexo;
        CboEcv.SelectedIndex = completo.Ecv;
        CboTipo.SelectedIndex = completo.Tipo;
        CboCondicion.SelectedIndex = completo.Condicion;
        CboInforcom.SelectedIndex = completo.Inforcom;
        CboLocal.SelectedValue = completo.Local;
        DpVencCi.SelectedDate = completo.VencCedula;
        MostrarPanelFoto(esNuevo: false);
        await CargarFotoDesdeDb(completo.CiCliente, completo.IdCliente);
    }

    private void HabilitarEdicion(bool habilitar)
    {
        _modoEdicion = habilitar;
        foreach (var control in new System.Windows.FrameworkElement[]
            { TxtRuc, TxtCi, TxtNombre, TxtDireccion, TxtTelefono, TxtCiudad, TxtZona,
              TxtRefGeo, TxtEmail, TxtCredMax, TxtConyuge, TxtCiConyuge,
              TxtEmpresa, TxtTelLab, TxtDirLab, TxtAntiguedad })
        {
            if (control is System.Windows.Controls.TextBox tb) tb.IsReadOnly = !habilitar;
        }
        CboEstado.IsEnabled = habilitar;
        CboSexo.IsEnabled = habilitar;
        CboEcv.IsEnabled = habilitar;
        CboTipo.IsEnabled = habilitar;
        CboCondicion.IsEnabled = habilitar;
        CboInforcom.IsEnabled = habilitar;
        CboLocal.IsEnabled = habilitar;
        DpVencCi.IsEnabled = habilitar;

        // Slots duales solo en modo Nuevo (sin cliente seleccionado)
        bool esNuevo = _clienteSeleccionado == null;
        BtnSelFotoFrontal.IsEnabled    = habilitar && esNuevo;
        BtnSelFotoDorso.IsEnabled      = habilitar && esNuevo;
        BtnQuitarFotoFrontal.IsEnabled = habilitar && esNuevo && _bytesFrontal != null;
        BtnQuitarFotoDorso.IsEnabled   = habilitar && esNuevo && _bytesDorso   != null;

        // Botones del slot unificado solo en modo Editar (con cliente existente)
        BtnCambiarFotoDoc.IsEnabled = habilitar && !esNuevo;
        BtnQuitarFotoDoc.IsEnabled  = habilitar && !esNuevo && ImgFotoDoc.Source != null;

        BtnGuardar.IsEnabled = habilitar;
    }

    private void MostrarPanelFoto(bool esNuevo)
    {
        PanelSlotsNuevo.Visibility = esNuevo ? Visibility.Visible : Visibility.Collapsed;
        PanelFotoUnica.Visibility  = esNuevo ? Visibility.Collapsed : Visibility.Visible;
    }

    private void LimpiarForm()
    {
        TxtRuc.Text = TxtCi.Text = TxtNombre.Text = TxtDireccion.Text = "";
        TxtTelefono.Text = TxtCiudad.Text = TxtZona.Text = TxtRefGeo.Text = "";
        TxtEmail.Text = TxtCredMax.Text = TxtConyuge.Text = TxtCiConyuge.Text = "";
        TxtEmpresa.Text = TxtTelLab.Text = TxtDirLab.Text = TxtAntiguedad.Text = "";
        CboEstado.SelectedIndex = -1; CboSexo.SelectedIndex = -1;
        CboEcv.SelectedIndex = -1; CboTipo.SelectedIndex = -1;
        CboCondicion.SelectedIndex = -1; CboInforcom.SelectedIndex = -1;
        CboLocal.SelectedIndex = -1;
        DpVencCi.SelectedDate = null;
        _clienteSeleccionado       = null;
        BtnVistaPrevia.IsEnabled   = false;
        BtnImprimirFicha.IsEnabled = false;
        LimpiarErrores();
        CargarFoto(string.Empty);
    }

    private void OnNuevo(object s, RoutedEventArgs e)
    {
        LimpiarForm();
        MostrarPanelFoto(esNuevo: true);
        HabilitarEdicion(true);
        TxtCi.Focus();
    }

    private void OnEditar(object s, RoutedEventArgs e)
    {
        if (_clienteSeleccionado == null) return;
        HabilitarEdicion(true);
        TxtNombre.Focus();
    }

    private async void OnVistaPrevia(object s, RoutedEventArgs e)
    {
        var data = await BuildFichaDataAsync();
        if (data == null) return;
        var w = new ClienteFichaPreviewWindow(data) { Owner = this };
        w.ShowDialog();
    }

    private async void OnImprimirFicha(object s, RoutedEventArgs e)
    {
        var data = await BuildFichaDataAsync();
        if (data == null) return;
        ClienteFichaImpresora.Imprimir(data, this);
    }

    private async Task<ClienteFichaData?> BuildFichaDataAsync()
    {
        if (_clienteSeleccionado == null)
        {
            MessageBox.Show("Seleccione un cliente primero.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }
        var c = _clienteSeleccionado;

        // Nombre del local
        string localNom = "—";
        if (c.Local > 0)
        {
            var locales = await _repo.ObtenerLocalesAsync();
            var found = locales.FirstOrDefault(l => l.Id == c.Local);
            if (found.Id > 0) localNom = found.Nombre;
        }

        // Foto de la cédula desde BD
        System.Drawing.Image? fotoDoc = null;
        try
        {
            using var conn = _db.Create();
            var datos = await conn.QueryFirstOrDefaultAsync<byte[]>(
                "SELECT TOP 1 DATOS FROM FOTOS WHERE CI = @ci OR IDCLIE = @id ORDER BY IDFOTO DESC",
                new { ci = c.CiCliente, id = c.IdCliente });
            if (datos != null && datos.Length > 0)
                fotoDoc = System.Drawing.Image.FromStream(new System.IO.MemoryStream(datos));
        }
        catch { /* si falla la foto, se omite */ }

        return new ClienteFichaData
        {
            Cliente  = c,
            LocalNom = localNom,
            LogoPath = ArticulosPagina.ResolverLogoPath(),
            Usuario  = CrediSoft.Core.Services.SessionService.Instance.UsuarioActual?.NombreUsuario ?? "",
            FotoDoc  = fotoDoc,
        };
    }

    private async void OnEliminar(object s, RoutedEventArgs e)
    {
        if (_clienteSeleccionado == null) return;
        if (MessageBox.Show($"¿Eliminar cliente '{_clienteSeleccionado.NombreCliente}'?",
            "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        await _repo.EliminarAsync(_clienteSeleccionado.IdCliente);
        LimpiarForm();
        BadgeCliente.Visibility = Visibility.Collapsed;
    }
    private void OnGuardarClick(object s, RoutedEventArgs e) => OnGuardar();

    private static readonly System.Windows.Media.SolidColorBrush _borderNormal =
        new(System.Windows.Media.Color.FromRgb(0xD1, 0xD5, 0xDB));

    private void LimpiarErrores()
    {
        ErrCi.Visibility        = Visibility.Collapsed;
        ErrNombre.Visibility    = Visibility.Collapsed;
        ErrDireccion.Visibility = Visibility.Collapsed;
        ErrTelefono.Visibility  = Visibility.Collapsed;
        ErrCiudad.Visibility    = Visibility.Collapsed;
        TxtCi.BorderBrush        = _borderNormal;
        TxtNombre.BorderBrush    = _borderNormal;
        TxtDireccion.BorderBrush = _borderNormal;
        TxtTelefono.BorderBrush  = _borderNormal;
        TxtCiudad.BorderBrush    = _borderNormal;
    }

    private void MostrarError(TextBlock err, System.Windows.Controls.TextBox campo, string msg)
    {
        err.Text = msg;
        err.Visibility = Visibility.Visible;
        campo.BorderBrush = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44));
    }

    private async void OnGuardar()
    {
        if (!_modoEdicion) return;

        // ── Validaciones inline ──────────────────────────────────────────────
        LimpiarErrores();
        bool hayError = false;
        System.Windows.Controls.TextBox? primerCampo = null;

        void Validar(bool falla, TextBlock err, System.Windows.Controls.TextBox campo, string msg)
        {
            if (!falla) return;
            MostrarError(err, campo, msg);
            primerCampo ??= campo;
            hayError = true;
        }

        Validar(TxtCi.Text.Trim().Length < 5,
            ErrCi, TxtCi, "CI obligatorio — mínimo 5 dígitos.");
        Validar(string.IsNullOrWhiteSpace(TxtNombre.Text),
            ErrNombre, TxtNombre, "Nombre / Razón Social es obligatorio.");
        Validar(string.IsNullOrWhiteSpace(TxtDireccion.Text),
            ErrDireccion, TxtDireccion, "Dirección es obligatoria.");
        Validar(string.IsNullOrWhiteSpace(TxtTelefono.Text),
            ErrTelefono, TxtTelefono, "Teléfono es obligatorio.");
        Validar(string.IsNullOrWhiteSpace(TxtCiudad.Text),
            ErrCiudad, TxtCiudad, "Ciudad es obligatoria.");

        if (hayError) { primerCampo!.Focus(); primerCampo.SelectAll(); return; }

        var email = TxtEmail.Text.Trim();
        if (!string.IsNullOrEmpty(email) && !System.Text.RegularExpressions.Regex.IsMatch(email,
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            MessageBox.Show("El formato del e-mail no es válido.\nEjemplo: nombre@dominio.com", "Validación",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtEmail.Focus();
            return;
        }
        var c = BuildClienteDesdeForm();
        if (_clienteSeleccionado?.IdCliente > 0)
        {
            // Aviso al cambiar el estado de Informconf: este flag determina si el cajero
            // ve la opcion de aplicar el cargo de gestion (Gs. 100.000) al cobrar cuotas de
            // este cliente. Como no hay una regla automatica que lo dispare (confirmado con
            // evidencia historica), quien edita el cliente debe saber el efecto del cambio.
            var inforcomCambio = _clienteSeleccionado.Inforcom != c.Inforcom;
            if (inforcomCambio)
            {
                using var conn = _db.Create();
                var valorInforconf = await conn.ExecuteScalarAsync<decimal?>(
                    "SELECT TOP 1 VALOR_INFORCONF FROM CONFIGURACION") ?? 0;

                var dlgInforcom = new CambioInforconfDialog(c.NombreCliente, pasaARegistrado: c.Inforcom == 1, valorInforconf)
                    { Owner = this };
                dlgInforcom.ShowDialog();
                if (!dlgInforcom.Confirmado) return;
            }

            c.IdCliente = _clienteSeleccionado.IdCliente;
            try
            {
                await _repo.ActualizarAsync(c);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Error al actualizar", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            await GuardarFotoUnificadaAsync(c.CiCliente, c.IdCliente);
            MessageBox.Show("Cliente actualizado correctamente.", "OK");
        }
        else
        {
            if (!ConfirmarCrearCliente(c)) return;
            try
            {
                await _repo.GuardarAsync(c);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("EXISTE CI"))
            {
                MostrarError(ErrCi, TxtCi, $"Ya existe un cliente con CI '{c.CiCliente}'.");
                TxtCi.Focus(); TxtCi.SelectAll();
                return;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("EXISTE RUC"))
            {
                MessageBox.Show($"Ya existe un cliente registrado con el RUC '{c.RucCliente}'.",
                    "RUC duplicado", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtRuc.Focus(); TxtRuc.SelectAll();
                return;
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Error al guardar", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // Buscar el ID recién generado para guardar la foto
            var nuevo = await _repo.BuscarPorCiAsync(c.CiCliente);
            if (nuevo != null)
                await GuardarFotoUnificadaAsync(c.CiCliente, nuevo.IdCliente);
            CiUltimoClienteGuardado = c.CiCliente;
            MessageBox.Show("Cliente registrado correctamente.", "OK");
        }
        HabilitarEdicion(false);
        BadgeCliente.Visibility = Visibility.Collapsed;
    }

    private bool ConfirmarCrearCliente(Cliente c)
    {
        // Resolver textos de combos para mostrar en el resumen
        string TextoCbo(ComboBox cbo) =>
            (cbo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "—";
        string localNombre = (CboLocal.SelectedItem as dynamic)?.Nombre ?? "—";

        var secciones = new[]
        {
            ("DATOS PERSONALES", new (string Label, string Valor)[]
            {
                ("CI",                c.CiCliente),
                ("RUC",               c.RucCliente),
                ("Nombre",            c.NombreCliente),
                ("Dirección",         c.DireccionCliente),
                ("Teléfono",          c.TelefonoCliente),
                ("Ciudad",            c.CiudadCliente),
                ("Zona",              c.ZonaCliente),
                ("Ref. geográfica",   c.ReferenciaGeo),
                ("e-mail",            c.EmailCliente),
                ("Crédito Máx.",      c.CredMax == 0 ? "0" : c.CredMax.ToString("N0", _cult) + " Gs."),
                ("Estado",            TextoCbo(CboEstado)),
                ("Sexo",              TextoCbo(CboSexo)),
                ("Estado Civil",      TextoCbo(CboEcv)),
                ("Cónyuge",           c.Conyuge),
                ("CI Cónyuge",        c.CiConyuge),
                ("Venc. CI",          c.VencCedula.HasValue ? c.VencCedula.Value.ToString("dd/MM/yyyy") : ""),
            }),
            ("DATOS LABORALES", new (string Label, string Valor)[]
            {
                ("Empresa",           c.EmpresaLaboral),
                ("Tel. laboral",      c.TelefonoLaboral),
                ("Dir. laboral",      c.DireccionLaboral),
                ("Antigüedad",        c.Antiguedad),
            }),
            ("DATOS ADICIONALES", new (string Label, string Valor)[]
            {
                ("Tipo de cliente",   TextoCbo(CboTipo)),
                ("Condición",         TextoCbo(CboCondicion)),
                ("InforcomConf",      TextoCbo(CboInforcom)),
                ("Local",             localNombre),
            }),
        };

        var resultado = false;

        // MaxHeight fijo (620) no consideraba resoluciones chicas (ej. 1366x768): con
        // ResizeMode.NoResize y sin SizeToContent, WPF mide el alto natural de todo el
        // contenido (header + scroll interno de hasta 460px + separador + botones) y, si esa
        // suma supera el alto real de la pantalla, los botones "Sí, crear cliente"/"Cancelar"
        // quedaban empujados fuera del área visible sin ninguna forma de scrollear hasta
        // ellos — el único ScrollViewer era el del cuerpo, no el de la ventana entera. Ahora
        // el límite se calcula contra el alto de pantalla real, dejando margen para la barra
        // de tareas y el propio header/botones.
        var maxAltoDisponible = System.Windows.SystemParameters.WorkArea.Height - 40;
        var dlg = new Window
        {
            Title = "Confirmar registro de cliente",
            Width = 480,
            MaxHeight = Math.Min(620, maxAltoDisponible),
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            Background = System.Windows.Media.Brushes.White,
        };

        // ── Encabezado ───────────────────────────────────────────────────────
        var header = new Border
        {
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xE0, 0x70, 0x00)),
            Padding = new Thickness(16, 12, 16, 12),
        };
        header.Child = new TextBlock
        {
            Text = "¿Estás seguro de crear este cliente?",
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        };

        // ── Cuerpo con scroll ────────────────────────────────────────────────
        var body = new StackPanel { Margin = new Thickness(16, 10, 16, 4) };

        foreach (var (titulo, campos) in secciones)
        {
            var filas = campos.Where(f => !string.IsNullOrWhiteSpace(f.Valor)).ToArray();
            if (filas.Length == 0) continue;

            // Subtítulo de sección
            body.Children.Add(new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xCC, 0x66, 0x00)),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(-16, 8, -16, 6),
                Child = new TextBlock
                {
                    Text = titulo,
                    Foreground = System.Windows.Media.Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 11,
                }
            });

            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            foreach (var (label, valor) in filas)
            {
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var row = g.RowDefinitions.Count - 1;

                var lbl = new TextBlock
                {
                    Text = label,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x80, 0x40, 0x00)),
                    Margin = new Thickness(0, 3, 8, 3),
                    VerticalAlignment = VerticalAlignment.Top,
                };
                Grid.SetRow(lbl, row); Grid.SetColumn(lbl, 0);

                var val = new TextBlock
                {
                    Text = valor,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 3, 0, 3),
                };
                Grid.SetRow(val, row); Grid.SetColumn(val, 1);

                g.Children.Add(lbl);
                g.Children.Add(val);
            }

            body.Children.Add(g);
        }

        // 220 = espacio estimado para header + separador + fila de botones + márgenes, para
        // que la ventana completa (header + este scroll + botones) nunca exceda el alto real
        // de la ventana y los botones queden siempre visibles, sin importar la resolución.
        // Bug real reportado 2026-08-04: este MaxHeight se calculaba contra maxAltoDisponible
        // (el alto de TODA la pantalla), pero la ventana en sí ya está topada en
        // dlg.MaxHeight = Math.Min(620, maxAltoDisponible) — en pantallas grandes,
        // maxAltoDisponible podía ser mucho mayor a 620, así que el scroll se creía con más
        // espacio del que la ventana realmente tenía, y los botones (fuera del scroll, pero
        // dentro de la ventana ya topada en 620) quedaban recortados fuera del área visible.
        var altoVentanaReal = Math.Min(620, maxAltoDisponible);
        var scroll = new ScrollViewer
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = Math.Max(200, altoVentanaReal - 220),
        };

        // ── Separador + botones ──────────────────────────────────────────────
        var sep = new Border
        {
            Height = 1,
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xDD, 0xDD, 0xDD)),
            Margin = new Thickness(0, 6, 0, 0),
        };

        var btnSi = new Button
        {
            Content = "Sí, crear cliente",
            Width = 130, Height = 34,
            Margin = new Thickness(0, 0, 8, 0),
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x27, 0xAE, 0x60)),
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        var btnNo = new Button
        {
            Content = "Cancelar",
            Width = 90, Height = 34,
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x95, 0x95, 0x95)),
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
        };

        btnSi.Click += (_, __) => { resultado = true; dlg.Close(); };
        btnNo.Click += (_, __) => dlg.Close();

        var botones = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(16, 10, 16, 16),
        };
        botones.Children.Add(btnSi);
        botones.Children.Add(btnNo);

        var root = new StackPanel();
        root.Children.Add(header);
        root.Children.Add(scroll);
        root.Children.Add(sep);
        root.Children.Add(botones);

        dlg.Content = root;
        dlg.ShowDialog();
        return resultado;
    }

    private Cliente BuildClienteDesdeForm()
    {
        return new Cliente
        {
            RucCliente = TxtRuc.Text.Trim(),
            CiCliente = TxtCi.Text.Trim(),
            NombreCliente = TxtNombre.Text.Trim(),
            DireccionCliente = TxtDireccion.Text.Trim(),
            TelefonoCliente = TxtTelefono.Text.Trim(),
            CiudadCliente = TxtCiudad.Text.Trim(),
            ZonaCliente = TxtZona.Text.Trim(),
            ReferenciaGeo = TxtRefGeo.Text.Trim(),
            EmailCliente = TxtEmail.Text.Trim(),
            CredMax = decimal.TryParse(TxtCredMax.Text, NumberStyles.Number, _cult, out var cm) ? cm : 0,
            Estado = (byte)Math.Max(0, CboEstado.SelectedIndex),
            Sexo = (byte)Math.Max(0, CboSexo.SelectedIndex),
            Ecv = (byte)Math.Max(0, CboEcv.SelectedIndex),
            Tipo = (byte)Math.Max(0, CboTipo.SelectedIndex),
            Condicion = (byte)Math.Max(0, CboCondicion.SelectedIndex),
            Inforcom = (byte)Math.Max(0, CboInforcom.SelectedIndex),
            Local = CboLocal.SelectedValue is int lid ? lid : 0,
            Conyuge = TxtConyuge.Text.Trim(),
            CiConyuge = TxtCiConyuge.Text.Trim(),
            EmpresaLaboral = TxtEmpresa.Text.Trim(),
            TelefonoLaboral = TxtTelLab.Text.Trim(),
            DireccionLaboral = TxtDirLab.Text.Trim(),
            Antiguedad = TxtAntiguedad.Text.Trim(),
            VencCedula = DpVencCi.SelectedDate,
            FechaRegistro = DateTime.Now,
            // Guardar solo el CI como identificador de foto (igual que VB6)
            FotoCliente = string.IsNullOrEmpty(_fotoRuta) ? string.Empty : TxtCi.Text.Trim()
        };
    }

    // ── FOTO: carga desde BD mostrando la imagen unificada ──────────────────
    private async Task CargarFotoDesdeDb(string ci, int idCliente)
    {
        LimpiarSlotsUI();
        if (string.IsNullOrWhiteSpace(ci) && idCliente == 0) return;
        try
        {
            using var conn = _db.Create();
            var datos = await conn.QueryFirstOrDefaultAsync<byte[]>(
                "SELECT TOP 1 DATOS FROM FOTOS WHERE CI = @ci OR IDCLIE = @id ORDER BY IDFOTO DESC",
                new { ci, id = idCliente });

            if (datos == null || datos.Length == 0) return;

            MostrarEnSlot(ImgFotoDoc, datos);
            PlaceholderFotoDoc.Visibility = Visibility.Collapsed;
            BtnQuitarFotoDoc.IsEnabled = _modoEdicion;
            _fotoRuta = ci;
        }
        catch { LimpiarSlotsUI(); }
    }

    private void CargarFoto(string fotoCliente)
        => _ = CargarFotoDesdeDb(fotoCliente, _clienteSeleccionado?.IdCliente ?? 0);

    // ── Seleccionar frontal ──────────────────────────────────────────────────
    private async void OnSeleccionarFotoFrontal(object s, RoutedEventArgs e)
    {
        var bytes = await SeleccionarImagen("Seleccionar cara FRONTAL de la cédula");
        if (bytes == null) return;
        _bytesFrontal = bytes;
        MostrarEnSlot(ImgFotoFrontal, bytes);
        BtnQuitarFotoFrontal.IsEnabled = true;
        ActualizarEstadoFoto();
    }

    // ── Seleccionar dorso ────────────────────────────────────────────────────
    private async void OnSeleccionarFotoDorso(object s, RoutedEventArgs e)
    {
        var bytes = await SeleccionarImagen("Seleccionar cara DORSO de la cédula");
        if (bytes == null) return;
        _bytesDorso = bytes;
        MostrarEnSlot(ImgFotoDorso, bytes);
        BtnQuitarFotoDorso.IsEnabled = true;
        ActualizarEstadoFoto();
    }

    // ── Quitar frontal ───────────────────────────────────────────────────────
    private void OnQuitarFotoFrontal(object s, RoutedEventArgs e)
    {
        _bytesFrontal = null;
        ImgFotoFrontal.Source = null;
        BtnQuitarFotoFrontal.IsEnabled = false;
        ActualizarEstadoFoto();
    }

    // ── Quitar dorso ─────────────────────────────────────────────────────────
    private void OnQuitarFotoDorso(object s, RoutedEventArgs e)
    {
        _bytesDorso = null;
        ImgFotoDorso.Source = null;
        BtnQuitarFotoDorso.IsEnabled = false;
        ActualizarEstadoFoto();
    }

    // ── Cambiar/Quitar foto en modo vista/editar ─────────────────────────────
    private async void OnCambiarFotoDoc(object s, RoutedEventArgs e)
    {
        var bytes = await SeleccionarImagen("Seleccionar foto del documento");
        if (bytes == null) return;
        _bytesFrontal = bytes;
        _bytesDorso   = null;
        MostrarEnSlot(ImgFotoDoc, bytes);
        PlaceholderFotoDoc.Visibility = Visibility.Collapsed;
        BtnQuitarFotoDoc.IsEnabled = true;
    }

    private async void OnQuitarFotoDoc(object s, RoutedEventArgs e)
    {
        if (_clienteSeleccionado == null) return;
        if (MessageBox.Show("¿Eliminar la foto del documento de este cliente?",
            "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        using var conn = _db.Create();
        await conn.ExecuteAsync(
            "DELETE FROM FOTOS WHERE CI = @ci OR IDCLIE = @id",
            new { ci = _clienteSeleccionado.CiCliente, id = _clienteSeleccionado.IdCliente });
        ImgFotoDoc.Source = null;
        PlaceholderFotoDoc.Visibility = Visibility.Visible;
        BtnQuitarFotoDoc.IsEnabled = false;
        _bytesFrontal = null; _bytesDorso = null;
    }

    // ── Combina frontal + dorso verticalmente y guarda en BD ─────────────────
    public async Task GuardarFotoUnificadaAsync(string ci, int idCliente)
    {
        if (_bytesFrontal == null && _bytesDorso == null) return;

        byte[] unificada = await Task.Run(() =>
        {
            // Si solo hay una cara, guardar esa directamente
            if (_bytesFrontal != null && _bytesDorso == null) return _bytesFrontal;
            if (_bytesFrontal == null && _bytesDorso != null) return _bytesDorso;

            using var imgF = System.Drawing.Image.FromStream(new MemoryStream(_bytesFrontal!));
            using var imgD = System.Drawing.Image.FromStream(new MemoryStream(_bytesDorso!));

            int ancho    = Math.Max(imgF.Width, imgD.Width);
            int sep      = 6; // separador en píxeles entre las dos caras
            int alto     = imgF.Height + sep + imgD.Height;

            using var canvas = new Bitmap(ancho, alto);
            using var g = Graphics.FromImage(canvas);
            g.Clear(System.Drawing.Color.FromArgb(240, 240, 240));
            g.DrawImage(imgF, 0, 0, ancho, imgF.Height);
            g.DrawImage(imgD, 0, imgF.Height + sep, ancho, imgD.Height);

            using var ms = new MemoryStream();
            canvas.Save(ms, ImageFormat.Jpeg);
            return ms.ToArray();
        });

        using var conn = _db.Create();
        var existe = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM FOTOS WHERE CI = @ci OR IDCLIE = @id",
            new { ci, id = idCliente });
        if (existe > 0)
            await conn.ExecuteAsync(
                "UPDATE FOTOS SET DATOS = @datos WHERE CI = @ci OR IDCLIE = @id",
                new { datos = unificada, ci, id = idCliente });
        else
            await conn.ExecuteAsync(
                "INSERT INTO FOTOS (IDCLIE, CI, DATOS) VALUES (@id, @ci, @datos)",
                new { id = idCliente, ci, datos = unificada });

        _bytesFrontal = null;
        _bytesDorso   = null;
        // Si ya estamos en modo vista/editar (cliente existente) refrescamos el slot unificado
        if (_clienteSeleccionado != null)
            await CargarFotoDesdeDb(ci, idCliente);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private static async Task<byte[]?> SeleccionarImagen(string titulo)
    {
        var dlg = new OpenFileDialog {
            Title = titulo,
            Filter = "Imágenes|*.jpg;*.jpeg;*.png;*.bmp|Todos|*.*"
        };
        if (dlg.ShowDialog() != true) return null;
        return await Task.Run(() => File.ReadAllBytes(dlg.FileName));
    }

    private static void MostrarEnSlot(System.Windows.Controls.Image slot, byte[] bytes)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.StreamSource = new MemoryStream(bytes);
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.DecodePixelWidth = 400;
        bmp.EndInit();
        bmp.Freeze();
        slot.Source = bmp;
    }

    private void LimpiarSlotsUI()
    {
        // Panel Nuevo
        _bytesFrontal = null; _bytesDorso = null;
        ImgFotoFrontal.Source = null; ImgFotoDorso.Source = null;
        BtnQuitarFotoFrontal.IsEnabled = false; BtnQuitarFotoDorso.IsEnabled = false;
        TxtFotoRuta.Text = "Sin fotos cargadas";
        // Panel unificado
        ImgFotoDoc.Source = null;
        PlaceholderFotoDoc.Visibility = Visibility.Visible;
        BtnQuitarFotoDoc.IsEnabled = false;
        _fotoRuta = string.Empty;
    }

    private void ActualizarEstadoFoto()
    {
        var partes = new List<string>();
        if (_bytesFrontal != null) partes.Add($"Frontal ({_bytesFrontal.Length / 1024} KB)");
        if (_bytesDorso   != null) partes.Add($"Dorso ({_bytesDorso.Length / 1024} KB)");
        TxtFotoRuta.Text = partes.Count > 0
            ? "📷 " + string.Join("  +  ", partes) + " — se unificarán al guardar"
            : "Sin fotos cargadas";
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.F5 || (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control))
            OnGuardar();
        else if (e.Key == Key.Escape)
            HabilitarEdicion(false);
        else if (e.Key == Key.F2)
            OnAbrirBuscador(this, new RoutedEventArgs());
        else if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
            OnNuevo(this, new RoutedEventArgs());
    }

    private void OnCerrar(object s, RoutedEventArgs e) => Close();
}

// ══════════════════════════════════════════════════════════════════════════
//  MODAL DE BÚSQUEDA DE CLIENTES — con paginado servidor
// ══════════════════════════════════════════════════════════════════════════
public class BuscadorClienteModal : Window
{
    private readonly IClienteRepository _repo;

    // UI — búsqueda y grid
    private TextBox   _txtBuscar   = null!;
    private DataGrid  _grid        = null!;
    private TextBlock _lblConteo   = null!;
    private TextBlock _lblPagina   = null!;
    private Button    _btnAnterior = null!;
    private Button    _btnSiguiente= null!;
    private Border    _loadOverlay = null!;
    private ComboBox  _cboPorPagina= null!;

    // UI — filtros
    private ComboBox _cboEstado    = null!;
    private ComboBox _cboInforcom  = null!;
    private ComboBox _cboTipo      = null!;
    private Button   _btnLocalSel  = null!;
    private TextBox  _txtCredDesde = null!;
    private TextBox  _txtCredHasta = null!;

    // estado filtro local
    private int?   _localFiltroId     = null;
    private string _localFiltroNombre = "";

    // Estado paginación
    private int _porPagina     = 50;
    private int _paginaActual  = 1;
    private int _totalRegistros = 0;
    private string _terminoActual = "";
    private System.Threading.CancellationTokenSource? _cts;
    private Dictionary<int, string> _localesDict = new();

    public Cliente? ClienteSeleccionado { get; private set; }

    private readonly bool _soloConCuotas;
    private readonly bool _soloConCreditos;

    public BuscadorClienteModal(IClienteRepository repo, bool soloConCuotas = false, bool soloConCreditos = false)
    {
        _repo            = repo;
        _soloConCuotas   = soloConCuotas;
        _soloConCreditos = soloConCreditos;
        Title  = soloConCreditos ? "Buscar Cliente (con créditos)"
               : soloConCuotas  ? "Buscar Cliente (con cuotas)"
               : "Buscar Cliente";
        Width  = 1180; Height = 640;
        MinWidth = 960; MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(240, 242, 245));
        BuildUI();
        Loaded += async (_, _) =>
        {
            var locales = await _repo.ObtenerLocalesAsync();
            _localesDict = locales.ToDictionary(l => l.Id, l => l.Nombre);
            _txtBuscar.Focus();
            await CargarPaginaAsync();
        };
    }

    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // header
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grid
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // paginación
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // botones

        // ── Header búsqueda ─────────────────────────────────────────────────
        var headerBg = new Border {
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x0E, 0x2F, 0x44)),
            Padding = new Thickness(12, 10, 12, 10)
        };
        var muted = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x7F, 0xB3, 0xD3));
        var headerSp = new StackPanel { Orientation = Orientation.Horizontal };
        headerSp.Children.Add(new TextBlock {
            Text = "🔍", FontSize = 16, Foreground = muted,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,8,0)
        });
        headerSp.Children.Add(new TextBlock {
            Text = "CI, Nombre o Teléfono:",
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,10,0)
        });
        _txtBuscar = new TextBox {
            Width = 280, Height = 30, FontSize = 13,
            Padding = new Thickness(8, 4, 8, 4),
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = System.Windows.Media.Brushes.White,
            Foreground = System.Windows.Media.Brushes.Black,
        };
        _txtBuscar.TextChanged += OnBuscarChanged;
        _txtBuscar.KeyDown     += (_, e) => { if (e.Key == Key.Enter) Seleccionar(); };
        headerSp.Children.Add(_txtBuscar);

        _lblConteo = new TextBlock {
            Foreground = muted,
            FontSize = 11, FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 0, 0)
        };
        headerSp.Children.Add(_lblConteo);
        headerBg.Child = headerSp;
        Grid.SetRow(headerBg, 0); root.Children.Add(headerBg);

        // ── Cuerpo: panel filtros (izq) + grid (der) ────────────────────────
        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(body, 1); root.Children.Add(body);

        // ── Panel filtros ────────────────────────────────────────────────────
        var filtPanel = new Border {
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(248, 249, 252)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(229, 231, 235)),
            BorderThickness = new Thickness(0, 0, 1, 0)
        };
        var filtScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var filtStack  = new StackPanel { Margin = new Thickness(0) };

        // cabecera panel filtros
        var filtHdr = new Border {
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(13, 71, 161)),
            Padding = new Thickness(10, 8, 10, 8)
        };
        filtHdr.Child = new TextBlock {
            Text = "🔎  Filtros",
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold, FontSize = 12
        };
        filtStack.Children.Add(filtHdr);

        var filtBody = new StackPanel { Margin = new Thickness(10, 8, 10, 8) };

        // helpers locales
        System.Windows.Media.SolidColorBrush CB(string h) =>
            new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

        void AddFiltro(string label, System.Windows.UIElement ctrl) {
            filtBody.Children.Add(new TextBlock {
                Text = label, FontSize = 10.5, FontWeight = FontWeights.SemiBold,
                Foreground = CB("#374151"), Margin = new Thickness(0, 6, 0, 2)
            });
            filtBody.Children.Add(ctrl);
        }

        ComboBox FCbo(params string[] opts) {
            var c = new ComboBox { FontSize = 12, Height = 28 };
            foreach (var o in opts) c.Items.Add(o);
            c.SelectedIndex = 0; return c;
        }
        TextBox FTxt(string ph = "") => new TextBox {
            FontSize = 12, Height = 28, Padding = new Thickness(6, 4, 6, 4),
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = CB("#D1D5DB"), Tag = ph
        };

        _cboEstado   = FCbo("Todos", "Normal", "Moroso");
        _cboInforcom = FCbo("Todos", "Libre", "Registrado");
        _cboTipo     = FCbo("Todos", "Malo", "Regular", "Bueno", "Muy Bueno", "Excelente");
        _txtCredDesde= FTxt("0");
        _txtCredHasta= FTxt("sin límite");
        AplicarFormatoMiles(_txtCredDesde);
        AplicarFormatoMiles(_txtCredHasta);

        // Botón selector de local
        _btnLocalSel = new Button {
            Content = "— Todos los locales —",
            Height = 30, FontSize = 11,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(8, 0, 8, 0),
            Background = System.Windows.Media.Brushes.White,
            Foreground = CB("#374151"),
            BorderBrush = CB("#D1D5DB"), BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand
        };
        _btnLocalSel.Click += async (_, _) =>
        {
            var locales = (await _repo.ObtenerLocalesAsync()).ToList();
            var modal = new SelectorLocalModal(locales) { Owner = this };
            if (modal.ShowDialog() == true)
            {
                _localFiltroId     = modal.LocalSeleccionadoId;
                _localFiltroNombre = modal.LocalSeleccionadoNombre;
                _btnLocalSel.Content  = _localFiltroId.HasValue
                    ? $"✔ {_localFiltroNombre}"
                    : "— Todos los locales —";
                _btnLocalSel.Foreground = _localFiltroId.HasValue
                    ? CB("#1B5E20") : CB("#374151");
            }
        };

        AddFiltro("Estado",          _cboEstado);
        AddFiltro("Inforcom",        _cboInforcom);
        AddFiltro("Tipo cliente",    _cboTipo);
        AddFiltro("Local",           _btnLocalSel);
        AddFiltro("Créd. Máx desde", _txtCredDesde);
        AddFiltro("Créd. Máx hasta", _txtCredHasta);

        // botones Aplicar / Limpiar
        var btnAplicar = new Button {
            Content = "Aplicar filtros", Height = 32, FontSize = 11.5,
            FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 4),
            Background = CB("#1565C0"), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };
        var btnLimpiar = new Button {
            Content = "Limpiar filtros", Height = 28, FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Background = CB("#6B7280"), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };
        btnAplicar.Click += async (_, _) => { _paginaActual = 1; await CargarPaginaAsync(); };
        btnLimpiar.Click += async (_, _) => {
            _cboEstado.SelectedIndex = 0; _cboInforcom.SelectedIndex = 0;
            _cboTipo.SelectedIndex   = 0;
            _txtCredDesde.Text = ""; _txtCredHasta.Text = "";
            _localFiltroId = null; _localFiltroNombre = "";
            _btnLocalSel.Content    = "— Todos los locales —";
            _btnLocalSel.Foreground = CB("#374151");
            _paginaActual = 1; await CargarPaginaAsync();
        };
        filtBody.Children.Add(btnAplicar);
        filtBody.Children.Add(btnLimpiar);

        filtStack.Children.Add(filtBody);
        filtScroll.Content = filtStack;
        filtPanel.Child    = filtScroll;
        Grid.SetColumn(filtPanel, 0); body.Children.Add(filtPanel);

        // ── DataGrid ────────────────────────────────────────────────────────
        var gridBorder = new Border { Margin = new Thickness(6, 6, 8, 0) };
        _grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(229, 231, 235)),
            RowBackground = System.Windows.Media.Brushes.White,
            AlternatingRowBackground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(249, 250, 251)),
            FontSize = 12,
            ColumnHeaderStyle = BuildHeaderStyle()
        };
        _grid.Columns.Add(new DataGridTextColumn { Header = "CI",        Binding = new System.Windows.Data.Binding("CiCliente"),       Width = new DataGridLength(95) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Nombre",    Binding = new System.Windows.Data.Binding("NombreCliente"),   Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Teléfono",  Binding = new System.Windows.Data.Binding("TelefonoCliente"), Width = new DataGridLength(95) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Ciudad",    Binding = new System.Windows.Data.Binding("CiudadCliente"),   Width = new DataGridLength(100) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Estado",    Binding = new System.Windows.Data.Binding("EstadoTexto"),     Width = new DataGridLength(70) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Créd. Máx", Binding = new System.Windows.Data.Binding("CredMax") { StringFormat = "{0:N0}" }, Width = new DataGridLength(90) });
        _grid.MouseDoubleClick += (_, _) => Seleccionar();
        gridBorder.Child = _grid;

        // overlay de carga
        _loadOverlay = new Border {
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(180, 255, 255, 255)),
            Visibility = Visibility.Collapsed
        };
        _loadOverlay.Child = new TextBlock {
            Text = "Cargando...", FontSize = 14, FontWeight = FontWeights.SemiBold,
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(107, 114, 128)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var gridContainer = new Grid();
        gridContainer.Children.Add(gridBorder);
        gridContainer.Children.Add(_loadOverlay);
        Grid.SetColumn(gridContainer, 1); body.Children.Add(gridContainer);

        // ── Barra paginación ─────────────────────────────────────────────────
        var pagBar = new Border {
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(248, 249, 252)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(229, 231, 235)),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(12, 6, 12, 6)
        };

        // layout: [selector filas/pág — izquierda] [navegación — centro] [espacio — derecha]
        var pagGrid = new Grid();
        pagGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        pagGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pagGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // selector de registros por página
        var selSp = new StackPanel { Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center };
        selSp.Children.Add(new TextBlock {
            Text = "Filas por página:",
            FontSize = 11, VerticalAlignment = VerticalAlignment.Center,
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(107, 114, 128)),
            Margin = new Thickness(0, 0, 6, 0)
        });
        _cboPorPagina = new ComboBox {
            Height = 26, Width = 64, FontSize = 12,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        foreach (var n in new[] { 5, 10, 15, 20, 50, 100 })
            _cboPorPagina.Items.Add(n);
        _cboPorPagina.SelectedItem = 50;
        _cboPorPagina.SelectionChanged += async (_, _) =>
        {
            if (_cboPorPagina.SelectedItem is int n)
            {
                _porPagina    = n;
                _paginaActual = 1;
                await CargarPaginaAsync();
            }
        };
        selSp.Children.Add(_cboPorPagina);
        Grid.SetColumn(selSp, 0); pagGrid.Children.Add(selSp);

        // navegación centrada
        var pagSp = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center };

        Button PagBtn(string txt) => new Button {
            Content = txt, Height = 28, MinWidth = 32,
            Padding = new Thickness(10, 0, 10, 0),
            FontSize = 12, FontWeight = FontWeights.SemiBold,
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(255, 140, 0)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            Margin = new Thickness(4, 0, 4, 0)
        };

        _btnAnterior  = PagBtn("◀ Anterior");
        _btnSiguiente = PagBtn("Siguiente ▶");
        _lblPagina = new TextBlock {
            FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 12, 0),
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(55, 65, 81))
        };

        _btnAnterior.Click  += async (_, _) => { _paginaActual--; await CargarPaginaAsync(); };
        _btnSiguiente.Click += async (_, _) => { _paginaActual++; await CargarPaginaAsync(); };

        pagSp.Children.Add(_btnAnterior);
        pagSp.Children.Add(_lblPagina);
        pagSp.Children.Add(_btnSiguiente);
        Grid.SetColumn(pagSp, 1); pagGrid.Children.Add(pagSp);

        pagBar.Child = pagGrid;
        Grid.SetRow(pagBar, 2); root.Children.Add(pagBar);

        // ── Botones acción ───────────────────────────────────────────────────
        var barBtns = new Border {
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(229, 231, 235)),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(8, 8, 8, 8)
        };
        var barGrid2 = new Grid();
        barGrid2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        barGrid2.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var hint = new TextBlock {
            Text = "Doble clic o Enter para seleccionar",
            FontSize = 10.5,
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(156, 163, 175)),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0)
        };

        Button MkBtn(string txt, string hex) => new Button {
            Content = txt, Height = 32, Padding = new Thickness(14, 0, 14, 0),
            Margin = new Thickness(6, 0, 0, 0), FontWeight = FontWeights.SemiBold,
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };

        var btnVista      = MkBtn("Vista previa",  "#6B7280");
        var btnImprimir   = MkBtn("Imprimir",       "#1B5E20");
        var btnImpTodo    = MkBtn("Imprimir todo",  "#6438A0");
        var btnSelec      = MkBtn("✔  Seleccionar", "#22C55E");
        var btnCerrar     = MkBtn("✕  Cerrar",       "#374151");

        btnVista.Click    += (_, _) => AbrirPrevia(imprimir: false, todo: false);
        btnImprimir.Click += (_, _) => AbrirPrevia(imprimir: true,  todo: false);
        btnImpTodo.Click  += (_, _) => AbrirPrevia(imprimir: true,  todo: true);
        btnSelec.Click    += (_, _) => Seleccionar();
        btnCerrar.Click   += (_, _) => { DialogResult = false; Close(); };

        var btnsSp = new StackPanel { Orientation = Orientation.Horizontal };
        btnsSp.Children.Add(btnVista);
        btnsSp.Children.Add(btnImprimir);
        btnsSp.Children.Add(btnImpTodo);
        btnsSp.Children.Add(btnSelec);
        btnsSp.Children.Add(btnCerrar);

        Grid.SetColumn(hint, 0); barGrid2.Children.Add(hint);
        Grid.SetColumn(btnsSp, 1); barGrid2.Children.Add(btnsSp);
        barBtns.Child = barGrid2;
        Grid.SetRow(barBtns, 3); root.Children.Add(barBtns);

        Content = root;
    }

    private static Style BuildHeaderStyle()
    {
        var s = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        s.Setters.Add(new Setter(Control.BackgroundProperty,
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1A, 0x4F, 0x6E))));
        s.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
        s.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        s.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 6, 8, 6)));
        s.Setters.Add(new Setter(Control.FontSizeProperty, 11.5));
        return s;
    }

    private void OnBuscarChanged(object s, System.Windows.Controls.TextChangedEventArgs e)
    {
        _cts?.Cancel();
        _cts = new System.Threading.CancellationTokenSource();
        var token = _cts.Token;
        var delay = Task.Delay(350, token);
        delay.ContinueWith(async t => {
            if (t.IsCanceled) return;
            await Dispatcher.InvokeAsync(async () => {
                _paginaActual  = 1;
                _terminoActual = _txtBuscar.Text.Trim();
                await CargarPaginaAsync();
            });
        }, System.Threading.Tasks.TaskScheduler.Default);
    }

    private static void AplicarFormatoMiles(TextBox txt)
    {
        bool upd = false;
        txt.PreviewTextInput += (_, e) => { e.Handled = !char.IsDigit(e.Text[0]); };
        DataObject.AddPastingHandler(txt, (_, e) =>
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                var t = (string)e.DataObject.GetData(DataFormats.Text);
                if (!t.All(char.IsDigit)) e.CancelCommand();
            }
            else e.CancelCommand();
        });
        txt.TextChanged += (_, __) =>
        {
            if (upd) return;
            upd = true;
            var caret = txt.CaretIndex;
            var digBefore = txt.Text.Take(caret).Count(char.IsDigit);
            var raw = new string(txt.Text.Where(char.IsDigit).ToArray()).TrimStart('0');
            if (raw == "") raw = "0";
            var formatted = long.TryParse(raw, out var n)
                ? n.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-PY"))
                : raw;
            txt.Text = formatted;
            int newCaret = 0, dCount = 0;
            foreach (char c in formatted)
            {
                if (dCount >= digBefore) break;
                newCaret++;
                if (char.IsDigit(c)) dCount++;
            }
            txt.CaretIndex = Math.Min(newCaret, formatted.Length);
            upd = false;
        };
    }

    private (int? estado, int? inforcom, int? tipo, int? local,
             decimal? credDesde, decimal? credHasta, string ciudad) LeerFiltros()
    {
        int? estado   = _cboEstado.SelectedIndex   > 0 ? _cboEstado.SelectedIndex   - 1 : null;
        int? inforcom = _cboInforcom.SelectedIndex > 0 ? _cboInforcom.SelectedIndex - 1 : null;
        int? tipo     = _cboTipo.SelectedIndex     > 0 ? _cboTipo.SelectedIndex     - 1 : null;
        var rawDesde = new string(_txtCredDesde.Text.Where(char.IsDigit).ToArray());
        var rawHasta = new string(_txtCredHasta.Text.Where(char.IsDigit).ToArray());
        decimal? credDesde = rawDesde.Length > 0 && rawDesde != "0" ? decimal.Parse(rawDesde) : null;
        decimal? credHasta = rawHasta.Length > 0 && rawHasta != "0" ? decimal.Parse(rawHasta) : null;
        return (estado, inforcom, tipo, _localFiltroId, credDesde, credHasta, "");
    }

    private async Task CargarPaginaAsync()
    {
        _loadOverlay.Visibility = Visibility.Visible;
        _btnAnterior.IsEnabled  = false;
        _btnSiguiente.IsEnabled = false;
        try
        {
            var (estado, inforcom, tipo, local, credDesde, credHasta, ciudad) = LeerFiltros();
            var (filas, total) = await _repo.BuscarPaginadoAsync(
                _terminoActual, _paginaActual, _porPagina,
                estado, inforcom, tipo, local, credDesde, credHasta, ciudad,
                soloConCuotas: _soloConCuotas, soloConCreditos: _soloConCreditos);
            _totalRegistros = total;
            _grid.ItemsSource  = filas;
            _grid.SelectedItem = null;

            int totalPaginas = Math.Max(1, (total + _porPagina - 1) / _porPagina);
            _lblPagina.Text = $"Página {_paginaActual} de {totalPaginas}  ({total:N0} registros)";
            _lblConteo.Text = _soloConCreditos  ? $"{total:N0} clientes con créditos"
                            : _soloConCuotas    ? $"{total:N0} clientes con cuotas pendientes"
                            : $"{total:N0} clientes";
            _btnAnterior.IsEnabled  = _paginaActual > 1;
            _btnSiguiente.IsEnabled = _paginaActual < totalPaginas;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al cargar clientes:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { _loadOverlay.Visibility = Visibility.Collapsed; }
    }

    private void Seleccionar()
    {
        if (_grid.SelectedItem is Cliente c)
        {
            ClienteSeleccionado = c;
            DialogResult = true;
            Close();
        }
        else
        {
            MessageBox.Show("Seleccione un cliente de la lista.",
                "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void AbrirPrevia(bool imprimir, bool todo)
    {
        List<Cliente> fuente;
        if (todo)
        {
            // Cargar todos los registros filtrados del servidor
            _loadOverlay.Visibility = Visibility.Visible;
            try
            {
                var (estado, inforcom, tipo, local, credDesde, credHasta, ciudad) = LeerFiltros();
            var (filas, _) = await _repo.BuscarPaginadoAsync(
                _terminoActual, 1, 99999,
                estado, inforcom, tipo, local, credDesde, credHasta, ciudad,
                soloConCuotas: _soloConCuotas, soloConCreditos: _soloConCreditos);
                fuente = filas.ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar clientes:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            finally { _loadOverlay.Visibility = Visibility.Collapsed; }
        }
        else
        {
            fuente = (_grid.ItemsSource as System.Collections.IEnumerable)?
                .OfType<Cliente>().ToList() ?? new List<Cliente>();
        }

        if (fuente.Count == 0)
        {
            MessageBox.Show("No hay registros para imprimir.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var logoPath = ArticulosPagina.ResolverLogoPath();
        var filtroDesc = string.IsNullOrEmpty(_terminoActual)
            ? "Todos los clientes"
            : $"Búsqueda: {_terminoActual}";

        static string TipoStr(byte t) => t switch {
            0 => "Malo", 1 => "Regular", 2 => "Bueno", 3 => "Muy Bueno", 4 => "Excelente", _ => "—" };

        var pagina = new ClientePagina
        {
            Filas = fuente.Select(c => new FilaClienteImp(
                Ci:          c.CiCliente,
                Nombre:      c.NombreCliente,
                Telefono:    c.TelefonoCliente,
                Ciudad:      c.CiudadCliente,
                Estado:      c.Estado == 1 ? "Moroso" : "Normal",
                CredMax:     c.CredMax > 0 ? c.CredMax.ToString("N0") : "—",
                Inforcom:    c.Inforcom == 1 ? "Registrado" : "Libre",
                Local:       c.Local > 0 && _localesDict.TryGetValue(c.Local, out var lNom) ? lNom : (c.Local > 0 ? c.Local.ToString() : "—"),
                TipoCliente: TipoStr(c.Tipo),
                Empresa:     string.IsNullOrEmpty(c.EmpresaLaboral) ? "—" : c.EmpresaLaboral,
                DirLab:      string.IsNullOrEmpty(c.DireccionLaboral) ? "—" : c.DireccionLaboral,
                TelLab:      string.IsNullOrEmpty(c.TelefonoLaboral) ? "—" : c.TelefonoLaboral,
                Antiguedad:  string.IsNullOrEmpty(c.Antiguedad) ? "—" : c.Antiguedad,
                TieneCI:     string.IsNullOrEmpty(c.FotoCliente) ? "No" : "Sí"
            )).ToList(),
            Filtro    = filtroDesc,
            FechaImp  = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            Usuario   = CrediSoft.Core.Services.SessionService.Instance.UsuarioActual?.NombreUsuario ?? "",
            LogoPath  = logoPath,
        };

        if (imprimir)
            ClienteImpresora.Imprimir(pagina, this);
        else
            new ClientePreviewWindow(pagina) { Owner = this }.ShowDialog();
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  SELECTOR DE LOCAL (modal pequeño)
// ══════════════════════════════════════════════════════════════════════════════
public class SelectorLocalModal : Window
{
    private readonly List<(int Id, string Nombre)> _locales;
    private DataGrid _grid = null!;

    public int?   LocalSeleccionadoId     { get; private set; }
    public string LocalSeleccionadoNombre { get; private set; } = "";

    public SelectorLocalModal(List<(int Id, string Nombre)> locales)
    {
        _locales = locales;
        Title  = "Seleccionar Local";
        Width  = 400; Height = 460;
        MinWidth = 320; MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(240, 242, 245));
        BuildUI();
    }

    private void BuildUI()
    {
        System.Windows.Media.SolidColorBrush CB(string h) =>
            new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // header
        var hdr = new Border { Background = CB("#0E2F44"), Padding = new Thickness(14, 10, 14, 10) };
        hdr.Child = new TextBlock {
            Text = "🏪  Seleccionar Local",
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 14, FontWeight = FontWeights.Bold
        };
        Grid.SetRow(hdr, 0); root.Children.Add(hdr);

        // grid
        var hdrStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        hdrStyle.Setters.Add(new Setter(Control.BackgroundProperty, CB("#0E2F44")));
        hdrStyle.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
        hdrStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        hdrStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 6, 10, 6)));

        _grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = CB("#F3F4F6"),
            RowBackground = System.Windows.Media.Brushes.White,
            AlternatingRowBackground = CB("#F9FAFB"),
            FontSize = 12, Margin = new Thickness(8, 8, 8, 0),
            ColumnHeaderStyle = hdrStyle
        };
        _grid.Columns.Add(new DataGridTextColumn {
            Header = "N°", Binding = new System.Windows.Data.Binding("Id"),
            Width = new DataGridLength(60)
        });
        _grid.Columns.Add(new DataGridTextColumn {
            Header = "Nombre", Binding = new System.Windows.Data.Binding("Nombre"),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });

        // fila "Todos" al inicio — anonymous objects para que DataGrid binding funcione
        var items = new[] { new { Id = 0, Nombre = "— Todos los locales —" } }
            .Concat(_locales.Select(x => new { Id = x.Id, Nombre = x.Nombre }))
            .ToList();
        _grid.ItemsSource = items;
        _grid.MouseDoubleClick += (_, _) => Seleccionar();

        var gridBorder = new Border { Margin = new Thickness(0) };
        gridBorder.Child = _grid;
        Grid.SetRow(gridBorder, 1); root.Children.Add(gridBorder);

        // botones
        var footer = new Border {
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = CB("#E5E7EB"), BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(10, 8, 10, 8)
        };
        var btnSp = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right };

        Button MkBtn(string t, string hex) => new Button {
            Content = t, Height = 32, Padding = new Thickness(16,0,16,0),
            Margin = new Thickness(6,0,0,0), FontWeight = FontWeights.SemiBold,
            Background = CB(hex), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };
        var btnSel = MkBtn("✔  Seleccionar", "#22C55E");
        var btnCer = MkBtn("✕  Cerrar",      "#6B7280");
        btnSel.Click += (_, _) => Seleccionar();
        btnCer.Click += (_, _) => { DialogResult = false; Close(); };
        btnSp.Children.Add(btnSel);
        btnSp.Children.Add(btnCer);
        footer.Child = btnSp;
        Grid.SetRow(footer, 2); root.Children.Add(footer);

        Content = root;
        Loaded += (_, _) => { if (_grid.Items.Count > 0) _grid.SelectedIndex = 0; };
    }

    private void Seleccionar()
    {
        if (_grid.SelectedItem is { } item)
        {
            var t      = item.GetType();
            var id     = (int)t.GetProperty("Id")!.GetValue(item)!;
            var nombre = (string)t.GetProperty("Nombre")!.GetValue(item)!;
            LocalSeleccionadoId     = id == 0 ? null : id;
            LocalSeleccionadoNombre = nombre;
            DialogResult = true;
            Close();
        }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
//  CONSULTA / INFORME DE CLIENTES
// ══════════════════════════════════════════════════════════════════════════════
public class ConsultaClientesWindow : Window
{
    private readonly IDbConnectionFactory _db;

    // Filtros
    private TextBox   _txtCiudad   = null!;
    private ComboBox  _cboEstado   = null!;
    private ComboBox  _cboSexo     = null!;
    private ComboBox  _cboTipo     = null!;
    private ComboBox  _cboCondicion = null!;
    private TextBox   _txtLocal    = null!;
    private TextBox   _txtDesde    = null!;
    private TextBox   _txtHasta    = null!;

    // Grilla
    private DataGrid  _dg          = null!;
    private TextBlock _lblConteo   = null!;

    private static System.Windows.Media.SolidColorBrush CB(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    public ConsultaClientesWindow()
    {
        _db = App.Services.GetRequiredService<IDbConnectionFactory>();
        Title = "Consultas / Informes sobre clientes";
        Width = 1100; Height = 680; MinWidth = 800; MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = CB("#F5F5F5");
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        FontSize = 12;
        BuildUI();
    }

    private void BuildUI()
    {
        var root = new DockPanel();

        // ── Header ────────────────────────────────────────────────────────
        var hdr = new Border { Padding = new Thickness(20, 14, 20, 14) };
        var grad = new System.Windows.Media.LinearGradientBrush(
            System.Windows.Media.Color.FromRgb(21, 101, 192),
            System.Windows.Media.Color.FromRgb(30, 136, 229), 0);
        hdr.Background = grad;
        var hSp = new StackPanel();
        hSp.Children.Add(new TextBlock { Text = "👥  Consultas / Informes sobre Clientes",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 15, FontWeight = FontWeights.Bold });
        hSp.Children.Add(new TextBlock {
            Text = "Filtre por ciudad, estado, sexo, tipo, condición, local o inicial de nombre",
            Foreground = CB("#BBDEFB"), FontSize = 10, Margin = new Thickness(0,3,0,0) });
        hdr.Child = hSp;
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        // ── Pie ───────────────────────────────────────────────────────────
        var pie = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = CB("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(16,10,16,10) };
        var pieDp = new DockPanel();
        _lblConteo = new TextBlock { VerticalAlignment = VerticalAlignment.Center,
            Foreground = CB("#1565C0"), FontSize = 12, FontWeight = FontWeights.SemiBold };
        DockPanel.SetDock(_lblConteo, Dock.Left); pieDp.Children.Add(_lblConteo);
        var pieBtns = new StackPanel { Orientation = Orientation.Horizontal };
        Button PBtn(string t, string bg) => new Button { Content = t, Height = 36,
            Padding = new Thickness(20,0,20,0), Margin = new Thickness(8,0,0,0),
            Background = CB(bg), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand, FontSize = 12 };
        var btnBuscar = PBtn("🔍  Buscar (F2)", "#1565C0");
        var btnLimpiar = PBtn("↺  Limpiar", "#546E7A");
        var btnCerrar  = PBtn("✖  Cerrar",  "#B71C1C");
        btnBuscar.Click  += async (_, _) => await Buscar();
        btnLimpiar.Click += (_, _) => Limpiar();
        btnCerrar.Click  += (_, _) => Close();
        pieBtns.Children.Add(btnBuscar); pieBtns.Children.Add(btnLimpiar); pieBtns.Children.Add(btnCerrar);
        DockPanel.SetDock(pieBtns, Dock.Right); pieDp.Children.Add(pieBtns);
        pie.Child = pieDp;
        DockPanel.SetDock(pie, Dock.Bottom); root.Children.Add(pie);

        // ── Layout: panel izquierdo + grilla ─────────────────────────────
        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(body);

        // ── Panel filtros profesional ─────────────────────────────────────
        var panelFiltros = new Border {
            Background = CB("#F8FAFC"),
            BorderBrush = CB("#CFD8DC"), BorderThickness = new Thickness(0,0,1,0) };
        var filtScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var filtRoot = new StackPanel();

        // título panel
        var panHdr = new Border {
            Background = CB("#1565C0"), Padding = new Thickness(14, 10, 14, 10) };
        panHdr.Child = new TextBlock { Text = "🔎  Filtros de búsqueda",
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold, FontSize = 12 };
        filtRoot.Children.Add(panHdr);

        var filtBody = new StackPanel { Margin = new Thickness(12, 10, 12, 10) };

        // helper: campo con ícono izquierdo
        void AddField(string icon, string label, System.Windows.UIElement control) {
            var card = new Border { Background = System.Windows.Media.Brushes.White,
                BorderBrush = CB("#E0E0E0"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6), Margin = new Thickness(0,0,0,8),
                Padding = new Thickness(0) };
            var g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            // label strip
            var lstrip = new Border { Background = CB("#E3F2FD"),
                Padding = new Thickness(8,4,8,4), CornerRadius = new CornerRadius(5,5,0,0) };
            var lSp = new StackPanel { Orientation = Orientation.Horizontal };
            lSp.Children.Add(new TextBlock { Text = icon, FontSize = 12,
                Margin = new Thickness(0,0,5,0), VerticalAlignment = VerticalAlignment.Center });
            lSp.Children.Add(new TextBlock { Text = label, FontSize = 11,
                FontWeight = FontWeights.SemiBold, Foreground = CB("#1565C0"),
                VerticalAlignment = VerticalAlignment.Center });
            lstrip.Child = lSp;
            Grid.SetRow(lstrip, 0); g.Children.Add(lstrip);
            // control wrapper
            var cWrap = new Border { Padding = new Thickness(8,6,8,6) };
            cWrap.Child = control;
            Grid.SetRow(cWrap, 1); g.Children.Add(cWrap);
            card.Child = g;
            filtBody.Children.Add(card);
        }

        ComboBox FCbo(params string[] opts) {
            var c = new ComboBox { BorderThickness = new Thickness(0),
                Background = System.Windows.Media.Brushes.Transparent };
            foreach (var o in opts) c.Items.Add(o);
            c.SelectedIndex = 0; return c;
        }
        TextBox FTxt() => new TextBox { BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent,
            Padding = new Thickness(0,2,0,2) };

        _txtCiudad = FTxt();    AddField("🏙", "Ciudad",     _txtCiudad);
        _cboEstado = FCbo("Todos", "Normal", "Moroso");
                                AddField("⚡", "Estado",     _cboEstado);
        _cboSexo   = FCbo("Todos", "Femenino", "Masculino");
                                AddField("👤", "Sexo",       _cboSexo);
        _cboTipo   = FCbo("Todos", "Malo", "Regular", "Bueno", "Muy bueno", "Excelente");
                                AddField("⭐", "Tipo",       _cboTipo);
        _cboCondicion = FCbo("Todos", "Activo", "Cancelado");
                                AddField("✅", "Condición",  _cboCondicion);
        _txtLocal  = FTxt();    AddField("🏪", "Local (N°)", _txtLocal);

        // separador Filtrar Nombres
        var sepCard = new Border { Background = CB("#1565C0"),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(10,7,10,7),
            Margin = new Thickness(0,4,0,8) };
        sepCard.Child = new TextBlock { Text = "🔤  Filtrar por inicial de nombre",
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, FontSize = 11 };
        filtBody.Children.Add(sepCard);

        // Desde/Hasta en card
        var desdeHastaCard = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = CB("#E0E0E0"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(10,8,10,8),
            Margin = new Thickness(0,0,0,12) };
        var dhG = new Grid();
        dhG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        dhG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        dhG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        void DHCol(string lbl, ref TextBox tb, int col) {
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = lbl, FontSize = 10,
                Foreground = CB("#757575"), Margin = new Thickness(0,0,0,3) });
            tb = new TextBox { MaxLength = 1, TextAlignment = TextAlignment.Center,
                FontSize = 14, FontWeight = FontWeights.Bold,
                BorderBrush = CB("#BBDEFB"), Padding = new Thickness(4,4,4,4) };
            sp.Children.Add(tb);
            Grid.SetColumn(sp, col); dhG.Children.Add(sp);
        }
        TextBox tmpDesde = null!, tmpHasta = null!;
        DHCol("Desde (letra)", ref tmpDesde, 0);
        DHCol("Hasta (letra)", ref tmpHasta, 2);
        _txtDesde = tmpDesde; _txtHasta = tmpHasta;
        desdeHastaCard.Child = dhG;
        filtBody.Children.Add(desdeHastaCard);

        // Botón buscar dentro del panel
        var btnBuscarP = new Button { Content = "🔍  Buscar", Height = 38,
            Background = CB("#1565C0"), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold, FontSize = 13, BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand };
        var btnBuscarPRad = new Border { CornerRadius = new CornerRadius(6),
            ClipToBounds = true, Margin = new Thickness(0,0,0,6) };
        btnBuscarPRad.Child = btnBuscarP;
        btnBuscarP.Click += async (_, _) => await Buscar();
        filtBody.Children.Add(btnBuscarPRad);

        filtRoot.Children.Add(filtBody);
        filtScroll.Content = filtRoot;
        panelFiltros.Child = filtScroll;
        Grid.SetColumn(panelFiltros, 0); body.Children.Add(panelFiltros);

        // Grilla
        var dgBorder = new Border { Background = System.Windows.Media.Brushes.White,
            Margin = new Thickness(0) };

        var hdrSt = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        hdrSt.Setters.Add(new Setter(Control.BackgroundProperty, CB("#1565C0")));
        hdrSt.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
        hdrSt.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        hdrSt.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8,5,8,5)));
        hdrSt.Setters.Add(new Setter(Control.FontSizeProperty, 11.0));

        _dg = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = CB("#E3F2FD"),
            AlternatingRowBackground = CB("#E8F5E9"),
            Background = System.Windows.Media.Brushes.White,
            ColumnHeaderStyle = hdrSt, FontSize = 11, RowHeight = 26 };

        DataGridTextColumn DC(string h, string b, double w) =>
            new DataGridTextColumn { Header = h,
                Binding = new System.Windows.Data.Binding(b),
                Width = new DataGridLength(w, DataGridLengthUnitType.Star) };

        _dg.Columns.Add(DC("CI",        "Ci",        0.8));
        _dg.Columns.Add(DC("RUC",       "Ruc",       0.8));
        _dg.Columns.Add(DC("Nombre",    "Nombre",    2.5));
        _dg.Columns.Add(DC("Teléfono",  "Telefono",  1.0));
        _dg.Columns.Add(DC("Estado",    "EstadoStr", 0.6));
        _dg.Columns.Add(DC("Inforcom",  "InforcomStr",0.6));
        _dg.Columns.Add(DC("Condición", "CondStr",   0.7));
        _dg.Columns.Add(DC("Local",     "Local",     0.4));

        dgBorder.Child = _dg;
        Grid.SetColumn(dgBorder, 1); body.Children.Add(dgBorder);

        Content = root;
        KeyDown += async (_, e) => { if (e.Key == Key.F2) await Buscar(); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    private void Limpiar()
    {
        _txtCiudad.Text = ""; _txtLocal.Text = "";
        _txtDesde.Text  = ""; _txtHasta.Text = "";
        _cboEstado.SelectedIndex = 0; _cboSexo.SelectedIndex = 0;
        _cboTipo.SelectedIndex   = 0; _cboCondicion.SelectedIndex = 0;
        _dg.ItemsSource = null; _lblConteo.Text = "";
    }

    private async Task Buscar()
    {
        try {
            // construir filtros
            string ciudad   = _txtCiudad.Text.Trim();
            int?   estado   = _cboEstado.SelectedIndex == 0 ? null : (int?)(_cboEstado.SelectedIndex - 1);
            int?   sexo     = _cboSexo.SelectedIndex == 0 ? null : (int?)(_cboSexo.SelectedIndex - 1);
            int?   tipo     = _cboTipo.SelectedIndex == 0 ? null : (int?)(_cboTipo.SelectedIndex - 1);
            int?   condicion= _cboCondicion.SelectedIndex == 0 ? null : (int?)(_cboCondicion.SelectedIndex - 1);
            string local    = _txtLocal.Text.Trim();
            string desde    = _txtDesde.Text.Trim().ToUpper();
            string hasta    = _txtHasta.Text.Trim().ToUpper();

            using var conn = _db.Create();
            var rows = (await conn.QueryAsync<FilaConsultaCliente>(
                @"SELECT CI_CLIENTE AS Ci, RUC_CLIENTE AS Ruc, NOMBRE_CLIENTE AS Nombre,
                         TELEFONO_CLIENTE AS Telefono, ESTADO, INFORCOM, CONDICION, LOCAL
                  FROM CLIENTES
                  WHERE (@ciudad = '' OR CIUDAD_CLIENTE LIKE '%' + @ciudad + '%')
                    AND (@estado IS NULL OR ESTADO = @estado)
                    AND (@sexo IS NULL OR SEXO = @sexo)
                    AND (@tipo IS NULL OR TIPO = @tipo)
                    AND (@condicion IS NULL OR CONDICION = @condicion)
                    AND (@local = '' OR CAST(LOCAL AS VARCHAR) = @local)
                    AND (@desde = '' OR NOMBRE_CLIENTE >= @desde)
                    AND (@hasta = '' OR NOMBRE_CLIENTE <= @hasta + 'ZZZZZZ')
                  ORDER BY NOMBRE_CLIENTE",
                new { ciudad, estado, sexo, tipo, condicion, local, desde, hasta })).ToList();

            _dg.ItemsSource = rows;
            _lblConteo.Text = $"{rows.Count} cliente(s) encontrado(s)";
        } catch (Exception ex) {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

internal class FilaConsultaCliente
{
    public string Ci        { get; set; } = "";
    public string Ruc       { get; set; } = "";
    public string Nombre    { get; set; } = "";
    public string Telefono  { get; set; } = "";
    public int    ESTADO    { get; set; }
    public int    INFORCOM  { get; set; }
    public int    CONDICION { get; set; }
    public int    LOCAL     { get; set; }
    public string EstadoStr   => ESTADO   == 1 ? "Moroso"    : "Normal";
    public string InforcomStr => INFORCOM == 1 ? "Registrado": "Libre";
    public string CondStr     => CONDICION== 1 ? "Cancelado" : "Activo";
}
