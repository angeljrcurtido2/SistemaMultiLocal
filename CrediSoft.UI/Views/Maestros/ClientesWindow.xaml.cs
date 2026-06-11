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
        _clienteSeleccionado = completo;
        BtnEditar.IsEnabled   = true;
        BtnEliminar.IsEnabled = true;

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
        _clienteSeleccionado = null;
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

    private void OnVista(object s, RoutedEventArgs e)
    {
        HabilitarEdicion(false);
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

        var dlg = new Window
        {
            Title = "Confirmar registro de cliente",
            Width = 480,
            MaxHeight = 620,
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

        var scroll = new ScrollViewer
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 460,
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
//  MODAL DE BÚSQUEDA DE CLIENTES
// ══════════════════════════════════════════════════════════════════════════
public class BuscadorClienteModal : Window
{
    private readonly IClienteRepository _repo;
    private TextBox  _txtBuscar  = null!;
    private DataGrid _grid       = null!;
    private List<Cliente> _todos = new();

    public Cliente? ClienteSeleccionado { get; private set; }

    public BuscadorClienteModal(IClienteRepository repo)
    {
        _repo = repo;
        Title  = "Buscar Cliente";
        Width  = 720; Height = 500;
        MinWidth = 560; MinHeight = 380;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(240, 242, 245));
        BuildUI();
        Loaded += async (_, _) => await CargarAsync();
    }

    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // header búsqueda
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grid
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // botones

        // ── Header búsqueda ──
        var headerBg = new Border {
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(255, 140, 0)),
            Padding = new Thickness(12, 10, 12, 10)
        };
        var headerSp = new StackPanel { Orientation = Orientation.Horizontal };
        headerSp.Children.Add(new TextBlock {
            Text = "🔍", FontSize = 16, Foreground = System.Windows.Media.Brushes.White,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,8,0)
        });
        headerSp.Children.Add(new TextBlock {
            Text = "CI, Nombre o Razón Social:",
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
        _txtBuscar.TextChanged += (_, _) => Filtrar();
        _txtBuscar.KeyDown     += (_, e) => { if (e.Key == Key.Enter) Seleccionar(); };
        headerSp.Children.Add(_txtBuscar);
        headerBg.Child = headerSp;
        Grid.SetRow(headerBg, 0); root.Children.Add(headerBg);

        // ── DataGrid ──
        _grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(229, 231, 235)),
            RowBackground = System.Windows.Media.Brushes.White,
            AlternatingRowBackground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(249, 250, 251)),
            FontSize = 12, Margin = new Thickness(8, 6, 8, 0),
            ColumnHeaderStyle = BuildHeaderStyle()
        };
        _grid.Columns.Add(new DataGridTextColumn { Header = "CI",       Binding = new System.Windows.Data.Binding("CiCliente"),       Width = new DataGridLength(100) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Nombre",   Binding = new System.Windows.Data.Binding("NombreCliente"),   Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Teléfono", Binding = new System.Windows.Data.Binding("TelefonoCliente"), Width = new DataGridLength(100) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Ciudad",   Binding = new System.Windows.Data.Binding("CiudadCliente"),   Width = new DataGridLength(110) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Estado",   Binding = new System.Windows.Data.Binding("EstadoTexto"),     Width = new DataGridLength(80) });
        _grid.MouseDoubleClick += (_, _) => Seleccionar();
        Grid.SetRow(_grid, 1); root.Children.Add(_grid);

        // ── Botones ──
        var barBtns = new Border {
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(229, 231, 235)),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(8, 8, 8, 8)
        };
        var btnsSp = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var lblConteo = new TextBlock {
            FontSize = 11, Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(107, 114, 128)),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(4, 0, 0, 0)
        };
        _grid.ItemsSource = _todos; // inicializar vacío
        _grid.LoadingRow += (_, e) => { /* se actualiza abajo */ };

        var barGrid = new Grid();
        barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Button MkBtn(string txt, string hex) => new Button {
            Content = txt, Height = 32, Padding = new Thickness(16, 0, 16, 0),
            Margin = new Thickness(6, 0, 0, 0), FontWeight = FontWeights.SemiBold,
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };

        var btnSelec  = MkBtn("✔  Seleccionar", "#22C55E");
        var btnCerrar = MkBtn("✕  Cerrar",       "#6B7280");

        btnSelec.Click  += (_, _) => Seleccionar();
        btnCerrar.Click += (_, _) => { DialogResult = false; Close(); };

        btnsSp.Children.Add(btnSelec);
        btnsSp.Children.Add(btnCerrar);

        Grid.SetColumn(lblConteo, 0);
        Grid.SetColumn(btnsSp, 1);
        barGrid.Children.Add(lblConteo);
        barGrid.Children.Add(btnsSp);
        barBtns.Child = barGrid;
        Grid.SetRow(barBtns, 2); root.Children.Add(barBtns);

        // Guardar referencia al label de conteo para actualizarlo
        _grid.ItemsSource = null;
        _grid.Tag = lblConteo;

        Content = root;
        Loaded += (_, _) => _txtBuscar.Focus();
    }

    private static Style BuildHeaderStyle()
    {
        var s = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        s.Setters.Add(new Setter(Control.BackgroundProperty,
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 140, 0))));
        s.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
        s.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        s.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 6, 8, 6)));
        s.Setters.Add(new Setter(Control.FontSizeProperty, 11.5));
        return s;
    }

    private async Task CargarAsync()
    {
        _todos = (await _repo.BuscarAsync(string.Empty)).ToList();
        ActualizarGrid(_todos);
    }

    private void Filtrar()
    {
        var q = _txtBuscar.Text.Trim().ToLowerInvariant();
        var lista = string.IsNullOrEmpty(q)
            ? _todos
            : _todos.Where(c =>
                c.CiCliente.Contains(q, StringComparison.OrdinalIgnoreCase)     ||
                c.NombreCliente.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (c.RucCliente?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
              .ToList();
        ActualizarGrid(lista);
    }

    private void ActualizarGrid(List<Cliente> lista)
    {
        _grid.ItemsSource = lista;
        _grid.SelectedItem = null;
        if (_grid.Tag is TextBlock lbl)
            lbl.Text = $"{lista.Count} cliente{(lista.Count != 1 ? "s" : "")} encontrado{(lista.Count != 1 ? "s" : "")}";
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
}
