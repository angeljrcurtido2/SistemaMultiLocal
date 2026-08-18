using CrediSoft.Core.Services;
using CrediSoft.Data;
using CrediSoft.Data.Repositories;
using CrediSoft.UI.Views.Informes;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CrediSoft.UI.Views.Caja;

// ── Apertura de Caja ──────────────────────────────────────────────────────────

public class CajaAperturaWindow : Window
{
    private readonly ICajaRepository      _caja;
    private readonly IDbConnectionFactory _db;
    private readonly ISessionService      _session;
    private PasswordBox _pwdClave   = null!;
    private TextBox   _txtEfectivo  = null!;
    private List<UsuarioItemPublic> _usuarios = new();
    private List<LocalItem> _locales = new();
    private LocalItem? _localSeleccionado;

    // Card de usuario seleccionado
    private UsuarioItemPublic? _usuarioSeleccionado;
    private Border    _cardUsuario     = null!;
    private TextBlock _tbUserNombre    = null!;
    private TextBlock _tbUserCodigo    = null!;
    private TextBlock _tbUserPlaceholder = null!;

    private static System.Windows.Media.SolidColorBrush RB(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    public CajaAperturaWindow()
    {
        _caja    = App.Services.GetRequiredService<ICajaRepository>();
        _db      = App.Services.GetRequiredService<IDbConnectionFactory>();
        _session = SessionService.Instance;
        Title    = "Apertura de Caja";
        Width = 520; Height = 480;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = RB("#F5F5F5");
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        Loaded += async (_, _) => await Inicializar();
    }

    public record UsuarioItemPublic(int Id, string Nombre, string Codigo)
    {
        public string CodigoDisplay => $"Código: {Codigo}";
    }
    private record UsuarioItem(int Id, string Nombre, string Codigo);

    private async Task Inicializar()
    {
        using (var conn = _db.Create())
        {
            _locales = (await conn.QueryAsync<LocalItem>(
                "SELECT ID_LOCAL AS Id, NOMBRE AS Nombre FROM LOCALES ORDER BY ID_LOCAL")).ToList();
        }

        // Pre-seleccionar el local de la sesión
        _localSeleccionado = _locales.FirstOrDefault(l => l.Id == _session.LocalActual!.IdLocal)
                             ?? _locales.FirstOrDefault();

        // Solo un ADMINISTRADOR puede elegir abrir caja de un local distinto al suyo.
        // Un usuario normal va directo al formulario de apertura de SU local — nunca ve la
        // lista de otros locales ni, por extension, la lista de usuarios de esos locales
        // (la carga de usuarios ya filtra por LOCAL_USUARIO del local elegido; si nunca se
        // permite elegir otro local, nunca se cargan usuarios ajenos al propio).
        var esAdmin = _session.UsuarioActual?.EsAdministrador == true;
        if (!esAdmin && _localSeleccionado != null)
        {
            await AbrirFormularioParaLocalAsync(_localSeleccionado);
            return;
        }

        BuildSelectorLocal();
    }

    // Salta el paso de "elegir local" e inicia directamente el formulario de apertura
    // para el local indicado (mismo flujo que dispara btnSig.Click en BuildSelectorLocal).
    private async Task AbrirFormularioParaLocalAsync(LocalItem local)
    {
        _localSeleccionado = local;

        var cajaAbierta = await _caja.ObtenerCajaAbiertaAsync(local.Id);
        if (cajaAbierta != null) { BuildUICajaAbierta(cajaAbierta); return; }

        await CargarUsuariosDelLocalAsync(local.Id);

        CrediSoft.Core.Models.CajaMaster? ultimoCierre;
        using (var conn = _db.Create())
            ultimoCierre = await conn.QueryFirstOrDefaultAsync<CrediSoft.Core.Models.CajaMaster>(
                @"SELECT TOP 1 M.ID_MASTER as IdMaster, M.FECHA_CIERRE as FechaCierre,
                    M.TOT_INGRESOS as TotIngresos, M.TOT_EGRESOS as TotEgresos,
                    M.MONTO_CIERRE_REAL as MontoBase, U.NOMBRE_USUARIO as NombreCajero
                  FROM CAJA_MASTER M
                  INNER JOIN USUARIOS U ON U.ID_USUARIO = M.ID_USUARIO_APE
                  WHERE M.ID_LOCAL = @loc AND M.ESTADO = 'C'
                  ORDER BY M.ID_MASTER DESC",
                new { loc = local.Id });
        BuildUI(ultimoCierre);
    }

    // Se llama cada vez que ya se conoce definitivamente el local de apertura (el usuario pudo
    // haber elegido uno distinto al de su sesión en el paso anterior). Solo trae los usuarios
    // que pertenecen a ESE local (LOCAL_USUARIO) — antes traia todos los usuarios del sistema.
    private async Task CargarUsuariosDelLocalAsync(int idLocal)
    {
        using var conn = _db.Create();
        _usuarios = (await conn.QueryAsync<UsuarioItemPublic>(
            "SELECT ID_USUARIO as Id, NOMBRE_USUARIO as Nombre, CODIGO_USUARIO as Codigo " +
            "FROM USUARIOS WHERE LOCAL_USUARIO = @loc ORDER BY NOMBRE_USUARIO",
            new { loc = idLocal })).ToList();
    }

    private void BuildSelectorLocal()
    {
        Width = 480; Height = 440 + Math.Min(_locales.Count, 4) * 10;
        ResizeMode = ResizeMode.NoResize;

        var root = new Grid { Background = RB("#F4F6FA") };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(130) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(72) });

        // ── Header ────────────────────────────────────────────────────────────
        var hdr = new Border {
            Background = new System.Windows.Media.LinearGradientBrush(
                System.Windows.Media.Color.FromRgb(25, 118, 210),
                System.Windows.Media.Color.FromRgb(13, 71, 161), 90)
        };

        // Círculo decorativo
        var canvas = new System.Windows.Controls.Canvas { ClipToBounds = true };
        void Circ(double x, double y, double r, string color, double op) {
            var e = new System.Windows.Shapes.Ellipse {
                Width = r*2, Height = r*2,
                Fill = RB(color), Opacity = op
            };
            System.Windows.Controls.Canvas.SetLeft(e, x - r);
            System.Windows.Controls.Canvas.SetTop(e,  y - r);
            canvas.Children.Add(e);
        }
        Circ(400, -20, 90, "#FFFFFF", 0.06);
        Circ(420,  80, 55, "#FFFFFF", 0.04);
        Circ( 30, 110, 70, "#FFFFFF", 0.04);
        hdr.Child = canvas;

        // Contenido header sobre el canvas
        var hdrContent = new Grid { Margin = new Thickness(28, 0, 28, 0) };
        hdrContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hdrContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Ícono "caja registradora" usando Segoe MDL2 Assets (sin emoji)
        var iconBg = new Border {
            Width = 52, Height = 52, CornerRadius = new CornerRadius(26),
            Background = System.Windows.Media.Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 18, 0)
        };
        iconBg.Child = new TextBlock {
            Text = "",   // Store icon — Segoe MDL2 Assets
            FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
            FontSize = 24, Foreground = RB("#0D47A1"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center
        };
        Grid.SetColumn(iconBg, 0); hdrContent.Children.Add(iconBg);

        var hdrTexts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        hdrTexts.Children.Add(new TextBlock {
            Text = "APERTURA DE CAJA",
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 18, FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 4)
        });
        hdrTexts.Children.Add(new TextBlock {
            Text = DateTime.Now.ToString("dddd d 'de' MMMM, yyyy",
                System.Globalization.CultureInfo.GetCultureInfo("es-PY")),
            Foreground = RB("#BBDEFB"), FontSize = 11.5
        });
        Grid.SetColumn(hdrTexts, 1); hdrContent.Children.Add(hdrTexts);

        // Superponer el contenido sobre el canvas
        var hdrOverlay = new Grid();
        hdrOverlay.Children.Add(hdr);
        hdrOverlay.Children.Add(hdrContent);
        Grid.SetRow(hdrOverlay, 0); root.Children.Add(hdrOverlay);

        // ── Cuerpo ────────────────────────────────────────────────────────────
        var body = new Border {
            Background = RB("#F4F6FA"),
            Padding = new Thickness(28, 22, 28, 16)
        };
        var bodySp = new StackPanel();

        bodySp.Children.Add(new TextBlock {
            Text = "¿En qué local se realizará la apertura?",
            FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = RB("#1A237E"), Margin = new Thickness(0, 0, 0, 16)
        });

        // Cards
        var cardsSp = new StackPanel();
        Border? selCard = null;
        List<(Border ring, Border dot)> radioRings = new();

        void SelectCard(Border card, Border ring, Border dot, LocalItem local)
        {
            if (selCard != null)
            {
                selCard.BorderBrush = RB("#E0E6EF");
                selCard.Background  = System.Windows.Media.Brushes.White;
                selCard.Effect      = new System.Windows.Media.Effects.DropShadowEffect {
                    BlurRadius = 4, ShadowDepth = 1, Opacity = 0.06,
                    Color = System.Windows.Media.Colors.Black };
            }
            foreach (var (r, d) in radioRings)
            {
                r.BorderBrush = RB("#B0BEC5");
                d.Visibility  = Visibility.Collapsed;
            }
            card.BorderBrush = RB("#1976D2");
            card.Background  = RB("#EBF3FB");
            card.Effect      = new System.Windows.Media.Effects.DropShadowEffect {
                BlurRadius = 10, ShadowDepth = 2, Opacity = 0.13,
                Color = System.Windows.Media.Color.FromRgb(21, 101, 192) };
            ring.BorderBrush = RB("#1976D2");
            dot.Visibility   = Visibility.Visible;
            selCard            = card;
            _localSeleccionado = local;
        }

        foreach (var local in _locales)
        {
            var card = new Border {
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = RB("#E0E6EF"), BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(0, 0, 0, 10),
                Cursor = Cursors.Hand
            };
            card.Effect = new System.Windows.Media.Effects.DropShadowEffect {
                BlurRadius = 4, ShadowDepth = 1, Opacity = 0.06,
                Color = System.Windows.Media.Colors.Black };

            var cg = new Grid();
            cg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            cg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Radio button manual
            var ring = new Border {
                Width = 20, Height = 20, CornerRadius = new CornerRadius(10),
                BorderBrush = RB("#B0BEC5"), BorderThickness = new Thickness(2),
                Background = System.Windows.Media.Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 14, 0)
            };
            var dot = new Border {
                Width = 10, Height = 10, CornerRadius = new CornerRadius(5),
                Background = RB("#1976D2"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            ring.Child = dot;
            radioRings.Add((ring, dot));
            Grid.SetColumn(ring, 0); cg.Children.Add(ring);

            // Nombre
            var nombre = new TextBlock {
                Text = local.Nombre, FontSize = 13.5, FontWeight = FontWeights.SemiBold,
                Foreground = RB("#1C2B3A"), VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(nombre, 1); cg.Children.Add(nombre);

            // Chip N° local
            var chip = new Border {
                Background = RB("#E8F0FE"), CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 4, 10, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
            chip.Child = new TextBlock {
                Text = $"N° {local.Id}", FontSize = 10.5,
                Foreground = RB("#1565C0"), FontWeight = FontWeights.Bold
            };
            Grid.SetColumn(chip, 2); cg.Children.Add(chip);
            card.Child = cg;

            var lc = local; var cc = card;
            var rr = ring;  var dd = dot;
            card.MouseLeftButtonUp += (_, _) => SelectCard(cc, rr, dd, lc);
            cardsSp.Children.Add(card);

            if (local.Id == _localSeleccionado?.Id)
                SelectCard(card, ring, dot, local);
        }

        var scroll = new ScrollViewer {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 260, Content = cardsSp
        };
        bodySp.Children.Add(scroll);
        body.Child = bodySp;
        Grid.SetRow(body, 1); root.Children.Add(body);

        // ── Pie ───────────────────────────────────────────────────────────────
        var pie = new Border {
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = RB("#E0E6EF"), BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(24, 0, 24, 0)
        };
        var pieSp = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };

        var btnCer = new Button {
            Content = "Cancelar", Height = 38, Padding = new Thickness(22, 0, 22, 0),
            Background = System.Windows.Media.Brushes.White,
            Foreground = RB("#546E7A"), FontSize = 13, FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(1.5), BorderBrush = RB("#CFD8DC"),
            Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 12, 0)
        };

        var btnSig = new Button {
            Height = 38, Padding = new Thickness(26, 0, 26, 0),
            Background = RB("#1565C0"), Foreground = System.Windows.Media.Brushes.White,
            FontSize = 13, FontWeight = FontWeights.Bold,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            Content = "Continuar  →"
        };

        btnCer.Click += (_, _) => Close();
        pieSp.Children.Add(btnCer); pieSp.Children.Add(btnSig);
        pie.Child = pieSp;
        Grid.SetRow(pie, 2); root.Children.Add(pie);

        Content = root;

        btnSig.Click += async (_, _) => {
            if (_localSeleccionado == null) return;
            await AbrirFormularioParaLocalAsync(_localSeleccionado);
        };
    }

    private void BuildUICajaAbierta(CrediSoft.Core.Models.CajaMaster caja)
    {
        Width = 480; Height = 400;
        var root = new DockPanel { Background = RB("#F5F5F5") };

        // header azul corporativo
        var hdr = new Border { Background = RB("#0E2F44"), Padding = new Thickness(18,12,18,12) };
        hdr.Child = new TextBlock { Text = "APERTURA DE CAJA",
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 16, FontWeight = FontWeights.Bold };
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        // pie cerrar
        var pie = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = RB("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(16,10,16,10) };
        var btnCerrar = new Button { Content = "✖  Cerrar", Height = 36,
            Padding = new Thickness(20,0,20,0), Background = RB("#546E7A"),
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.SemiBold,
            FontSize = 13, BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Right };
        btnCerrar.Click += (_, _) => Close();
        pie.Child = btnCerrar;
        DockPanel.SetDock(pie, Dock.Bottom); root.Children.Add(pie);

        // cuerpo: info de caja abierta
        var body = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = RB("#BDBDBD"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4), Margin = new Thickness(20,16,20,16),
            Padding = new Thickness(16,14,16,14) };
        var sp = new StackPanel();

        // ícono + mensaje
        var nombreLocalMsg = _localSeleccionado?.Nombre ?? _session.LocalActual!.NombreLocal;
        var idLocalMsg     = _localSeleccionado?.Id ?? _session.LocalActual!.IdLocal;
        var topSp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,14) };
        topSp.Children.Add(new TextBlock { Text = "✔", FontSize = 22,
            Foreground = RB("#1565C0"), Margin = new Thickness(0,0,10,0),
            VerticalAlignment = VerticalAlignment.Center });
        // MaxWidth explícito — un StackPanel horizontal mide a sus hijos con ancho infinito
        // disponible, así que TextWrapping.Wrap por sí solo no alcanza para que el TextBlock
        // sepa dónde cortar: el texto largo quedaba en una sola línea, recortado visualmente
        // por el borde del diálogo en vez de bajar de línea.
        var msgSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, MaxWidth = 380 };
        msgSp.Children.Add(new TextBlock {
            Text = $"Tu local {nombreLocalMsg} (Local N.º {idLocalMsg}) ya cuenta con una apertura de caja activa.",
            FontSize = 13, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap });
        msgSp.Children.Add(new TextBlock { Text = "Podés operar directamente — no es necesario abrir una nueva.",
            FontSize = 11, Foreground = RB("#757575"), Margin = new Thickness(0,2,0,0), TextWrapping = TextWrapping.Wrap });
        topSp.Children.Add(msgSp);
        sp.Children.Add(topSp);

        // separador
        sp.Children.Add(new Border { BorderBrush = RB("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0),
            Margin = new Thickness(0,0,0,12) });

        // datos de la apertura
        var dataG = new Grid();
        dataG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        dataG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        void DI(string lbl, string val, int col, int row) {
            if (dataG.RowDefinitions.Count <= row)
                dataG.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var s = new StackPanel { Margin = new Thickness(0,0,0,6) };
            s.Children.Add(new TextBlock { Text = lbl, FontSize = 10,
                Foreground = RB("#757575"), FontWeight = FontWeights.SemiBold });
            s.Children.Add(new TextBlock { Text = val, FontSize = 12, FontWeight = FontWeights.Bold });
            Grid.SetColumn(s, col); Grid.SetRow(s, row); dataG.Children.Add(s);
        }
        var nombreCajero = !string.IsNullOrWhiteSpace(caja.NombreCajero) ? caja.NombreCajero
                          : !string.IsNullOrWhiteSpace(caja.UsuarioAperturanombre) ? caja.UsuarioAperturanombre
                          : "(no disponible)";
        DI("ABIERTA POR",    nombreCajero, 0, 0);
        DI("FECHA APERTURA", caja.FechaApertura.ToString("dd/MM/yyyy"), 1, 0);
        DI("HORA APERTURA",  caja.FechaApertura.ToString("HH:mm:ss"), 0, 1);
        DI("BASE EN CAJA",   $"Gs. {caja.MontoBase:N0}", 1, 1);

        var transcurrido = DateTime.Now - caja.FechaApertura;
        var txtTranscurrido = transcurrido.TotalHours >= 1
            ? $"Hace {(int)transcurrido.TotalHours} h {transcurrido.Minutes} min"
            : $"Hace {transcurrido.Minutes} min";
        DI("ABIERTA DESDE HACE", txtTranscurrido, 0, 2);

        sp.Children.Add(dataG);

        body.Child = sp;
        root.Children.Add(body);
        Content = root;
    }

    private void BuildUI(CrediSoft.Core.Models.CajaMaster? cierre)
    {
        var root = new DockPanel { Background = RB("#F5F5F5") };

        // ── Header ────────────────────────────────────────────────────────────
        var hdr = new Border { Background = RB("#1565C0"), Padding = new Thickness(18, 12, 18, 12) };
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock { Text = "APERTURA DE CAJA",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 16, FontWeight = FontWeights.Bold });
        hdrSp.Children.Add(new TextBlock {
            Text = _localSeleccionado?.Nombre ?? "",
            Foreground = RB("#90CAF9"), FontSize = 12 });
        hdr.Child = hdrSp;
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        // ── Pie botones ───────────────────────────────────────────────────────
        var pie = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = RB("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(16,10,16,10) };
        var pieSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        Button MkBtn(string txt, string bg) {
            var b = new Button { Content = txt, Height = 36, Padding = new Thickness(18,0,18,0),
                Background = RB(bg), Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.SemiBold, FontSize = 13,
                BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
                Margin = new Thickness(6,0,0,0) };
            return b;
        }
        var btnGuardar  = MkBtn("Guardar", "#1B5E20");
        var btnCerrar   = MkBtn("✖  Cerrar",  "#546E7A");
        btnGuardar.Click += async (_, _) => await Guardar();
        btnCerrar.Click  += (_, _) => Close();
        pieSp.Children.Add(btnGuardar); pieSp.Children.Add(btnCerrar);
        pie.Child = pieSp;
        DockPanel.SetDock(pie, Dock.Bottom); root.Children.Add(pie);

        // ── Cuerpo ────────────────────────────────────────────────────────────
        var body = new StackPanel { Margin = new Thickness(20, 14, 20, 14) };

        // Sección: último cierre
        if (cierre != null) {
            var secCierre = new Border { Background = System.Windows.Media.Brushes.White,
                BorderBrush = RB("#BDBDBD"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4), Padding = new Thickness(14,10,14,10),
                Margin = new Thickness(0,0,0,14) };
            var cierreG = new Grid();
            cierreG.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cierreG.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cierreG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cierreG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var tit = new TextBlock { Text = "ÚLTIMO CIERRE DE CAJA",
                FontSize = 10, FontWeight = FontWeights.Bold, Foreground = RB("#1565C0"),
                Margin = new Thickness(0,0,0,8) };
            Grid.SetColumnSpan(tit, 2); cierreG.Children.Add(tit);

            var infoG = new Grid(); Grid.SetRow(infoG, 1); Grid.SetColumnSpan(infoG, 2);
            infoG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            infoG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            void CI(string l, string v, int col, int row = 0) {
                var sp = new StackPanel { Margin = new Thickness(0,2,0,2) };
                Grid.SetColumn(sp, col); Grid.SetRow(sp, row);
                sp.Children.Add(new TextBlock { Text = l, FontSize = 10, Foreground = RB("#757575") });
                sp.Children.Add(new TextBlock { Text = v, FontSize = 12, FontWeight = FontWeights.SemiBold });
                infoG.Children.Add(sp);
            }
            infoG.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            infoG.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            CI("Fecha", cierre.FechaCierre?.ToString("dd/MM/yyyy HH:mm") ?? "-", 0, 0);
            CI("Ingreso", $"Gs. {cierre.TotIngresos:N0}", 1, 0);
            CI("Salida", $"Gs. {cierre.TotEgresos:N0}", 0, 1);
            CI("T. Caja", $"Gs. {cierre.MontoBase:N0}", 1, 1);
            cierreG.Children.Add(infoG);
            secCierre.Child = cierreG;
            body.Children.Add(secCierre);
        }

        // Sección: nueva apertura
        var secAper = new Border {
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = RB("#E3EAF4"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18, 14, 18, 18)
        };
        secAper.Effect = new System.Windows.Media.Effects.DropShadowEffect {
            BlurRadius = 8, ShadowDepth = 1, Opacity = 0.07,
            Color = System.Windows.Media.Color.FromRgb(0,0,0)
        };
        var aperSp = new StackPanel { };

        // Título sección
        var titRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,14) };
        titRow.Children.Add(new Border {
            Width = 3, CornerRadius = new CornerRadius(2),
            Background = RB("#1565C0"), Margin = new Thickness(0,0,8,0)
        });
        titRow.Children.Add(new TextBlock {
            Text = "NUEVA APERTURA DE CAJA",
            FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = RB("#1565C0"),
            VerticalAlignment = VerticalAlignment.Center
        });
        aperSp.Children.Add(titRow);

        // ── Fila fecha ───────────────────────────────────────────────────────
        var lblFecha = new TextBlock { Text = "FECHA", FontSize = 10, FontWeight = FontWeights.SemiBold,
            Foreground = RB("#546E7A"), Margin = new Thickness(0,0,0,3) };
        aperSp.Children.Add(lblFecha);
        var fechaBorder = new Border {
            BorderBrush = RB("#CFD8DC"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5), Padding = new Thickness(10,8,10,8),
            Background = RB("#F8FAFB"), Margin = new Thickness(0,0,0,14)
        };
        fechaBorder.Child = new TextBlock {
            Text = DateTime.Now.ToString("dd/MM/yyyy"),
            FontSize = 13, FontWeight = FontWeights.Bold, Foreground = RB("#1A237E")
        };
        aperSp.Children.Add(fechaBorder);

        // ── Usuario: card de selección ───────────────────────────────────────
        aperSp.Children.Add(new TextBlock { Text = "CAJERO / USUARIO", FontSize = 10,
            FontWeight = FontWeights.SemiBold, Foreground = RB("#546E7A"),
            Margin = new Thickness(0,0,0,6) });

        // Card usuario (vacío = placeholder)
        _cardUsuario = new Border {
            BorderBrush = RB("#B0BEC5"), BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(7), Padding = new Thickness(12,10,12,10),
            Background = RB("#F8FAFB"), Margin = new Thickness(0,0,0,8),
            Cursor = Cursors.Hand
        };
        var cardInner = new Grid();
        cardInner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        cardInner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cardInner.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Avatar círculo
        var avatar = new Border {
            Width = 36, Height = 36, CornerRadius = new CornerRadius(18),
            Background = RB("#E3EAF4"), VerticalAlignment = VerticalAlignment.Center
        };
        var avatarTxt = new TextBlock {
            Text = "👤", FontSize = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        avatar.Child = avatarTxt;
        Grid.SetColumn(avatar, 0); cardInner.Children.Add(avatar);

        // Nombre + código (se actualizan al seleccionar)
        var userInfoSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10,0,0,0) };
        _tbUserPlaceholder = new TextBlock {
            Text = "Haga clic para seleccionar un usuario",
            FontSize = 12, Foreground = RB("#90A4AE"), FontStyle = FontStyles.Italic
        };
        _tbUserNombre = new TextBlock {
            FontSize = 13, FontWeight = FontWeights.Bold, Foreground = RB("#1A237E"),
            Visibility = Visibility.Collapsed
        };
        _tbUserCodigo = new TextBlock {
            FontSize = 10.5, Foreground = RB("#546E7A"),
            Visibility = Visibility.Collapsed
        };
        userInfoSp.Children.Add(_tbUserPlaceholder);
        userInfoSp.Children.Add(_tbUserNombre);
        userInfoSp.Children.Add(_tbUserCodigo);
        Grid.SetColumn(userInfoSp, 1); cardInner.Children.Add(userInfoSp);

        // Botón buscar
        var btnBuscarUser = new Button {
            Content = "🔍  Buscar",
            Height = 30, Padding = new Thickness(12,0,12,0),
            Background = RB("#1565C0"), Foreground = System.Windows.Media.Brushes.White,
            FontSize = 11.5, FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center
        };
        btnBuscarUser.Click += (_, _) => AbrirModalUsuario();
        _cardUsuario.MouseLeftButtonUp += (_, _) => AbrirModalUsuario();
        Grid.SetColumn(btnBuscarUser, 2); cardInner.Children.Add(btnBuscarUser);

        _cardUsuario.Child = cardInner;
        aperSp.Children.Add(_cardUsuario);

        // ── Contraseña ───────────────────────────────────────────────────────
        aperSp.Children.Add(new TextBlock { Text = "CONTRASEÑA", FontSize = 10,
            FontWeight = FontWeights.SemiBold, Foreground = RB("#546E7A"),
            Margin = new Thickness(0,0,0,6) });

        var pwdBorder = new Border {
            BorderBrush = RB("#B0BEC5"), BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(6), Margin = new Thickness(0,0,0,14)
        };
        _pwdClave = new PasswordBox {
            Padding = new Thickness(10,9,10,9), FontSize = 13,
            BorderThickness = new Thickness(0), Background = System.Windows.Media.Brushes.Transparent
        };
        _pwdClave.KeyDown += (_, e) => { if (e.Key == Key.Enter) _ = Guardar(); };
        pwdBorder.Child = _pwdClave;
        aperSp.Children.Add(pwdBorder);

        // ── Efectivo en caja ─────────────────────────────────────────────────
        aperSp.Children.Add(new TextBlock { Text = "EFECTIVO EN CAJA (Gs.)", FontSize = 10,
            FontWeight = FontWeights.SemiBold, Foreground = RB("#546E7A"),
            Margin = new Thickness(0,0,0,6) });

        var efBorder = new Border {
            BorderBrush = RB("#B0BEC5"), BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(6)
        };
        _txtEfectivo = new TextBox {
            Padding = new Thickness(10,9,10,9), FontSize = 14,
            FontWeight = FontWeights.Bold, BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent,
            HorizontalContentAlignment = HorizontalAlignment.Right,
            Foreground = RB("#1B5E20")
        };
        _txtEfectivo.Text = "0";
        bool fmtBusy = false;
        _txtEfectivo.TextChanged += (_, _) => {
            if (fmtBusy) return; fmtBusy = true;
            var raw = _txtEfectivo.Text.Replace(".","").Replace(",","").Trim();
            if (long.TryParse(raw, out var n)) {
                var fmt = n.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-PY"));
                _txtEfectivo.Text = fmt; _txtEfectivo.CaretIndex = fmt.Length;
            }
            fmtBusy = false;
        };
        efBorder.Child = _txtEfectivo;
        aperSp.Children.Add(efBorder);

        secAper.Child = aperSp;
        body.Children.Add(secAper);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = body };
        root.Children.Add(scroll);
        Content = root;
    }

    private void AbrirModalUsuario()
    {
        var modal = new BuscarUsuarioCajaWindow(_usuarios) { Owner = this };
        if (modal.ShowDialog() == true && modal.UsuarioSeleccionado != null)
        {
            _usuarioSeleccionado = modal.UsuarioSeleccionado;
            _tbUserNombre.Text = _usuarioSeleccionado.Nombre;
            _tbUserCodigo.Text = _usuarioSeleccionado.CodigoDisplay;
            _tbUserPlaceholder.Visibility = Visibility.Collapsed;
            _tbUserNombre.Visibility      = Visibility.Visible;
            _tbUserCodigo.Visibility      = Visibility.Visible;
            _cardUsuario.BorderBrush = RB("#1565C0");
            _cardUsuario.Background  = RB("#EEF4FB");
            _pwdClave.Focus();
        }
    }

    private async Task Guardar()
    {
        if (_usuarioSeleccionado == null) {
            MessageBox.Show("Seleccione un usuario.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        if (string.IsNullOrWhiteSpace(_pwdClave.Password)) {
            MessageBox.Show("Ingrese la contraseña.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }

        // validar credenciales
        using var conn = _db.Create();
        var idUsuario = await conn.QueryFirstOrDefaultAsync<int?>(
            "SELECT ID_USUARIO FROM USUARIOS WHERE ID_USUARIO=@id AND CONTRASEÑA_USUARIO=@clave",
            new { id = _usuarioSeleccionado!.Id, clave = _pwdClave.Password });
        if (idUsuario == null) {
            MessageBox.Show("Contraseña incorrecta.", "Acceso denegado", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }

        var raw = _txtEfectivo.Text.Replace(".","").Replace(",","");
        if (!decimal.TryParse(raw, out var monto) || monto < 0) {
            MessageBox.Show("Ingrese un monto de efectivo válido.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }

        await _caja.AbrirCajaAsync(_localSeleccionado!.Id, idUsuario.Value, monto);
        MessageBox.Show($"¡Caja abierta correctamente!\nCajero: {_usuarioSeleccionado.Nombre}\nFondo: Gs. {monto:N0}",
            "ElectroMar", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }
}

// ── Modal de búsqueda de usuario para Apertura de Caja ───────────────────────

public class BuscarUsuarioCajaWindow : Window
{
    private readonly List<CajaAperturaWindow.UsuarioItemPublic> _todos;
    private TextBox  _txtBuscar = null!;
    private System.Windows.Controls.ListBox _lista = null!;

    public CajaAperturaWindow.UsuarioItemPublic? UsuarioSeleccionado { get; private set; }

    private static System.Windows.Media.SolidColorBrush RB(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    public BuscarUsuarioCajaWindow(List<CajaAperturaWindow.UsuarioItemPublic> usuarios)
    {
        _todos = usuarios;
        Title  = "Seleccionar usuario";
        Width  = 420; Height = 500;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        Background = RB("#F0F4F8");
        BuildUI();
    }

    private void BuildUI()
    {
        var root = new DockPanel();

        // ── Header ───────────────────────────────────────────────────────────
        var hdr = new Border {
            Background = RB("#1565C0"), Padding = new Thickness(16,12,16,12)
        };
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock {
            Text = "👤  Seleccionar cajero",
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,8)
        });

        // Caja búsqueda en el header
        var searchBorder = new Border {
            Background = RB("#0D47A1"), CornerRadius = new CornerRadius(6),
            BorderBrush = RB("#42A5F5"), BorderThickness = new Thickness(1),
            Padding = new Thickness(10,0,8,0)
        };
        var searchRow = new StackPanel { Orientation = Orientation.Horizontal };
        searchRow.Children.Add(new TextBlock {
            Text = "🔎", FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
            Foreground = RB("#90CAF9"), Margin = new Thickness(0,0,8,0)
        });
        _txtBuscar = new TextBox {
            Height = 32, MinWidth = 260, FontSize = 12.5,
            Background = System.Windows.Media.Brushes.Transparent,
            Foreground = System.Windows.Media.Brushes.White,
            CaretBrush  = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _txtBuscar.TextChanged += (_, _) => Filtrar();
        _txtBuscar.KeyDown     += (_, e) => {
            if (e.Key == Key.Enter || e.Key == Key.Down) { _lista.Focus(); if (_lista.Items.Count > 0) _lista.SelectedIndex = 0; }
        };
        searchRow.Children.Add(_txtBuscar);
        searchBorder.Child = searchRow;
        hdrSp.Children.Add(searchBorder);
        hdr.Child = hdrSp;
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        // ── Footer ───────────────────────────────────────────────────────────
        var footer = new Border {
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = RB("#CFD8DC"), BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(14,10,14,10)
        };
        var footSp = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right };

        Button MkB(string txt, string bg) => new Button {
            Content = txt, Height = 34, Padding = new Thickness(18,0,18,0),
            Margin = new Thickness(6,0,0,0),
            Background = RB(bg), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, FontSize = 12.5,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };

        var btnSeleccionar = MkB("✔  Seleccionar", "#1B5E20");
        var btnCerrar      = MkB("✕  Cancelar",    "#546E7A");
        btnSeleccionar.Click += (_, _) => Aceptar();
        btnCerrar.Click      += (_, _) => { DialogResult = false; Close(); };
        footSp.Children.Add(btnSeleccionar);
        footSp.Children.Add(btnCerrar);
        footer.Child = footSp;
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);

        // ── Lista de usuarios ─────────────────────────────────────────────────
        var listWrap = new Border {
            Margin = new Thickness(12,10,12,8),
            BorderBrush = RB("#CFD8DC"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), ClipToBounds = true,
            Background = System.Windows.Media.Brushes.White
        };

        _lista = new System.Windows.Controls.ListBox {
            BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(_lista, ScrollBarVisibility.Disabled);

        // ItemContainerStyle: hover + selección en azul corporativo
        var itemStyle = new Style(typeof(System.Windows.Controls.ListBoxItem));
        itemStyle.Setters.Add(new Setter(System.Windows.Controls.ListBoxItem.PaddingProperty,   new Thickness(12,10,12,10)));
        itemStyle.Setters.Add(new Setter(System.Windows.Controls.ListBoxItem.BorderThicknessProperty, new Thickness(0,0,0,1)));
        itemStyle.Setters.Add(new Setter(System.Windows.Controls.ListBoxItem.BorderBrushProperty, RB("#ECEFF1")));
        var trigSel = new Trigger { Property = System.Windows.Controls.ListBoxItem.IsSelectedProperty, Value = true };
        trigSel.Setters.Add(new Setter(System.Windows.Controls.ListBoxItem.BackgroundProperty, RB("#E3F2FD")));
        trigSel.Setters.Add(new Setter(System.Windows.Controls.ListBoxItem.ForegroundProperty, RB("#0D47A1")));
        itemStyle.Triggers.Add(trigSel);
        _lista.ItemContainerStyle = itemStyle;

        // ItemTemplate: avatar + nombre + código
        var dt = new DataTemplate();
        var rowFactory = new FrameworkElementFactory(typeof(StackPanel));
        rowFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

        var avFactory = new FrameworkElementFactory(typeof(Border));
        avFactory.SetValue(Border.WidthProperty, 34.0);
        avFactory.SetValue(Border.HeightProperty, 34.0);
        avFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(17));
        avFactory.SetValue(Border.BackgroundProperty, RB("#E3EAF4"));
        avFactory.SetValue(Border.MarginProperty, new Thickness(0,0,10,0));
        avFactory.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Center);
        var avTxt = new FrameworkElementFactory(typeof(TextBlock));
        avTxt.SetValue(TextBlock.TextProperty, "👤");
        avTxt.SetValue(TextBlock.FontSizeProperty, 15.0);
        avTxt.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        avTxt.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        avFactory.AppendChild(avTxt);
        rowFactory.AppendChild(avFactory);

        var infoFactory = new FrameworkElementFactory(typeof(StackPanel));
        infoFactory.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);
        var nomFactory = new FrameworkElementFactory(typeof(TextBlock));
        nomFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Nombre"));
        nomFactory.SetValue(TextBlock.FontSizeProperty, 13.0);
        nomFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        var codFactory = new FrameworkElementFactory(typeof(TextBlock));
        codFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("CodigoDisplay"));
        codFactory.SetValue(TextBlock.FontSizeProperty, 10.5);
        codFactory.SetValue(TextBlock.ForegroundProperty, RB("#78909C"));
        infoFactory.AppendChild(nomFactory);
        infoFactory.AppendChild(codFactory);
        rowFactory.AppendChild(infoFactory);

        dt.VisualTree = rowFactory;
        _lista.ItemTemplate = dt;
        _lista.MouseDoubleClick += (_, _) => Aceptar();
        _lista.KeyDown += (_, e) => { if (e.Key == Key.Enter) Aceptar(); };

        listWrap.Child = new ScrollViewer {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _lista
        };
        root.Children.Add(listWrap);

        Content = root;
        Loaded += (_, _) => { Filtrar(); _txtBuscar.Focus(); };
    }

    private void Filtrar()
    {
        var q = (_txtBuscar?.Text ?? "").Trim().ToLowerInvariant();
        var lista = string.IsNullOrEmpty(q)
            ? _todos
            : _todos.Where(u => u.Nombre.ToLowerInvariant().Contains(q) ||
                                u.Codigo.ToLowerInvariant().Contains(q)).ToList();
        _lista.ItemsSource = lista;
        if (lista.Count > 0) _lista.SelectedIndex = 0;
    }

    private void Aceptar()
    {
        if (_lista.SelectedItem is CajaAperturaWindow.UsuarioItemPublic u) {
            UsuarioSeleccionado = u;
            DialogResult = true;
            Close();
        } else {
            MessageBox.Show("Seleccione un usuario.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}

// ── Cierre de Caja ────────────────────────────────────────────────────────────

public class CajaCierreWindow : Window
{
    private readonly ICajaRepository     _caja;
    private readonly IDbConnectionFactory _db;
    private readonly ISessionService     _session;
    private int     _idMaster;
    private decimal _totalNetoActual;
    // Un TextBox + un TextBlock de diferencia por medio de pago — antes un solo "efectivo
    // contado" se comparaba contra el neto de TODOS los medios mezclados (efectivo + tarjeta +
    // transferencia + QR + cheque), generando faltantes/sobrantes falsos cada vez que había
    // ventas no-efectivo (ese dinero nunca puede estar físicamente en la mano del cajero).
    // Pedido explícito: declarar cada medio por separado para poder auditar bien qué cargó
    // cada cajero, aunque implique un cambio de hábito frente al campo único de antes.
    private readonly Dictionary<string, TextBox>   _txtContado    = new();
    private readonly Dictionary<string, TextBlock> _lblDifPorMedio = new();
    private TextBox _txtObs = null!;
    private TextBlock _lblIndicador = null!;
    private static readonly (string Clave, string Etiqueta)[] MediosPago = {
        ("EFECTIVO", "Efectivo"), ("TRANSFERENCIA", "Transferencia"), ("QR", "QR"),
        ("TARJETA", "Tarjeta"), ("CHEQUE", "Cheque")
    };
    // Comprobante de depósito — opcional en este momento, no bloquea el cierre. El dueño del
    // negocio pidió poder exigirlo pero con 48hs de plazo desde el cierre para completarlo
    // (ver CajaComprobantesPendientesWindow para la carga posterior).
    private TextBox    _txtNroComprobanteDep  = null!;
    private TextBlock  _lblEstadoFotoDep      = null!;
    private Image      _imgMiniaturaComprobanteDep = null!;
    private byte[]?    _fotoComprobanteDepSeleccionada;
    private List<FilaCierre> _movs = new();
    private LocalItem? _localSeleccionado;

    // totales calculados por forma de pago
    private record TotalesFP(
        decimal SaldoIni, decimal VentasEf, decimal VentasTar, decimal VentasTrans, decimal VentasCheq, decimal VentasOtro,
        decimal CobrosEf, decimal CobrosTar, decimal CobrosTrans, decimal CobrosCheq, decimal CobrosOtro,
        decimal IngrManEf, decimal IngrManTar, decimal IngrManTrans, decimal IngrManCheq, decimal IngrManOtro,
        decimal EgresosEf, decimal EgresosTar, decimal EgresosTrans, decimal EgresosCheq, decimal EgresosOtro,
        decimal AnticiposEf);

    private static System.Windows.Media.SolidColorBrush RB(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    public CajaCierreWindow()
    {
        _caja    = App.Services.GetRequiredService<ICajaRepository>();
        _db      = App.Services.GetRequiredService<IDbConnectionFactory>();
        _session = SessionService.Instance;
        Title    = "Cierre de caja";
        Width = 950; Height = 680;
        MinWidth = 900; MinHeight = 600;
        // CenterOwner queda descentrada hacia la derecha cuando el Owner (MainWindow) está
        // maximizado — bug conocido de WPF, calcula el centro con el tamaño restaurado en
        // vez del real. Se centra a mano contra el área de trabajo de la pantalla en su lugar.
        // Nota: BuildUI(CajaMaster) más abajo vuelve a fijar Width/Height/Left/Top una vez
        // que se conoce si hay caja abierta — este cálculo inicial es solo para las pantallas
        // intermedias (selector de local, "sin caja abierta", error) que no pasan por BuildUI.
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = (SystemParameters.WorkArea.Width  - Width)  / 2;
        Top  = (SystemParameters.WorkArea.Height - Height) / 2;
        Background = RB("#F5F5F5");
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        Loaded += async (_, _) => await Inicializar();
    }

    private async Task Inicializar()
    {
        try
        {
        // Paso 1: seleccionar local
        List<LocalItem> locales;
        using (var conn = _db.Create())
            locales = (await conn.QueryAsync<LocalItem>(
                "SELECT ID_LOCAL AS Id, NOMBRE AS Nombre FROM LOCALES ORDER BY ID_LOCAL")).ToList();

        _localSeleccionado = locales.FirstOrDefault(l => l.Id == _session.LocalActual!.IdLocal)
                             ?? locales.FirstOrDefault();

        // Solo un ADMINISTRADOR puede elegir cerrar la caja de un local distinto al suyo.
        // Un usuario normal va directo al cierre de SU local — nunca ve la lista de otros
        // locales (mismo criterio ya aplicado en CajaAperturaWindow.Inicializar).
        var esAdmin = _session.UsuarioActual?.EsAdministrador == true;
        if (!esAdmin && _localSeleccionado != null)
        {
            await CargarCierre();
            return;
        }

        await MostrarSelectorLocalCierre(locales);
    }
    catch (Exception ex)
    {
        var err = new StackPanel { Margin = new Thickness(32) };
        err.Children.Add(new TextBlock { Text = "Error:", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = RB("#0E2F44") });
        err.Children.Add(new TextBlock { Text = ex.Message, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,6,0,0) });
        var b = new Button { Content = "Cerrar", Width = 100, Height = 34, Margin = new Thickness(0,16,0,0) };
        b.Click += (_, _) => Close(); err.Children.Add(b); Content = err;
    }
    }

    private async Task MostrarSelectorLocalCierre(List<LocalItem> locales)
    {
        Width = 460; Height = 420; ResizeMode = ResizeMode.NoResize;

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(110) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(64) });

        // ── Header azul cierre ────────────────────────────────────────────────
        var hdrBorder = new Border();
        hdrBorder.Background = new System.Windows.Media.LinearGradientBrush(
            System.Windows.Media.Color.FromRgb(14, 47, 68),
            System.Windows.Media.Color.FromRgb(10, 33, 48), 90);
        var hdrGrid = new Grid { Margin = new Thickness(24, 0, 24, 0) };
        hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var iconCircle = new Border {
            Width = 48, Height = 48, CornerRadius = new CornerRadius(24),
            Background = RB("#FFFFFF22"), VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 14, 0)
        };
        iconCircle.Child = new TextBlock {
            Text = "🔒", FontSize = 22,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center
        };
        Grid.SetColumn(iconCircle, 0); hdrGrid.Children.Add(iconCircle);

        var hdrTexts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        hdrTexts.Children.Add(new TextBlock {
            Text = "CIERRE DE CAJA",
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 17, FontWeight = FontWeights.Bold
        });
        hdrTexts.Children.Add(new TextBlock {
            Text = DateTime.Now.ToString("dddd, dd 'de' MMMM yyyy",
                System.Globalization.CultureInfo.GetCultureInfo("es-PY")),
            Foreground = RB("#90CAF9"), FontSize = 11, Margin = new Thickness(0, 2, 0, 0)
        });
        Grid.SetColumn(hdrTexts, 1); hdrGrid.Children.Add(hdrTexts);
        hdrBorder.Child = hdrGrid;
        Grid.SetRow(hdrBorder, 0); root.Children.Add(hdrBorder);

        // ── Cuerpo cards ──────────────────────────────────────────────────────
        var bodyBorder = new Border {
            Background = RB("#F8FAFD"), Padding = new Thickness(24, 20, 24, 16)
        };
        var bodySp = new StackPanel();
        bodySp.Children.Add(new TextBlock {
            Text = "Seleccione el local",
            FontSize = 13, FontWeight = FontWeights.Bold,
            Foreground = RB("#0E2F44"), Margin = new Thickness(0, 0, 0, 4)
        });
        bodySp.Children.Add(new TextBlock {
            Text = "¿En qué sucursal desea realizar el cierre de caja?",
            FontSize = 11.5, Foreground = RB("#607D8B"), Margin = new Thickness(0, 0, 0, 16)
        });

        var cardsSp = new StackPanel();
        Border? cardSel = null;

        void SelCard(Border card, LocalItem local)
        {
            if (cardSel != null)
            {
                cardSel.BorderBrush = RB("#DDE3ED");
                cardSel.Background  = System.Windows.Media.Brushes.White;
                if (cardSel.Child is Grid cg)
                {
                    var d = cg.Children.OfType<Border>().FirstOrDefault(b => b.Tag is string s && s == "dot");
                    if (d != null) d.Background = RB("#CFD8DC");
                }
            }
            card.BorderBrush = RB("#0E2F44");
            card.Background  = RB("#EEF4FB");
            if (card.Child is Grid g)
            {
                var d = g.Children.OfType<Border>().FirstOrDefault(b => b.Tag is string s && s == "dot");
                if (d != null) d.Background = RB("#0E2F44");
            }
            cardSel = card;
            _localSeleccionado = local;
        }

        foreach (var local in locales)
        {
            var card = new Border {
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = RB("#DDE3ED"), BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 11, 14, 11),
                Margin = new Thickness(0, 0, 0, 8), Cursor = Cursors.Hand
            };
            card.Effect = new System.Windows.Media.Effects.DropShadowEffect {
                BlurRadius = 4, ShadowDepth = 1, Opacity = 0.06,
                Color = System.Windows.Media.Color.FromRgb(0, 0, 0)
            };
            var cg = new Grid();
            cg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            cg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var dot = new Border {
                Width = 16, Height = 16, CornerRadius = new CornerRadius(8),
                Background = RB("#CFD8DC"), VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0), Tag = "dot"
            };
            Grid.SetColumn(dot, 0); cg.Children.Add(dot);

            var txt = new TextBlock {
                Text = local.Nombre, FontSize = 13, FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center, Foreground = RB("#263238")
            };
            Grid.SetColumn(txt, 1); cg.Children.Add(txt);

            var badge = new Border {
                Background = RB("#EEF4FB"), CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 8, 3), VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock {
                Text = $"Local {local.Id}", FontSize = 10,
                Foreground = RB("#0E2F44"), FontWeight = FontWeights.SemiBold
            };
            Grid.SetColumn(badge, 2); cg.Children.Add(badge);
            card.Child = cg;

            var lc = local; var cc = card;
            card.MouseLeftButtonUp += (_, _) => SelCard(cc, lc);
            cardsSp.Children.Add(card);
            if (local.Id == _localSeleccionado?.Id) SelCard(card, local);
        }

        var scroll = new ScrollViewer {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 220, Content = cardsSp
        };
        bodySp.Children.Add(scroll);
        bodyBorder.Child = bodySp;
        Grid.SetRow(bodyBorder, 1); root.Children.Add(bodyBorder);

        // ── Pie ───────────────────────────────────────────────────────────────
        var pieBorder = new Border {
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = RB("#E0E0E0"), BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(20, 0, 20, 0)
        };
        var pieSp = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        var btnCer = new Button {
            Content = "Cancelar", Height = 36, Padding = new Thickness(20, 0, 20, 0),
            Background = System.Windows.Media.Brushes.Transparent,
            Foreground = RB("#607D8B"), FontSize = 13, FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(1), BorderBrush = RB("#B0BEC5"),
            Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 10, 0)
        };
        var btnSig = new Button {
            Height = 36, Padding = new Thickness(24, 0, 24, 0),
            Background = RB("#0E2F44"), Foreground = System.Windows.Media.Brushes.White,
            FontSize = 13, FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };
        var btnSigSp = new StackPanel { Orientation = Orientation.Horizontal };
        btnSigSp.Children.Add(new TextBlock { Text = "Continuar", VerticalAlignment = VerticalAlignment.Center });
        btnSigSp.Children.Add(new TextBlock { Text = "  →", VerticalAlignment = VerticalAlignment.Center, FontSize = 15 });
        btnSig.Content = btnSigSp;

        btnCer.Click += (_, _) => Close();
        btnSig.Click += async (_, _) => await CargarCierre();
        pieSp.Children.Add(btnCer); pieSp.Children.Add(btnSig);
        pieBorder.Child = pieSp;
        Grid.SetRow(pieBorder, 2); root.Children.Add(pieBorder);

        Content = root;
    }

    private async Task CargarCierre()
    {
        try
        {
        var cajaAbierta = await _caja.ObtenerCajaAbiertaAsync(_localSeleccionado!.Id);
        if (cajaAbierta == null)
        {
            MostrarSinCajaAbierta();
            return;
        }

        // Pedido explícito: solo quien abrió la caja (o un administrador) puede cerrarla —
        // antes cualquier vendedor logueado en el local podía cerrar la caja de otro (mismo
        // criterio "una caja por local, compartida" que se usa para vender/cobrar, pero acá
        // el cierre es una operación distinta: consolida el turno de una persona puntual).
        var esAdmin = _session.UsuarioActual?.EsAdministrador == true;
        if (!esAdmin && cajaAbierta.IdUsuarioApe != _session.UsuarioActual?.IdUsuario)
        {
            MostrarCajaDeOtroUsuario(cajaAbierta);
            return;
        }

        _idMaster = cajaAbierta.IdMaster;

        // cargar movimientos
        using var conn = _db.Create();
        _movs = (await conn.QueryAsync<FilaCierre>(@"
            SELECT
                CASE D.TIPO WHEN 'I' THEN 'INGRESO' ELSE 'EGRESO' END AS Accion,
                D.SUBTIPO  AS Concepto,
                ISNULL(D.CONCEPTO,'') AS Detalle,
                D.MONTO,
                ISNULL(D.FORMA_PAGO,'') AS Metodo,
                ISNULL(D.REFERENCIA,'') AS Numero,
                CONVERT(varchar(10), D.FECHA_HORA, 103) AS Fecha,
                CONVERT(varchar(8),  D.FECHA_HORA, 108) AS Hora,
                D.TIPO, D.SUBTIPO AS SubTipo, D.ESTADO_REG
            FROM CAJA_DETALLE D
            WHERE D.ID_MASTER = @m AND D.ESTADO_REG = 'V'
            ORDER BY D.FECHA_HORA DESC",
            new { m = _idMaster })).ToList();

        // CAJA_DETALLE.CONCEPTO es varchar (no Unicode) — el "°" que los SPs legados escriben
        // literal en el texto ("CUOTA N°:") se corrompe con el collation del servidor,
        // llegando como bytes sueltos en vez del símbolo real (confirmado: 0xC2 suelto, el
        // primer byte de la codificación UTF-8 de "°" mal interpretado). Se normaliza acá en
        // vez de tocar el SP — cualquier basura entre "CUOTA N" y los dos puntos se reemplaza
        // por "°" limpio.
        foreach (var m in _movs)
            m.Detalle = System.Text.RegularExpressions.Regex.Replace(m.Detalle, @"(CUOTA N)\S*?(:)", "$1°$2");

        BuildUI(cajaAbierta);
        }
        catch (Exception ex)
        {
            var err = new StackPanel { Margin = new Thickness(32) };
            err.Children.Add(new TextBlock { Text = "Error al cargar el cierre de caja:",
                FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = RB("#B71C1C") });
            err.Children.Add(new TextBlock { Text = ex.Message, FontSize = 12,
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,6,0,0) });
            var btnCerrar = new Button { Content = "Cerrar", Width = 100, Height = 34, Margin = new Thickness(0,16,0,0) };
            btnCerrar.Click += (_, _) => Close();
            err.Children.Add(btnCerrar);
            Content = err;
        }
    }

    // Pantalla accionable cuando se intenta cerrar una caja que nunca se abrió — en vez de
    // solo informar y obligar a salir para ir a buscar el módulo de Apertura por su cuenta,
    // ofrece abrirlo directamente desde acá (mismo patrón "atajo" que ya usa CobrosWindow/
    // VentasWindows cuando detectan que hace falta abrir caja antes de continuar).
    private void MostrarSinCajaAbierta()
    {
        var root = new Grid { Background = RB("#F5F6F8") };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var hdr = new Border { Background = RB("#0E2F44"), Padding = new Thickness(24, 18, 24, 18) };
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock { Text = "CIERRE DE CAJA",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 16, FontWeight = FontWeights.Bold });
        hdrSp.Children.Add(new TextBlock { Text = "No es posible continuar",
            Foreground = RB("#B0BEC5"), FontSize = 11, Margin = new Thickness(0,2,0,0) });
        hdr.Child = hdrSp;
        Grid.SetRow(hdr, 0); root.Children.Add(hdr);

        var body = new StackPanel { Margin = new Thickness(32,28,32,28), VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 460 };

        var iconCircle = new Border {
            Width = 56, Height = 56, CornerRadius = new CornerRadius(28),
            Background = RB("#FFF3E0"), HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0,0,0,16)
        };
        iconCircle.Child = new TextBlock { Text = "⚠", FontSize = 26, Foreground = RB("#E65100"),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        body.Children.Add(iconCircle);

        body.Children.Add(new TextBlock { Text = "No hay caja abierta en este local",
            FontSize = 16, FontWeight = FontWeights.Bold, Foreground = RB("#263238"),
            HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center });
        body.Children.Add(new TextBlock { Text = $"Local: {_localSeleccionado!.Nombre}",
            FontSize = 13, Margin = new Thickness(0,10,0,0), Foreground = RB("#607D8B"),
            HorizontalAlignment = HorizontalAlignment.Center });

        // Bloque explicativo: por qué pasa esto y qué hacer, en vez de un mensaje seco.
        var explicBorder = new Border {
            Background = System.Windows.Media.Brushes.White, BorderBrush = RB("#CFD8DC"),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14,12,14,12), Margin = new Thickness(0,18,0,0)
        };
        var explicSp = new StackPanel();
        explicSp.Children.Add(new TextBlock { Text = "¿Por qué pasa esto?",
            FontSize = 11, FontWeight = FontWeights.Bold, Foreground = RB("#37474F") });
        explicSp.Children.Add(new TextBlock {
            Text = "Para cerrar una caja, primero debe existir una apertura activa para este local. " +
                   "Todavía no se registró ninguna hoy (o ya fue cerrada anteriormente).",
            FontSize = 12, Foreground = RB("#546E7A"), TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0,4,0,10)
        });
        explicSp.Children.Add(new TextBlock { Text = "¿Qué podés hacer?",
            FontSize = 11, FontWeight = FontWeights.Bold, Foreground = RB("#37474F") });
        explicSp.Children.Add(new TextBlock {
            Text = "Abrí la caja de este local ahora mismo y, cuando termines la jornada, volvé a " +
                   "esta pantalla para cerrarla.",
            FontSize = 12, Foreground = RB("#546E7A"), TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0,4,0,0)
        });
        explicBorder.Child = explicSp;
        body.Children.Add(explicBorder);

        Grid.SetRow(body, 1); root.Children.Add(body);

        // Pie con las dos acciones: abrir el módulo de Apertura ahora, o cancelar y salir.
        var pie = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = RB("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(20,14,20,14) };
        var pieSp = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center };

        var btnCancelar = new Button { Content = "Cancelar", Height = 40, Padding = new Thickness(22,0,22,0),
            Background = System.Windows.Media.Brushes.Transparent, Foreground = RB("#607D8B"),
            FontSize = 13, FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(1),
            BorderBrush = RB("#B0BEC5"), Cursor = Cursors.Hand, Margin = new Thickness(0,0,10,0) };
        btnCancelar.Click += (_, _) => Close();

        var btnAbrirCaja = new Button { Height = 40, Padding = new Thickness(22,0,22,0),
            Background = RB("#0E2F44"), Foreground = System.Windows.Media.Brushes.White,
            FontSize = 13, FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand };
        var btnAbrirCajaSp = new StackPanel { Orientation = Orientation.Horizontal };
        btnAbrirCajaSp.Children.Add(new TextBlock { Text = "🔓  ", VerticalAlignment = VerticalAlignment.Center });
        btnAbrirCajaSp.Children.Add(new TextBlock { Text = "Abrir Caja Ahora", VerticalAlignment = VerticalAlignment.Center });
        btnAbrirCaja.Content = btnAbrirCajaSp;
        btnAbrirCaja.Click += (_, _) =>
        {
            Close();
            new CajaAperturaWindow().Show();
        };

        pieSp.Children.Add(btnCancelar);
        pieSp.Children.Add(btnAbrirCaja);
        pie.Child = pieSp;
        Grid.SetRow(pie, 2); root.Children.Add(pie);

        Content = root;
    }

    // Pantalla de bloqueo cuando la caja SÍ está abierta, pero por otro usuario — a diferencia
    // de MostrarSinCajaAbierta (que ofrece un atajo para abrir), acá no hay nada que el usuario
    // logueado pueda hacer más que esperar a que el dueño del turno cierre, o pedirle a un
    // administrador que lo haga por él.
    private void MostrarCajaDeOtroUsuario(CrediSoft.Core.Models.CajaMaster caja)
    {
        var root = new Grid { Background = RB("#F5F6F8") };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var hdr = new Border { Background = RB("#0E2F44"), Padding = new Thickness(24, 18, 24, 18) };
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock { Text = "CIERRE DE CAJA",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 16, FontWeight = FontWeights.Bold });
        hdrSp.Children.Add(new TextBlock { Text = "No es posible continuar",
            Foreground = RB("#B0BEC5"), FontSize = 11, Margin = new Thickness(0,2,0,0) });
        hdr.Child = hdrSp;
        Grid.SetRow(hdr, 0); root.Children.Add(hdr);

        var body = new StackPanel { Margin = new Thickness(32,28,32,28), VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 460 };

        var iconCircle = new Border {
            Width = 56, Height = 56, CornerRadius = new CornerRadius(28),
            Background = RB("#FFEBEE"), HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0,0,0,16)
        };
        iconCircle.Child = new TextBlock { Text = "🔒", FontSize = 24, Foreground = RB("#C62828"),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        body.Children.Add(iconCircle);

        body.Children.Add(new TextBlock { Text = "Esta caja está a nombre de otro usuario",
            FontSize = 16, FontWeight = FontWeights.Bold, Foreground = RB("#263238"),
            HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center });
        body.Children.Add(new TextBlock { Text = $"Local: {_localSeleccionado!.Nombre}",
            FontSize = 13, Margin = new Thickness(0,10,0,0), Foreground = RB("#607D8B"),
            HorizontalAlignment = HorizontalAlignment.Center });

        var explicBorder = new Border {
            Background = System.Windows.Media.Brushes.White, BorderBrush = RB("#CFD8DC"),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14,12,14,12), Margin = new Thickness(0,18,0,0)
        };
        var explicSp = new StackPanel();
        explicSp.Children.Add(new TextBlock { Text = "¿Por qué pasa esto?",
            FontSize = 11, FontWeight = FontWeights.Bold, Foreground = RB("#37474F") });
        explicSp.Children.Add(new TextBlock {
            Text = $"La caja abierta en este local pertenece a {caja.NombreCajero} " +
                   $"(abierta el {caja.FechaApertura:dd/MM/yyyy HH:mm}). Solo quien abrió el turno, " +
                   "o un administrador, puede cerrarlo.",
            FontSize = 12, Foreground = RB("#546E7A"), TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0,4,0,10)
        });
        explicSp.Children.Add(new TextBlock { Text = "¿Qué podés hacer?",
            FontSize = 11, FontWeight = FontWeights.Bold, Foreground = RB("#37474F") });
        explicSp.Children.Add(new TextBlock {
            Text = $"Pedile a {caja.NombreCajero} que cierre su propia caja, o pedile a un " +
                   "administrador que lo haga en su nombre.",
            FontSize = 12, Foreground = RB("#546E7A"), TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0,4,0,0)
        });
        explicBorder.Child = explicSp;
        body.Children.Add(explicBorder);

        Grid.SetRow(body, 1); root.Children.Add(body);

        var pie = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = RB("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(20,14,20,14) };
        var pieSp = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center };

        var btnCerrarVentana = new Button { Content = "Entendido", Height = 40, Padding = new Thickness(22,0,22,0),
            Background = RB("#0E2F44"), Foreground = System.Windows.Media.Brushes.White,
            FontSize = 13, FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand };
        btnCerrarVentana.Click += (_, _) => Close();
        pieSp.Children.Add(btnCerrarVentana);
        pie.Child = pieSp;
        Grid.SetRow(pie, 2); root.Children.Add(pie);

        Content = root;
    }

    private void BuildUI(CrediSoft.Core.Models.CajaMaster caja)
    {
        // ventana más ancha para que la grilla de movimientos y la tabla resumen tengan espacio real
        // Height/MinHeight fijos excedían el área de trabajo en pantallas de 1366x768.
        // Width tope al ancho de pantalla disponible (antes 1280 fijo, se salía del work area
        // en pantallas de 1360px y quedaba pegada/cortada contra el borde derecho — Left ya
        // había sido calculado en el constructor para un Width menor, así que hay que
        // recalcularlo acá también, después de fijar el Width real).
        var altoDisponibleCierre = System.Windows.SystemParameters.WorkArea.Height - 20;
        var anchoDisponibleCierre = System.Windows.SystemParameters.WorkArea.Width - 20;
        Width = Math.Min(1200, anchoDisponibleCierre); Height = Math.Min(620, altoDisponibleCierre);
        MinWidth = Math.Min(1050, anchoDisponibleCierre); MinHeight = Math.Min(560, altoDisponibleCierre);
        Left = (System.Windows.SystemParameters.WorkArea.Width  - Width)  / 2;
        Top  = (System.Windows.SystemParameters.WorkArea.Height - Height) / 2;

        // ── calcular totales primero ──────────────────────────────────────────
        decimal Suma(string subtipo, string tipo, string fp) => _movs
            .Where(m => (subtipo == "*" || m.SubTipo == subtipo) &&
                        (tipo == "*" || m.Tipo == tipo) &&
                        (fp == "*" || m.Metodo.ToUpper() == fp))
            .Sum(m => m.Monto);

        var saldoIni    = caja.MontoBase;
        var ventasEf    = Suma("VENTA","I","EFECTIVO");
        var ventasTar   = Suma("VENTA","I","TARJETA");
        var ventasTrans = Suma("VENTA","I","TRANSFERENCIA");
        var ventasCheq  = Suma("VENTA","I","CHEQUE");
        var ventasOtro  = _movs.Where(m=>m.SubTipo=="VENTA"&&m.Tipo=="I"&&
            !new[]{"EFECTIVO","TARJETA","TRANSFERENCIA","CHEQUE"}.Contains(m.Metodo.ToUpper())).Sum(m=>m.Monto);
        var cobrosEf    = Suma("COBRO_SISTEMA","I","EFECTIVO");
        var cobrosTar   = Suma("COBRO_SISTEMA","I","TARJETA");
        var cobrosTrans = Suma("COBRO_SISTEMA","I","TRANSFERENCIA");
        var cobrosCheq  = Suma("COBRO_SISTEMA","I","CHEQUE");
        var cobrosOtro  = _movs.Where(m=>m.SubTipo=="COBRO_SISTEMA"&&m.Tipo=="I"&&
            !new[]{"EFECTIVO","TARJETA","TRANSFERENCIA","CHEQUE"}.Contains(m.Metodo.ToUpper())).Sum(m=>m.Monto);
        var ingrManEf   = _movs.Where(m=>m.Tipo=="I"&&m.SubTipo!="VENTA"&&m.SubTipo!="COBRO_SISTEMA"&&m.SubTipo!="APERTURA"&&m.Metodo.ToUpper()=="EFECTIVO").Sum(m=>m.Monto);
        var ingrManTar  = _movs.Where(m=>m.Tipo=="I"&&m.SubTipo!="VENTA"&&m.SubTipo!="COBRO_SISTEMA"&&m.SubTipo!="APERTURA"&&m.Metodo.ToUpper()=="TARJETA").Sum(m=>m.Monto);
        var ingrManTrans= _movs.Where(m=>m.Tipo=="I"&&m.SubTipo!="VENTA"&&m.SubTipo!="COBRO_SISTEMA"&&m.SubTipo!="APERTURA"&&m.Metodo.ToUpper()=="TRANSFERENCIA").Sum(m=>m.Monto);
        var ingrManCheq = _movs.Where(m=>m.Tipo=="I"&&m.SubTipo!="VENTA"&&m.SubTipo!="COBRO_SISTEMA"&&m.SubTipo!="APERTURA"&&m.Metodo.ToUpper()=="CHEQUE").Sum(m=>m.Monto);
        var egresosEf   = _movs.Where(m=>m.Tipo=="E"&&m.SubTipo!="ANTICIPO"&&m.Metodo.ToUpper()=="EFECTIVO").Sum(m=>m.Monto);
        var egresosTar  = _movs.Where(m=>m.Tipo=="E"&&m.SubTipo!="ANTICIPO"&&m.Metodo.ToUpper()=="TARJETA").Sum(m=>m.Monto);
        var egresosTrans= _movs.Where(m=>m.Tipo=="E"&&m.SubTipo!="ANTICIPO"&&m.Metodo.ToUpper()=="TRANSFERENCIA").Sum(m=>m.Monto);
        var egresosCheq = _movs.Where(m=>m.Tipo=="E"&&m.SubTipo!="ANTICIPO"&&m.Metodo.ToUpper()=="CHEQUE").Sum(m=>m.Monto);
        var anticipos   = _movs.Where(m=>m.SubTipo=="ANTICIPO"&&m.Tipo=="E").Sum(m=>m.Monto);
        decimal Tot(decimal a,decimal b,decimal c,decimal d,decimal e=0)=>a+b+c+d+e;
        var totVentas   = Tot(ventasEf,ventasTar,ventasTrans,ventasCheq,ventasOtro);
        var totCobros   = Tot(cobrosEf,cobrosTar,cobrosTrans,cobrosCheq,cobrosOtro);
        var totIngrMan  = Tot(ingrManEf,ingrManTar,ingrManTrans,ingrManCheq);
        var totEgresos  = Tot(egresosEf,egresosTar,egresosTrans,egresosCheq);
        // TOTAL NETO recalculado solo con los montos en EFECTIVO — el cierre acá es solo de
        // efectivo (pedido explícito), así que debe cuadrar exactamente contra la suma de la
        // columna EFECTIVO que se muestra en la tabla, no contra el total de todos los medios
        // de pago (que incluía Tarjeta/Transferencia/Cheque/Otros, ya ocultos de la tabla).
        var totalNeto   = saldoIni + ventasEf + cobrosEf + ingrManEf - egresosEf - anticipos;
        _totalNetoActual = totalNeto;

        // Neto por medio de pago — para comparar lo que el cajero declara en CADA campo contra
        // lo que le corresponde a ESE medio, en vez de un único neto mezclado (ver comentario en
        // los campos de clase). QR se separa de "Otro" acá porque el desglose de cierre lo pide
        // como columna propia, aunque en el resto de esta pantalla (tabla resumen/impresión) siga
        // agrupado dentro de "OTRO" — no se toca esa parte para no alterar el reporte existente.
        decimal SumaQr(string subtipo, string tipo) => Suma(subtipo, tipo, "QR");
        var ventasQr    = SumaQr("VENTA","I");
        var cobrosQr    = SumaQr("COBRO_SISTEMA","I");
        var ingrManQr   = _movs.Where(m=>m.Tipo=="I"&&m.SubTipo!="VENTA"&&m.SubTipo!="COBRO_SISTEMA"&&m.SubTipo!="APERTURA"&&m.Metodo.ToUpper()=="QR").Sum(m=>m.Monto);
        var egresosQr   = _movs.Where(m=>m.Tipo=="E"&&m.SubTipo!="ANTICIPO"&&m.Metodo.ToUpper()=="QR").Sum(m=>m.Monto);

        var netoPorMedio = new Dictionary<string, decimal> {
            ["EFECTIVO"]      = saldoIni + ventasEf  + cobrosEf  + ingrManEf  - egresosEf,
            ["TARJETA"]       =            ventasTar + cobrosTar + ingrManTar - egresosTar,
            ["TRANSFERENCIA"] =            ventasTrans + cobrosTrans + ingrManTrans - egresosTrans,
            ["QR"]            =            ventasQr  + cobrosQr  + ingrManQr  - egresosQr,
            ["CHEQUE"]        =            ventasCheq + cobrosCheq + ingrManCheq - egresosCheq,
        };
        // Los anticipos (pago de sueldo) siempre son en efectivo en la práctica — se descuentan
        // solo del neto de efectivo, igual que el cálculo original descontaba del único neto.
        netoPorMedio["EFECTIVO"] -= anticipos;

        var root = new DockPanel { Background = RB("#F5F5F5") };

        // ── Header — naranja, igual al sistema viejo (2026-07-30) ────────────────
        var hdr = new Border { Background = RB("#E65100"), Padding = new Thickness(18,12,18,12) };
        var hdrG = new Grid();
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hdrG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var hdrLeft = new StackPanel();
        hdrLeft.Children.Add(new TextBlock { Text = "CIERRE DE CAJA",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 17, FontWeight = FontWeights.Bold });
        hdrLeft.Children.Add(new TextBlock { Text = $"Local: {_session.LocalActual!.NombreLocal}",
            Foreground = RB("#FFE0B2"), FontSize = 12 });
        Grid.SetColumn(hdrLeft, 0); hdrG.Children.Add(hdrLeft);
        var lblHora = new TextBlock { Foreground = System.Windows.Media.Brushes.White,
            FontSize = 22, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
        lblHora.Text = DateTime.Now.ToString("HH:mm:ss");
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => lblHora.Text = DateTime.Now.ToString("HH:mm:ss");
        timer.Start();
        Grid.SetColumn(lblHora, 1); hdrG.Children.Add(lblHora);
        hdr.Child = hdrG;
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        // ── Subheader ─────────────────────────────────────────────────────────
        var subhdr = new Border { Background = RB("#FFF3E0"), BorderBrush = RB("#FFCC80"),
            BorderThickness = new Thickness(0,0,0,1), Padding = new Thickness(18,8,18,8) };
        var subG = new Grid();
        for (int i = 0; i < 5; i++) subG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        void SHItem(string lbl, string val, int col) {
            var sp = new StackPanel(); Grid.SetColumn(sp, col);
            sp.Children.Add(new TextBlock { Text = lbl, FontSize = 10, Foreground = RB("#E65100"), FontWeight = FontWeights.Bold });
            sp.Children.Add(new TextBlock { Text = val, FontSize = 13, FontWeight = FontWeights.Bold });
            subG.Children.Add(sp);
        }
        SHItem("FECHA APERTURA", caja.FechaApertura.ToString("dd/MM/yyyy"), 0);
        SHItem("HORA APERTURA",  caja.FechaApertura.ToString("HH:mm:ss"), 1);
        SHItem("USUARIO / CAJERO", _session.UsuarioActual?.NombreUsuario ?? "-", 2);
        SHItem("BASE EN CAJA", $"Gs. {caja.MontoBase:N0}", 3);
        SHItem("MOVIMIENTOS", _movs.Count.ToString(), 4);
        subhdr.Child = subG;
        DockPanel.SetDock(subhdr, Dock.Top); root.Children.Add(subhdr);

        // ── Pie ───────────────────────────────────────────────────────────────
        var pie = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = RB("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(18,10,18,10) };
        var pieSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        Button MkBtn(string txt, string bg) => new Button {
            Content = txt, Height = 38, Padding = new Thickness(22,0,22,0),
            Background = RB(bg), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, FontSize = 13,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            Margin = new Thickness(8,0,0,0) };
        var btnGuardar  = MkBtn("Guardar (F12)", "#1B5E20");
        var btnImprimir = MkBtn("Imprimir",     "#1565C0");
        var btnCancelar = MkBtn("✖  Cerrar (Esc)",  "#546E7A");
        btnGuardar.Click  += async (_, _) => await Guardar();
        btnCancelar.Click += (_, _) => Close();
        KeyDown += (_, e) => { if (e.Key == Key.F12) _ = Guardar(); if (e.Key == Key.Escape) Close(); };

        // capturar los totales calculados (solo Efectivo) para usarlos en la impresión
        btnImprimir.Click += async (_, _) => await ImprimirCierreCaja(caja,
            saldoIni, ventasEf, cobrosEf, ingrManEf, egresosEf, anticipos, totalNeto);

        pieSp.Children.Add(btnGuardar); pieSp.Children.Add(btnImprimir); pieSp.Children.Add(btnCancelar);
        pie.Child = pieSp;
        DockPanel.SetDock(pie, Dock.Bottom); root.Children.Add(pie);

        // ── Cuerpo: layout en 2 filas, igual al sistema viejo (2026-07-30) —
        // fila superior: grilla de movimientos a todo el ancho; fila inferior: tabla de
        // totales (columna ancha) + efectivo/diferencia/OBS/comprobante (columna angosta a la
        // derecha). Antes era 2 columnas de alto completo (grilla | resumen+efectivo), que
        // dejaba la tabla de totales apilada arriba del campo de efectivo en vez de abajo de
        // la grilla — reportado como distinto al layout de referencia.
        var body = new Grid { Margin = new Thickness(12,8,12,8) };
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.8, GridUnitType.Star) });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.2, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.6, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        // Tercera columna, angosta, solo para el bloque "Comprobante de depósito" (con la
        // foto) — antes vivía apilado debajo de OBS en la misma columna que Efectivo/
        // Diferencia, empujando todo hacia abajo y obligando a scrollear para verlo. Ahora
        // queda al costado, visible sin scroll junto al resto.
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.85, GridUnitType.Star) });
        root.Children.Add(body);

        // ── Fila inferior, columna derecha: efectivo/diferencia/OBS ──
        var efBox = new StackPanel();
        var efScroll = new ScrollViewer {
            Margin = new Thickness(10,0,0,0), Content = efBox,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetRow(efScroll, 1); Grid.SetColumn(efScroll, 1); body.Children.Add(efScroll);

        // ── Fila inferior, tercera columna: comprobante de depósito + foto ──
        var compBox = new StackPanel();
        var compScroll = new ScrollViewer {
            Margin = new Thickness(10,0,0,0), Content = compBox,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetRow(compScroll, 1); Grid.SetColumn(compScroll, 2); body.Children.Add(compScroll);

        void EfLbl(string t) => efBox.Children.Add(new TextBlock { Text = t, FontSize = 10,
            FontWeight = FontWeights.Bold, Foreground = RB("#546E7A"), Margin = new Thickness(0,0,0,3) });

        // Pedido explícito de la dueña del negocio: los cajeros vienen del sistema viejo, que
        // solo pedía declarar EFECTIVO al cerrar — tarjeta/transferencia/QR/cheque se calculaban
        // solos del sistema, sin campo para tecleer y sin afectar el indicador de cuadrado/
        // faltante/sobrante (que ahí también comparaba únicamente "EFEC. SISTEMA" vs "EFEC.
        // REAL", confirmado leyendo el ticket de cierre embebido en el .exe legado). Volver a
        // exigir 5 campos rompía ese hábito. Solo EFECTIVO queda editable acá; los demás se
        // muestran de solo lectura con el neto ya calculado — el desglose completo por medio de
        // pago sigue disponible sin recortes en el Historial de Caja (arqueo general).
        foreach (var (clave, etiqueta) in MediosPago)
        {
            var esEfectivo = clave == "EFECTIVO";
            if (esEfectivo) EfLbl($"{etiqueta.ToUpper()} EN CAJA");

            if (esEfectivo)
            {
                var txt = new TextBox { Padding = new Thickness(8,6,8,6), FontSize = 14,
                    FontWeight = FontWeights.Bold, Background = RB("#E3F2FD"),
                    BorderBrush = RB("#1565C0"), BorderThickness = new Thickness(2),
                    HorizontalContentAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0,0,0,4) };
                bool fmtBusy = false;
                txt.TextChanged += (_, _) => {
                    if (fmtBusy) return; fmtBusy = true;
                    var raw = txt.Text.Replace(".","").Replace(",","").Trim();
                    if (long.TryParse(raw, out var n)) {
                        var fmt = n.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-PY"));
                        txt.Text = fmt; txt.CaretIndex = fmt.Length;
                    }
                    fmtBusy = false;
                    RecalcDiferencia(netoPorMedio);
                };
                _txtContado[clave] = txt;
                efBox.Children.Add(txt);

                var lblDif = new TextBlock { Text = "0", FontSize = 12, FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Right, Padding = new Thickness(8,4,8,4),
                    Background = RB("#E0E0E0"), Margin = new Thickness(0,0,0,10) };
                _lblDifPorMedio[clave] = lblDif;
                efBox.Children.Add(lblDif);
            }
            else
            {
                // Oculto de la UI a pedido de la dueña del negocio (el sistema viejo solo
                // mostraba EFECTIVO) — pero el TextBox se sigue creando y cargando en
                // _txtContado[clave] con el neto ya calculado, sin agregarlo a efBox.Children,
                // para que Guardar()/ImprimirCierreCaja sigan leyendo los 5 medios sin cambios.
                var neto = netoPorMedio[clave];
                var txtRO = new TextBox {
                    Text = neto.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-PY")),
                    Padding = new Thickness(8,6,8,6), FontSize = 13, FontWeight = FontWeights.SemiBold,
                    Background = RB("#ECEFF1"), Foreground = RB("#546E7A"),
                    BorderBrush = RB("#CFD8DC"), BorderThickness = new Thickness(1),
                    HorizontalContentAlignment = HorizontalAlignment.Right,
                    IsReadOnly = true, IsHitTestVisible = false, Focusable = false,
                    Margin = new Thickness(0,0,0,10) };
                _txtContado[clave] = txtRO;
            }
        }

        _lblIndicador = new TextBlock { FontSize = 15, FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center, Padding = new Thickness(10,14,10,14),
            Margin = new Thickness(0,4,0,4),
            TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        efBox.Children.Add(_lblIndicador);

        efBox.Children.Add(new TextBlock { Text = "OBS: (opcional, editable)", FontSize = 10, FontWeight = FontWeights.Bold,
            Foreground = RB("#546E7A"), Margin = new Thickness(0,12,0,3) });
        // Fondo blanco + borde más marcado y grueso — antes se confundía visualmente con una
        // etiqueta de solo lectura porque no contrastaba con el fondo gris claro del panel.
        _txtObs = new TextBox { Height = 60, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            Padding = new Thickness(6,4,6,4), Background = System.Windows.Media.Brushes.White,
            BorderBrush = RB("#90A4AE"), BorderThickness = new Thickness(1.5),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        efBox.Children.Add(_txtObs);

        // ── Comprobante de depósito (opcional acá, no bloquea el cierre) — columna
        // propia (compBox), sin margen superior porque es el único elemento ahí. ──────
        var compBorder = new Border {
            Margin = new Thickness(0,0,0,0), Background = RB("#F0F4F8"),
            BorderBrush = RB("#BDBDBD"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4), Padding = new Thickness(10,8,10,8)
        };
        var compSp = new StackPanel();
        compSp.Children.Add(new TextBlock {
            Text = "COMPROBANTE DE DEPÓSITO (opcional)", FontSize = 10, FontWeight = FontWeights.Bold,
            Foreground = RB("#546E7A")
        });
        compSp.Children.Add(new TextBlock {
            Text = "Si ya realizó el depósito, puede cargarlo ahora. Si no, tiene 48hs desde el " +
                   "cierre para completarlo desde \"Comprobantes de Depósito Pendientes\".",
            FontSize = 9.5, Foreground = RB("#78909C"), TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0,2,0,8)
        });
        compSp.Children.Add(new TextBlock {
            Text = "N° de comprobante", FontSize = 9.5, Foreground = RB("#78909C"),
            Margin = new Thickness(0,0,0,3)
        });
        _txtNroComprobanteDep = new TextBox {
            Height = 28, Padding = new Thickness(6,4,6,4),
            Background = System.Windows.Media.Brushes.White, BorderBrush = RB("#BDBDBD"),
            Margin = new Thickness(0,0,0,6)
        };
        compSp.Children.Add(_txtNroComprobanteDep);
        var btnFotoDep = new Button {
            Content = "📷 Adjuntar foto de comprobante", Height = 28,
            Background = RB("#0E2F44"), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };
        _imgMiniaturaComprobanteDep = new Image {
            Stretch = System.Windows.Media.Stretch.Uniform, MaxHeight = 110, Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var miniBorderDep = new Border {
            BorderBrush = RB("#BDBDBD"), BorderThickness = new Thickness(1),
            Background = System.Windows.Media.Brushes.White, Padding = new Thickness(3),
            Margin = new Thickness(0,8,0,0), HorizontalAlignment = HorizontalAlignment.Left,
            Visibility = Visibility.Collapsed, Child = _imgMiniaturaComprobanteDep
        };
        _imgMiniaturaComprobanteDep.MouseLeftButtonUp += (_, _) => VerFotoComprobanteAmpliada();

        btnFotoDep.Click += (_, _) =>
        {
            var dlg = new OpenFileDialog {
                Title = "Seleccionar foto de comprobante de depósito",
                Filter = "Imágenes|*.jpg;*.jpeg;*.png;*.bmp|Todos|*.*"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                _fotoComprobanteDepSeleccionada = System.IO.File.ReadAllBytes(dlg.FileName);
                _lblEstadoFotoDep.Text = "✔ Foto seleccionada — se guardará junto con el cierre. Haga clic en la imagen para verla en grande.";
                _lblEstadoFotoDep.Foreground = RB("#2E7D32");

                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = new System.IO.MemoryStream(_fotoComprobanteDepSeleccionada);
                bmp.CacheOption  = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 300;
                bmp.EndInit();
                bmp.Freeze();
                _imgMiniaturaComprobanteDep.Source = bmp;
                miniBorderDep.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo leer el archivo: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        compSp.Children.Add(btnFotoDep);
        _lblEstadoFotoDep = new TextBlock {
            Text = "Sin foto adjuntada todavía.", FontSize = 9.5,
            Foreground = RB("#90A4AE"), Margin = new Thickness(0,6,0,0), TextWrapping = TextWrapping.Wrap
        };
        compSp.Children.Add(_lblEstadoFotoDep);
        compSp.Children.Add(miniBorderDep);

        void VerFotoComprobanteAmpliada()
        {
            if (_fotoComprobanteDepSeleccionada == null) return;
            var bmpGrande = new System.Windows.Media.Imaging.BitmapImage();
            bmpGrande.BeginInit();
            bmpGrande.StreamSource = new System.IO.MemoryStream(_fotoComprobanteDepSeleccionada);
            bmpGrande.CacheOption  = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmpGrande.EndInit();
            bmpGrande.Freeze();
            var imgGrande = new Image {
                Source = bmpGrande, Stretch = System.Windows.Media.Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8)
            };
            var winFoto = new Window {
                Title = "Comprobante de depósito", Width = 580, Height = 500,
                MinWidth = 420, MinHeight = 360,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
                ResizeMode = ResizeMode.CanResize,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30,30,30))
            };
            winFoto.Content = new ScrollViewer {
                Content = imgGrande,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30,30,30))
            };
            winFoto.ShowDialog();
        }

        compBorder.Child = compSp;
        compBox.Children.Add(compBorder);

        // tabla resumen — anclada arriba (no estira el Border vacío cuando efBox es más alto por
        // el panel de comprobante) y con columnas Star (no ancho fijo en px) para que ocupe todo
        // el ancho real disponible en vez de dejar espacio muerto entre la tabla y efBox.
        var tbl = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = RB("#BDBDBD"), BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Stretch };
        // Tabla resumen con el desglose completo por medio de pago (Efectivo/Tarjeta/
        // Transferencia/Cheque/Otros), igual que el sistema viejo — vuelta a mostrar a pedido
        // explícito (2026-07-30) tras haber estado recortada a solo Efectivo. El campo editable
        // y el indicador de diferencia siguen siendo SOLO de efectivo (eso no cambió): esta
        // tabla es de solo lectura, informativa, para que el cajero vea de dónde sale cada
        // total sin tener que declarar manualmente los otros 4 medios.
        var tblG = new Grid();
        var colWidths = new[] { 1.6, 1.0, 1.0, 1.1, 1.0, 1.0, 1.1 };
        foreach (var w in colWidths)
            tblG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w, GridUnitType.Star) });
        var thdrs = new[] { "CONCEPTO","EFECTIVO","TARJETA","TRANSFER.","CHEQUE","OTROS","TOTALES" };
        int trow = 0;
        tblG.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int c = 0; c < thdrs.Length; c++) {
            var cell = new Border { Background = RB("#E65100"), Padding = new Thickness(6,5,6,5) };
            cell.Child = new TextBlock { Text = thdrs[c], Foreground = System.Windows.Media.Brushes.White,
                FontSize = 11, FontWeight = FontWeights.Bold,
                TextAlignment = c == 0 ? TextAlignment.Left : TextAlignment.Right };
            Grid.SetRow(cell,trow); Grid.SetColumn(cell,c); tblG.Children.Add(cell);
        }
        void TRow(string lbl, decimal ef, decimal tar, decimal trans, decimal cheq, decimal otro, decimal tot, bool bold=false, string? bg=null) {
            tblG.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); trow++;
            var vals = new[] { lbl, ef.ToString("N0"), tar.ToString("N0"), trans.ToString("N0"), cheq.ToString("N0"), otro.ToString("N0"), tot.ToString("N0") };
            var rowBg = bg != null ? RB(bg) : (trow%2==0 ? RB("#F5F5F5") : System.Windows.Media.Brushes.White);
            for (int c = 0; c < vals.Length; c++) {
                var cell = new Border { Background = rowBg, Padding = new Thickness(6,4,6,4),
                    BorderBrush = RB("#E0E0E0"), BorderThickness = new Thickness(0,0,0,1) };
                var isNeg = (lbl.Contains("EGRESO")||lbl.Contains("ANTICIPO")) && c > 0 && tot > 0;
                cell.Child = new TextBlock { Text = vals[c], FontSize = 11,
                    TextAlignment = c==0 ? TextAlignment.Left : TextAlignment.Right,
                    FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = isNeg ? RB("#C62828") : System.Windows.Media.Brushes.Black };
                Grid.SetRow(cell,trow); Grid.SetColumn(cell,c); tblG.Children.Add(cell);
            }
        }
        TRow("SALDO INICIAL (+)", saldoIni,0,0,0,0, saldoIni);
        TRow("VENTAS (+)",       ventasEf,ventasTar,ventasTrans,ventasCheq,ventasOtro,totVentas);
        TRow("COBROS SIST. (+)", cobrosEf,cobrosTar,cobrosTrans,cobrosCheq,cobrosOtro,totCobros);
        TRow("INGR. MAN. (+)",   ingrManEf,ingrManTar,ingrManTrans,ingrManCheq,0,totIngrMan);
        TRow("EGRESOS (-)",      egresosEf,egresosTar,egresosTrans,egresosCheq,0,totEgresos);
        TRow("ANTICIPOS (-)",    anticipos,0,0,0,0,anticipos);
        // TOTAL NETO por columna: Efectivo es el único que el sistema exige cuadrar contra lo
        // declarado por el cajero (netoPorMedio["EFECTIVO"] ya resta ANTICIPOS); las demás
        // columnas se muestran informativas con su propio neto (sin restar anticipos, que
        // siempre son en efectivo en la práctica).
        var totNetoTar   = ventasTar   + cobrosTar   + ingrManTar   - egresosTar;
        var totNetoTrans = ventasTrans + cobrosTrans + ingrManTrans - egresosTrans;
        var totNetoCheq  = ventasCheq  + cobrosCheq  + ingrManCheq  - egresosCheq;
        var totNetoOtro  = ventasOtro  + cobrosOtro;
        var totNetoGral  = totalNeto + totNetoTar + totNetoTrans + totNetoCheq + totNetoOtro;
        TRow("TOTAL NETO", totalNeto,totNetoTar,totNetoTrans,totNetoCheq,totNetoOtro,totNetoGral, bold:true, bg:totalNeto>=0?"#E8F5E9":"#FFEBEE");
        tbl.Child = tblG;
        // Tabla de totales: fila inferior, columna izquierda (ancha) — a la par del panel de
        // efectivo/diferencia de la derecha, ambos en la misma fila, igual que el viejo.
        var tblScroll = new ScrollViewer {
            Margin = new Thickness(0,0,0,0), Content = tbl,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetRow(tblScroll, 1); Grid.SetColumn(tblScroll, 0); body.Children.Add(tblScroll);

        // ── Fila superior: grilla de movimientos, a todo el ancho (colspan 2) ────
        const double rowH = 32, headerH = 34;

        var dg = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            CanUserResizeRows = false, SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = RB("#EEEEEE"),
            Background = System.Windows.Media.Brushes.White,
            RowBackground = System.Windows.Media.Brushes.White,
            AlternatingRowBackground = RB("#FAFAFA"),
            BorderThickness = new Thickness(1), BorderBrush = RB("#BDBDBD"),
            FontSize = 12, Margin = new Thickness(0,0,0,8),
            RowHeight = rowH, ColumnHeaderHeight = headerH,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            ColumnWidth = new DataGridLength(1, DataGridLengthUnitType.Star) };
        DataGridTextColumn DGC(string h, string b, double w, TextAlignment a = TextAlignment.Left) {
            var col = new DataGridTextColumn { Header = h,
                Binding = new System.Windows.Data.Binding(b),
                Width = new DataGridLength(w, DataGridLengthUnitType.Star) };
            if (a != TextAlignment.Left)
                col.ElementStyle = new System.Windows.Style(typeof(TextBlock)) {
                    Setters = { new Setter(TextBlock.TextAlignmentProperty, a) } };
            return col;
        }
        // Columna Fecha agregada junto a Hora — antes solo se veía la hora, y en cajas que
        // quedan abiertas varios días (pedido explícito 2026-08-03, caso real: Fassardi abierta
        // desde el 31/07 hasta el 03/08) los movimientos de distintos días se mezclaban en la
        // grilla sin poder distinguir a qué día correspondía cada uno.
        dg.Columns.Add(DGC("Fecha",   "Fecha",   0.7));
        dg.Columns.Add(DGC("Hora",    "Hora",    0.6));
        dg.Columns.Add(DGC("Acción",  "Accion",  0.7));
        dg.Columns.Add(DGC("Concepto","Concepto",0.9));
        dg.Columns.Add(new DataGridTextColumn { Header = "Monto",
            Binding = new System.Windows.Data.Binding("Monto") { StringFormat = "N0" },
            Width = new DataGridLength(1.0, DataGridLengthUnitType.Star),
            ElementStyle = new System.Windows.Style(typeof(TextBlock)) {
                Setters = { new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right) } } });
        dg.Columns.Add(DGC("Método",  "Metodo",  0.8));
        dg.Columns.Add(DGC("Número",  "Numero",  0.7));
        dg.Columns.Add(DGC("Concepto / Detalle","Detalle", 2.0));

        var egresoStyle = new System.Windows.Style(typeof(DataGridRow));
        var egTrig = new DataTrigger { Binding = new System.Windows.Data.Binding("Accion"), Value = "EGRESO" };
        egTrig.Setters.Add(new Setter(DataGridRow.ForegroundProperty, RB("#E65100")));
        egTrig.Setters.Add(new Setter(DataGridRow.FontWeightProperty, FontWeights.SemiBold));
        egresoStyle.Triggers.Add(egTrig);
        dg.RowStyle = egresoStyle;
        dg.ItemsSource = _movs;
        Grid.SetRow(dg, 0); Grid.SetColumn(dg, 0); Grid.SetColumnSpan(dg, 2); body.Children.Add(dg);

        Content = root;
    }

    // Lee lo que el cajero tecleó en el campo de un medio de pago puntual, o 0 si está vacío/
    // inválido — reutilizado tanto acá como al guardar (Guardar()).
    private decimal LeerContado(string clave)
    {
        var raw = _txtContado[clave].Text.Replace(".","").Replace(",","").Trim();
        return decimal.TryParse(raw, out var v) ? v : 0m;
    }

    private void RecalcDiferencia(Dictionary<string, decimal> netoPorMedio)
    {
        // El indicador de cuadrado/faltante/sobrante compara SOLO efectivo — pedido explícito
        // (ver comentario donde se arma _txtContado más arriba): tarjeta/transferencia/QR/cheque
        // son informativos, el cajero no los declara, así que no tiene sentido "diferenciarlos"
        // contra nada. Mismo criterio que el sistema viejo (ticket de cierre: "EFEC. SISTEMA" /
        // "EFEC. REAL" / "DIF. EFECTIVO", sin fila equivalente para los demás medios).
        foreach (var (clave, _) in MediosPago)
        {
            if (clave != "EFECTIVO" || !_lblDifPorMedio.ContainsKey(clave)) continue;
            var contado = LeerContado(clave);
            var neto    = netoPorMedio[clave];
            var diffMedio = contado - neto;
            var lbl = _lblDifPorMedio[clave];
            lbl.Text = diffMedio.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-PY"));
            if (neto < 0 && contado <= 0) {
                // Caja desfinanciada por diseño (ej. anticipos en efectivo superan lo ingresado
                // en efectivo ese día): un neto negativo no puede compensarse con dinero contado
                // negativo — el piso real es 0, no hay faltante físico.
                lbl.Foreground = RB("#E65100"); lbl.Background = RB("#FFF3E0");
            } else if (diffMedio > 0) {
                lbl.Foreground = RB("#1B5E20"); lbl.Background = RB("#E8F5E9");
            } else if (diffMedio < 0) {
                lbl.Foreground = RB("#B71C1C"); lbl.Background = RB("#FFEBEE");
            } else {
                lbl.Foreground = System.Windows.Media.Brushes.Black; lbl.Background = RB("#E0E0E0");
            }
        }

        var totalContado = LeerContado("EFECTIVO");
        var totalNeto    = netoPorMedio["EFECTIVO"];
        var diff = totalContado - totalNeto;
        if (totalNeto < 0 && totalContado <= 0)
        {
            _lblIndicador.Text = $"CAJA DESFINANCIADA\nGs. {Math.Abs(totalNeto):N0} salieron de otra fuente (no de esta caja)";
            _lblIndicador.Background = RB("#B71C1C");
            _lblIndicador.Foreground = System.Windows.Media.Brushes.White;
            _lblIndicador.Visibility = Visibility.Visible;
            return;
        }

        if (diff > 0) {
            _lblIndicador.Text = "SOBRANTE DE DINERO";
            _lblIndicador.Background = RB("#F9A825");
            _lblIndicador.Foreground = System.Windows.Media.Brushes.Black;
            _lblIndicador.Visibility = Visibility.Visible;
        } else if (diff < 0) {
            _lblIndicador.Text = "FALTANTE DE DINERO";
            _lblIndicador.Background = RB("#B71C1C");
            _lblIndicador.Foreground = System.Windows.Media.Brushes.White;
            _lblIndicador.Visibility = Visibility.Visible;
        } else if (totalContado > 0) {
            // diff == 0 con algo ya declarado: caja cuadrada — antes se ocultaba el indicador
            // por completo acá, dejando la pantalla sin ningún mensaje (reportado: el sistema
            // viejo muestra "CAJA CUADRADA" en verde en este caso, el nuevo no mostraba nada).
            _lblIndicador.Text = "CAJA CUADRADA";
            _lblIndicador.Background = RB("#2E7D32");
            _lblIndicador.Foreground = System.Windows.Media.Brushes.White;
            _lblIndicador.Visibility = Visibility.Visible;
        } else {
            // Todavía no se declaró nada (campo vacío/0) — no mostrar "CAJA CUADRADA" de forma
            // prematura antes de que el cajero cargue el efectivo real.
            _lblIndicador.Visibility = Visibility.Collapsed;
        }
    }

    private async Task ImprimirCierreCaja(
        CrediSoft.Core.Models.CajaMaster caja,
        decimal saldoIni, decimal ventasEf, decimal cobrosEf, decimal ingrEf, decimal egrEf,
        decimal anticipos, decimal totalNeto)
    {
        var (impresora, _) = await CrediSoft.UI.Views.Shared.TicketPrinter.ObtenerImpresoraAsync("reporte");

        var doc = new System.Drawing.Printing.PrintDocument { DocumentName = "Cierre de Caja" };

        doc.PrintPage += (_, e) =>
        {
            var g      = e.Graphics!;
            var bold9  = new System.Drawing.Font("Courier New", 9,  System.Drawing.FontStyle.Bold);
            var reg8   = new System.Drawing.Font("Courier New", 8,  System.Drawing.FontStyle.Regular);
            var bold10 = new System.Drawing.Font("Courier New", 10, System.Drawing.FontStyle.Bold);

            int lx = 20, y = 20;
            int w  = (int)(e.PageBounds.Width - 40);

            void Linea(string txt, System.Drawing.Font f, bool centrar = false)
            {
                var sz = g.MeasureString(txt, f);
                float fx = centrar ? (w - sz.Width) / 2f + lx : lx;
                g.DrawString(txt, f, System.Drawing.Brushes.Black, fx, y);
                y += (int)sz.Height + 2;
            }

            void Sep() { g.DrawLine(System.Drawing.Pens.Black, lx, y, lx + w, y); y += 4; }

            // Ticket recortado a solo EFECTIVO (+ TOTAL) — mismo criterio que la tabla en
            // pantalla: el cierre de caja acá es solo de efectivo, y el TOTAL antes sumaba
            // Tarjeta/Transferencia/Cheque/Otros que ni se ven ni se cobran en este cierre,
            // dejando el TOTAL NETO impreso desalineado con lo que el cajero vio en pantalla.
            void Fila(string concepto, decimal ef, bool bold = false)
            {
                var f = bold ? bold9 : reg8;
                g.DrawString(concepto, f, System.Drawing.Brushes.Black, lx, y);
                var s = ef.ToString("N0");
                var sz2 = g.MeasureString(s, bold9);
                g.DrawString(s, bold9, System.Drawing.Brushes.Black, lx + w - sz2.Width, y);
                y += bold ? 18 : 15;
            }

            // ── Encabezado ─────────────────────────────────────────────────────
            Linea("CIERRE DE CAJA", bold10, centrar: true);
            Linea($"Local: {_session.LocalActual!.NombreLocal}", reg8, centrar: true);
            Linea($"Cajero: {_session.UsuarioActual?.NombreUsuario ?? "-"}", reg8, centrar: true);
            Linea($"Apertura: {caja.FechaApertura:dd/MM/yyyy HH:mm}   Cierre: {DateTime.Now:dd/MM/yyyy HH:mm}", reg8, centrar: true);
            Sep();

            // ── Cabecera tabla ─────────────────────────────────────────────────
            g.DrawString("CONCEPTO",      bold9, System.Drawing.Brushes.Black, lx,       y);
            g.DrawString("EFECTIVO",      bold9, System.Drawing.Brushes.Black, lx + w - 90, y);
            y += 18; g.DrawLine(System.Drawing.Pens.Black, lx, y, lx + w, y); y += 3;

            // ── Filas ───────────────────────────────────────────────────────────
            Fila("SALDO INICIAL (+)",   saldoIni);
            Fila("VENTAS (+)",          ventasEf);
            Fila("COBROS SIST. (+)",    cobrosEf);
            Fila("INGR. MAN. (+)",      ingrEf);
            Fila("EGRESOS (-)",         egrEf);
            Fila("ANTICIPOS (-)",       anticipos);
            Sep();
            Fila("TOTAL NETO (SOLO EFECTIVO)", totalNeto, bold: true);
            Sep();

            // ── Contado (solo Efectivo, único medio que maneja este cierre) ─────
            var totalContado = LeerContado("EFECTIVO");
            Linea($"{"Efectivo",-14} contado:  Gs. {totalContado:N0}", bold9);
            var diferencia = totalContado - totalNeto;
            Linea($"Diferencia:        Gs. {diferencia:N0}  {(diferencia > 0 ? "(SOBRANTE)" : diferencia < 0 ? "(FALTANTE)" : "(OK)")}", bold9);
            if (!string.IsNullOrWhiteSpace(_txtObs?.Text))
                Linea($"Obs: {_txtObs.Text}", reg8);

            Sep();
            Linea("Sistema ElectroMar", reg8, centrar: true);

            bold9.Dispose(); reg8.Dispose(); bold10.Dispose();
            e.HasMorePages = false;
        };

        CrediSoft.UI.Views.Shared.TicketPrinter.ImprimirConConfig(doc, impresora);
    }

    private async Task Guardar()
    {
        if (string.IsNullOrWhiteSpace(_txtContado["EFECTIVO"].Text)) {
            MessageBox.Show("Ingrese el efectivo contado en caja.", "Validación",
                MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        var montoEfectivo = LeerContado("EFECTIVO");
        var montoTarjeta  = LeerContado("TARJETA");
        var montoTransf   = LeerContado("TRANSFERENCIA");
        var montoQr       = LeerContado("QR");
        var montoCheque   = LeerContado("CHEQUE");
        var montoReal     = montoEfectivo + montoTarjeta + montoTransf + montoQr + montoCheque;

        // Caja desfinanciada por diseño (egresos del día superaron lo ingresado en ESTA caja
        // puntual, ej. un pago de sueldo cubierto con plata de otra fuente). Si no hay ninguna
        // observación cargada, la sugerimos automáticamente para que quede trazable en el
        // historial — sin esto, un cierre en -524.500 queda indistinguible de un faltante real
        // por error de conteo o sustracción cuando se lo mira más adelante.
        if (_totalNetoActual < 0 && montoReal <= 0 && string.IsNullOrWhiteSpace(_txtObs?.Text))
        {
            // CAJA_MASTER.OBSERVACIONES tiene límite de 150 caracteres — el texto sugerido
            // original (189 chars) lo excedía y hacía fallar el Guardar con "Los datos de
            // cadena o binarios se truncarían" (Msg 8152), un error real de SQL en vez de
            // completar el cierre. Bug real reportado 2026-08-03: el cajero no podía cerrar
            // la caja aceptando la propia sugerencia automática del sistema.
            var sugerencia = $"Caja desfinanciada por diseño: egresos superan ingresos en " +
                $"Gs. {Math.Abs(_totalNetoActual):N0}. Dinero de otra fuente, no falta de esta caja.";
            var r = MessageBox.Show(
                "Esta caja cierra con TOTAL NETO (SOLO EFECTIVO) negativo y no cargaste ninguna observación.\n\n" +
                "¿Agregar automáticamente una nota explicando el motivo, para que quede clara " +
                "en el historial?\n\n" + sugerencia,
                "Sugerir observación", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (r == MessageBoxResult.Yes && _txtObs != null)
                _txtObs.Text = sugerencia;
        }

        // credenciales
        var cred = new CajaCredencialesDialog { Owner = this };
        if (cred.ShowDialog() != true) return;

        var nroComprobantePrev = _txtNroComprobanteDep?.Text.Trim() ?? "";
        var tieneNro  = !string.IsNullOrWhiteSpace(nroComprobantePrev);
        var tieneFoto = _fotoComprobanteDepSeleccionada != null;
        // Caja donde se registró un pago de salario (mismo criterio que ListarCierresRecientesAsync/
        // ObtenerCierrePorIdAsync): el efectivo salió como sueldo, no hay depósito bancario que
        // hacer, así que no corresponde exigir comprobante. No se condiciona al signo del neto —
        // TOT_INGRESOS/TOT_EGRESOS de CAJA_MASTER pueden no reflejarlo fielmente (ver caso real
        // con dos pagos de sueldo donde el neto quedó en 0 en vez de negativo).
        var esDesfinanciadaPorPagoSalario = _movs.Any(m => m.Tipo == "E" && m.SubTipo == "PAGO");
        var estadoComprobante =
            esDesfinanciadaPorPagoSalario ? EstadoComprobanteCierre.NoAplica :
            tieneNro && tieneFoto ? EstadoComprobanteCierre.Completo :
            !tieneNro && !tieneFoto ? EstadoComprobanteCierre.SinCargar :
            tieneNro ? EstadoComprobanteCierre.FaltaFoto : EstadoComprobanteCierre.FaltaNro;

        var confirmDlg = new CajaConfirmarCierreDialog(montoReal, _movs.Count, estadoComprobante) { Owner = this };
        if (confirmDlg.ShowDialog() != true) return;

        try {
            var ok = await _caja.CerrarCajaAsync(_idMaster, cred.UsuarioId, montoEfectivo, montoTarjeta,
                montoTransf, montoQr, montoCheque, _txtObs?.Text ?? "");
            if (ok) {
                // Comprobante de depósito — completamente opcional en este momento, nunca
                // bloquea el cierre ya consolidado arriba. Si el cajero no cargó nada, queda
                // pendiente y disponible por 48hs desde CajaComprobantesPendientesWindow.
                var nroComprobante = _txtNroComprobanteDep?.Text.Trim() ?? "";
                if (!string.IsNullOrWhiteSpace(nroComprobante) || _fotoComprobanteDepSeleccionada != null)
                {
                    try
                    {
                        await _caja.GuardarComprobanteDepositoAsync(
                            _idMaster, nroComprobante, _fotoComprobanteDepSeleccionada);
                    }
                    catch (Exception exComp)
                    {
                        MessageBox.Show(
                            "El cierre se guardó correctamente, pero no se pudo guardar el " +
                            "comprobante de depósito: " + exComp.Message +
                            "\n\nPodrá cargarlo después desde \"Comprobantes de Depósito Pendientes\".",
                            "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                MessageBox.Show("¡El Cierre de Caja se ha consolidado correctamente en ELECTROMAR!",
                    "ElectroMar", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            } else {
                MessageBox.Show("Ocurrió un error al cerrar la caja. Verifique e intente nuevamente.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        } catch (Exception ex) {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

public enum EstadoComprobanteCierre { Completo, SinCargar, FaltaNro, FaltaFoto, NoAplica }

// Modal de confirmación de cierre — reemplaza el MessageBox plano para poder mostrar,
// cuando corresponde, un bloque destacado aparte avisando qué falta del comprobante de
// depósito (N° y/o foto) y que hay 48hs de plazo para completarlo después.
public class CajaConfirmarCierreDialog : Window
{
    public CajaConfirmarCierreDialog(decimal montoCierre, int cantMovimientos, EstadoComprobanteCierre estadoComprobante)
    {
        Title = "Confirmar Cierre";
        Width = 460; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");

        var root = new DockPanel();

        var hdr = new Border { Background = RB("#0E2F44"), Padding = new Thickness(18,14,18,14) };
        var hdrSp = new StackPanel { Orientation = Orientation.Horizontal };
        hdrSp.Children.Add(new TextBlock { Text = "⚠", FontSize = 20,
            Foreground = System.Windows.Media.Brushes.White, Margin = new Thickness(0,0,10,0),
            VerticalAlignment = VerticalAlignment.Center });
        hdrSp.Children.Add(new TextBlock { Text = "CONFIRMAR CIERRE DE CAJA",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 15,
            FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
        hdr.Child = hdrSp;
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        var pie = new Border { Padding = new Thickness(18,12,18,12),
            BorderBrush = RB("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0) };
        var pieSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var btnSi = new Button { Content = "✔ Sí, procesar cierre", Height = 36, Padding = new Thickness(18,0,18,0),
            Background = RB("#1B5E20"), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, FontSize = 13,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Margin = new Thickness(0,0,8,0) };
        var btnNo = new Button { Content = "Cancelar", Height = 36, Padding = new Thickness(18,0,18,0),
            Background = RB("#546E7A"), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, FontSize = 13,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        btnSi.Click += (_, _) => { DialogResult = true; Close(); };
        btnNo.Click += (_, _) => { DialogResult = false; Close(); };
        pieSp.Children.Add(btnSi); pieSp.Children.Add(btnNo);
        pie.Child = pieSp;
        DockPanel.SetDock(pie, Dock.Bottom); root.Children.Add(pie);

        var body = new StackPanel { Margin = new Thickness(20,16,20,16) };

        body.Children.Add(new TextBlock {
            Text = "¿Está seguro que desea procesar el CIERRE DEFINITIVO de la caja?",
            FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = RB("#263238"),
            TextWrapping = TextWrapping.Wrap });
        body.Children.Add(new TextBlock {
            Text = "Esta acción consolidará los totales y cerrará el turno actual.",
            FontSize = 12, Foreground = RB("#607D8B"), TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0,3,0,0) });

        // Resumen del cierre — para que no se pierda de vista qué se está por consolidar.
        var resumen = new Border { Background = RB("#F5F5F5"), BorderBrush = RB("#E0E0E0"),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
            Padding = new Thickness(14,10,14,10), Margin = new Thickness(0,14,0,0) };
        var resumenG = new Grid();
        resumenG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        resumenG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        void ResItem(string lbl, string val, int col) {
            var sp = new StackPanel(); Grid.SetColumn(sp, col);
            sp.Children.Add(new TextBlock { Text = lbl, FontSize = 10, FontWeight = FontWeights.Bold,
                Foreground = RB("#78909C") });
            sp.Children.Add(new TextBlock { Text = val, FontSize = 15, FontWeight = FontWeights.Bold,
                Foreground = RB("#0E2F44"), Margin = new Thickness(0,2,0,0) });
            resumenG.Children.Add(sp);
        }
        ResItem("EFECTIVO A CERRAR", $"Gs. {montoCierre:N0}", 0);
        ResItem("MOVIMIENTOS", cantMovimientos.ToString(), 1);
        resumen.Child = resumenG;
        body.Children.Add(resumen);

        if (estadoComprobante == EstadoComprobanteCierre.NoAplica)
        {
            // No es una advertencia — es una aclaración para que quede claro por qué esta caja
            // NO va a pedir comprobante de depósito, y que eso no es un olvido ni un error.
            var info = new Border { Background = RB("#E3F2FD"), BorderBrush = RB("#1565C0"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
                Padding = new Thickness(14,10,14,10), Margin = new Thickness(0,12,0,0) };
            var infoSp = new StackPanel();
            var infoTitleSp = new StackPanel { Orientation = Orientation.Horizontal };
            infoTitleSp.Children.Add(new TextBlock { Text = "ℹ", FontSize = 13, Margin = new Thickness(0,0,6,0),
                Foreground = RB("#0D47A1") });
            infoTitleSp.Children.Add(new TextBlock { Text = "NO SE PEDIRÁ COMPROBANTE DE DEPÓSITO",
                FontSize = 11, FontWeight = FontWeights.Bold, Foreground = RB("#0D47A1") });
            infoSp.Children.Add(infoTitleSp);
            infoSp.Children.Add(new TextBlock {
                Text = "Esta caja quedó en negativo porque en el sistema se registró un PAGO DE SALARIO, " +
                    "no un depósito bancario. El efectivo salió como sueldo, así que no corresponde " +
                    "exigir un comprobante de depósito que nunca va a existir.",
                FontSize = 11.5, Foreground = RB("#0D47A1"), TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0,4,0,0) });
            info.Child = infoSp;
            body.Children.Add(info);
        }
        else if (estadoComprobante != EstadoComprobanteCierre.Completo)
        {
            var (titulo, detalle) = estadoComprobante switch
            {
                EstadoComprobanteCierre.FaltaFoto => (
                    "FALTA LA FOTO DEL COMPROBANTE",
                    "Cargó el N° de comprobante pero no adjuntó la foto del depósito. Podrá completarla " +
                    "dentro de las 48hs desde \"Comprobantes de Depósito Pendientes\"."),
                EstadoComprobanteCierre.FaltaNro => (
                    "FALTA EL N° DE COMPROBANTE",
                    "Adjuntó la foto del depósito pero no cargó el N° de comprobante. Podrá completarlo " +
                    "dentro de las 48hs desde \"Comprobantes de Depósito Pendientes\"."),
                _ => (
                    "COMPROBANTE DE DEPÓSITO PENDIENTE",
                    "No cargó el N° de comprobante ni la foto del depósito. Podrá completarlo " +
                    "dentro de las 48hs desde \"Comprobantes de Depósito Pendientes\"."),
            };

            var warn = new Border { Background = RB("#FFF8E1"), BorderBrush = RB("#FFB300"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
                Padding = new Thickness(14,10,14,10), Margin = new Thickness(0,12,0,0) };
            var warnSp = new StackPanel();
            var warnTitleSp = new StackPanel { Orientation = Orientation.Horizontal };
            warnTitleSp.Children.Add(new TextBlock { Text = "📋", FontSize = 13, Margin = new Thickness(0,0,6,0) });
            warnTitleSp.Children.Add(new TextBlock { Text = titulo,
                FontSize = 11, FontWeight = FontWeights.Bold, Foreground = RB("#8D6E00") });
            warnSp.Children.Add(warnTitleSp);
            warnSp.Children.Add(new TextBlock {
                Text = detalle,
                FontSize = 11.5, Foreground = RB("#6D4C00"), TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0,4,0,0) });
            warnSp.Children.Add(new TextBlock {
                Text = "Pasado ese plazo, quedará marcado como vencido para revisión del dueño del negocio.",
                FontSize = 10.5, Foreground = RB("#9C7A00"), TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0,4,0,0), FontStyle = FontStyles.Italic });
            warn.Child = warnSp;
            body.Children.Add(warn);
        }

        root.Children.Add(body);
        Content = root;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { DialogResult = false; Close(); } };
    }

    private static System.Windows.Media.SolidColorBrush RB(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));
}

internal class FilaCierre
{
    public string  Accion   { get; set; } = "";
    public string  Concepto { get; set; } = "";
    public string  Detalle  { get; set; } = "";
    public decimal Monto    { get; set; }
    public string  Metodo   { get; set; } = "";
    public string  Numero   { get; set; } = "";
    public string  Fecha    { get; set; } = "";
    public string  Hora     { get; set; } = "";
    public string  Tipo     { get; set; } = "";
    public string  SubTipo  { get; set; } = "";
    public string  EstadoReg{ get; set; } = "";
}

// ── Arqueo de Caja ────────────────────────────────────────────────────────────
// Modal de criterios de búsqueda con 3 columnas lado a lado (Entrada / Salida /
// Entrada-Salida) — replica el diseño del sistema viejo. No hay grilla en pantalla:
// cada columna dispara directo su propio reporte imprimible (resumen o detallado).
public class CajaArqueoWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private readonly ISessionService      _session;

    private DatePicker _dpDesde = null!;
    private DatePicker _dpHasta = null!;

    private List<FilaCaja> _todosEnt = new();
    private List<FilaCaja> _todosSal = new();
    private List<FilaCaja> _todosES  = new();
    private bool _datosCargados = false;

    private static System.Windows.Media.SolidColorBrush CB(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    public CajaArqueoWindow()
    {
        _db      = App.Services.GetRequiredService<IDbConnectionFactory>();
        _session = SessionService.Instance;
        Title  = "Arqueo de caja";
        Width  = 700; Height = 620;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = CB("#EEF1F8");
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        FontSize   = 12;
        BuildUI();
    }

    private ColumnaArqueo _colEnt = null!, _colSal = null!, _colES = null!;

    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // fechas
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // columnas
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // pie

        // ── Header — ícono + título + subtítulo, mismo lenguaje visual que el
        // resto de la app (Proveedores, Aceptar Transferencia, etc.) ──────────
        var hdr = new Border { Background = CB("#283593"), Padding = new Thickness(20, 16, 20, 16) };
        Grid.SetRow(hdr, 0);
        var hdrRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        hdrRow.Children.Add(new Border {
            Width = 38, Height = 38, CornerRadius = new CornerRadius(19),
            Background = CB("#3F51B5"), Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = "🔎", FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        });
        var hdrTexts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        hdrTexts.Children.Add(new TextBlock { Text = "ARQUEO DE CAJA", FontSize = 15,
            FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White });
        hdrTexts.Children.Add(new TextBlock { Text = "Criterios de búsqueda por local y período", FontSize = 10.5,
            Foreground = CB("#B3BAF0"), Margin = new Thickness(0, 2, 0, 0) });
        hdrRow.Children.Add(hdrTexts);
        hdr.Child = hdrRow;
        root.Children.Add(hdr);

        // ── Fechas ──────────────────────────────────────────────────────────
        var fBar = new Border { Background = CB("#D1D5F0"), Padding = new Thickness(14, 10, 14, 10),
            BorderBrush = CB("#B3BAF0"), BorderThickness = new Thickness(0, 0, 0, 1) };
        Grid.SetRow(fBar, 1);
        var fSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        TextBlock FechaLbl(string t) => new() { Text = t, VerticalAlignment = VerticalAlignment.Center,
            Foreground = CB("#283593"), FontWeight = FontWeights.SemiBold, FontSize = 11, Margin = new Thickness(0, 0, 6, 0) };
        // Pedido explícito: arrancar con el día de hoy en ambos campos (no 3 meses atrás) —
        // el caso de uso más común es revisar el arqueo del día actual; el usuario sigue
        // pudiendo ampliar el rango a mano si necesita un período más largo.
        _dpDesde = new DatePicker { SelectedDate = DateTime.Today,
            Width = 130, Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center };
        _dpHasta = new DatePicker { SelectedDate = DateTime.Today,
            Width = 130, VerticalAlignment = VerticalAlignment.Center };
        fSp.Children.Add(FechaLbl("Desde:"));
        fSp.Children.Add(_dpDesde);
        fSp.Children.Add(FechaLbl("Hasta:"));
        fSp.Children.Add(_dpHasta);
        fBar.Child = fSp;
        root.Children.Add(fBar);

        // ── 3 columnas ────────────────────────────────────────────────────────
        var cols = new Grid { Margin = new Thickness(14, 14, 14, 14) };
        cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(cols, 2);

        _colEnt = new ColumnaArqueo("ENTRADA", CB("#1B5E20"), 0, this);
        _colSal = new ColumnaArqueo("SALIDA",  CB("#B71C1C"), 1, this);
        _colES  = new ColumnaArqueo("ENTRADA - SALIDA", CB("#283593"), 2, this);

        Grid.SetColumn(_colEnt.Root, 0); cols.Children.Add(_colEnt.Root);
        Grid.SetColumn(_colSal.Root, 2); cols.Children.Add(_colSal.Root);
        Grid.SetColumn(_colES.Root,  4); cols.Children.Add(_colES.Root);

        // Scroll de respaldo: si el contenido de las tarjetas alguna vez supera el
        // alto disponible, aparece una barra de scroll en vez de recortar silenciosamente
        // el botón "Buscar" del resumen (como pasó con Height fija + NoResize).
        var scrollCols = new ScrollViewer {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = cols };
        Grid.SetRow(scrollCols, 2);
        root.Children.Add(scrollCols);

        // ── Pie ───────────────────────────────────────────────────────────────
        var pie = new Border { Background = CB("#283593"), Padding = new Thickness(16, 10, 16, 10) };
        Grid.SetRow(pie, 3);
        var pieSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var btnCerrar = new Button { Content = "✕  Cerrar", Height = 34,
            Padding = new Thickness(20, 0, 20, 0),
            Background = CB("#5C6BC0"), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, FontSize = 12, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        btnCerrar.Click += (_, _) => Close();
        pieSp.Children.Add(btnCerrar);
        pie.Child = pieSp;
        root.Children.Add(pie);

        Content = root;
    }

    // Solo un ADMINISTRADOR (o el usuario con excepción puntual, ver
    // Usuario.PuedeVerTodosLosLocales) puede arquear "Todos los locales" o un local
    // específico distinto al propio. Un usuario normal arquea siempre SU local — el
    // combo "Locales" y el campo de código quedan fijos/deshabilitados.
    private bool EsAdmin => _session.UsuarioActual?.PuedeVerTodosLosLocales == true;
    private int?   LocalSesion       => _session.LocalActual?.IdLocal;
    private string? LocalNombreSesion => _session.LocalActual?.NombreLocal;

    // ── Carga de datos (una sola vez, todas las columnas comparten el resultado) ──
    private async Task<bool> AsegurarDatosAsync()
    {
        if (_datosCargados) return true;

        var desde = _dpDesde.SelectedDate ?? DateTime.Today.AddMonths(-3);
        var hasta = _dpHasta.SelectedDate?.AddDays(1) ?? DateTime.Today.AddDays(1);

        try
        {
            using var conn = _db.Create();
            // CAJA_DETALLE/CAJA_MASTER es el sistema de caja realmente activo (apertura/cierre
            // diario por local vía sp_AbrirCaja_CS/sp_CerrarCaja_CS) — la tabla legacy DET_CAJA/
            // CAB_CAJA prácticamente dejó de recibir movimientos (verificado: toda la red de
            // locales solo escribió 1 fila ahí en un día con miles de operaciones reales en
            // CAJA_DETALLE) y además nunca reflejaba si la caja seguía abierta, así que el Arqueo
            // no mostraba nada de una caja que el cajero todavía no había cerrado. Se filtra
            // ESTADO_REG='V' para excluir movimientos anulados (ver ArqueoLocalSelectorModal).
            // USUARIO se agrupa por ID_VENDEDOR cuando el movimiento lo tiene registrado (a
            // quién se le atribuye realmente la venta/cobro — puede ser distinto de quien tenía
            // la caja física abierta, ej. un vendedor sin caja propia pide a otro cajero que le
            // cobre una cuota) — igual criterio que FilaHistCajaRaw.Cobrador (ver comentario ahí).
            // Antes este reporte agrupaba SIEMPRE por ID_CAJERO, así que una venta de Mabel
            // Escobar (ID_VENDEDOR ya grabado correctamente) aparecía en la sección de Chisel
            // Martinez (quien solo tenía la caja física abierta) — bug real reportado: el arqueo
            // no coincidía con Historial de Cobranzas, que sí usa el vendedor real. Se cae a
            // ID_CAJERO cuando no hay vendedor (aperturas, gastos, y demás movimientos sin venta
            // asociada, donde ID_VENDEDOR queda en 0/NULL).
            var rows = (await conn.QueryAsync<FilaCajaRaw>(@"
                SELECT D.ID_DETALLE AS IDCAJA, D.ID_MASTER AS IDCABCAJA,
                       D.ID_LOCAL,
                       ISNULL(L.NOMBRE, CAST(D.ID_LOCAL AS VARCHAR)) AS LOCAL_NOMBRE,
                       ISNULL(UV.NOMBRE_USUARIO, ISNULL(UC.NOMBRE_USUARIO, '—')) AS USUARIO,
                       D.ID_MASTER AS CAJA,
                       CASE D.TIPO WHEN 'I' THEN 'Entrada' WHEN 'E' THEN 'Salida' ELSE '' END AS ACCION,
                       D.SUBTIPO AS CONCEPTO,
                       D.MONTO,
                       CASE D.FORMA_PAGO WHEN 'EFECTIVO'      THEN 'Efectivo'
                                         WHEN 'CHEQUE'        THEN 'Cheque'
                                         WHEN 'TARJETA'       THEN 'Tarjeta'
                                         WHEN 'TRANSFERENCIA' THEN 'Transferencia'
                                         ELSE 'Otro' END AS METODO,
                       ISNULL(D.CONCEPTO,'') AS OBSERVACION,
                       D.FECHA_HORA AS FECHA
                FROM CAJA_DETALLE D
                LEFT JOIN USUARIOS UC ON UC.ID_USUARIO = D.ID_CAJERO
                LEFT JOIN USUARIOS UV ON UV.ID_USUARIO = D.ID_VENDEDOR AND D.ID_VENDEDOR > 0
                LEFT JOIN LOCALES  L ON L.ID_LOCAL    = D.ID_LOCAL
                WHERE D.ESTADO_REG = 'V' AND D.FECHA_HORA BETWEEN @desde AND @hasta
                ORDER BY D.FECHA_HORA DESC",
                new { desde, hasta })).ToList();

            _todosEnt = rows.Where(r => r.ACCION == "Entrada").Select(Map).ToList();
            _todosSal = rows.Where(r => r.ACCION == "Salida").Select(Map).ToList();
            _todosES  = rows.Select(Map).ToList();
            _datosCargados = true;
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    // Invalida el caché de datos cuando cambian las fechas — cada columna vuelve a
    // consultar en su próxima búsqueda.
    private void InvalidarDatos() => _datosCargados = false;

    private static FilaCaja Map(FilaCajaRaw r) => new() {
        IdLocal  = r.ID_LOCAL,
        Local    = r.LOCAL_NOMBRE,
        Usuario  = r.USUARIO,
        Concepto = r.CONCEPTO,
        Metodo   = r.METODO,
        Monto    = r.MONTO,
        Fecha    = r.FECHA,
        FechaStr = r.FECHA.ToString("dd/MM/yyyy HH:mm"),
        Obs      = r.OBSERVACION ?? "",
        Accion   = r.ACCION
    };

    private List<FilaCaja> DatosDe(int accion) => accion switch {
        0 => _todosEnt, 1 => _todosSal, _ => _todosES
    };

    // ── Reporte RESUMEN (agrupado por local, con el total del tab activo) ───────
    private async void AbrirReporteResumen(int accion, int? idLocalFiltro)
    {
        InvalidarDatos();
        if (!await AsegurarDatosAsync()) return;
        if (!IsLoaded) return;

        var todos = DatosDe(accion);
        var filtrado = idLocalFiltro.HasValue ? todos.Where(r => r.IdLocal == idLocalFiltro.Value).ToList() : todos;

        if (filtrado.Count == 0)
        {
            MostrarSinResultados(accion, idLocalFiltro);
            return;
        }

        // Ingresos y Egresos por separado — antes el reporte "ENTRADA - SALIDA" sumaba TODOS
        // los montos como positivos (Sum(x => x.Monto)), así que el "Total" mostrado era en
        // realidad ingresos+egresos juntos, no el neto real de la caja. Con Accion ("Entrada"/
        // "Salida", ya disponible en FilaCaja) se puede separar y mostrar ambos montos más el
        // neto verdadero (Ingresos - Egresos).
        var filas = filtrado
            .GroupBy(r => r.Local)
            .Select(g => {
                var ingresos = g.Where(x => x.Accion == "Entrada").Sum(x => x.Monto);
                var egresos  = g.Where(x => x.Accion == "Salida").Sum(x => x.Monto);
                return new FilaArqueoResumen(g.Key, ingresos - egresos, g.Count(), ingresos, egresos);
            })
            .OrderBy(f => f.Local)
            .ToList();

        var tituloTipo = accion switch { 0 => "ENTRADA", 1 => "SALIDA", _ => "ENTRADA - SALIDA" };
        var pagina = new CajaArqueoResumenPagina {
            Filas      = filas,
            TituloTipo = tituloTipo,
            Desde      = (_dpDesde.SelectedDate ?? DateTime.Today).ToString("d/M/yyyy"),
            Hasta      = (_dpHasta.SelectedDate ?? DateTime.Today).ToString("d/M/yyyy"),
            FechaImp   = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            Usuario    = _session.UsuarioActual?.NombreUsuario ?? "",
            LogoPath   = CajaArqueoResumenPagina.ResolverLogoPath(),
        };
        new CajaArqueoResumenPreviewWindow(pagina) { Owner = this }.ShowDialog();
    }

    // ── Reporte DETALLADO (agrupado por usuario/cajero, fila por fila) ──────────
    private async void AbrirReporteDetalle(int accion, int? idLocalFiltro)
    {
        InvalidarDatos();
        if (!await AsegurarDatosAsync()) return;
        if (!IsLoaded) return;

        var todos = DatosDe(accion);
        var filtrado = idLocalFiltro.HasValue ? todos.Where(r => r.IdLocal == idLocalFiltro.Value).ToList() : todos;

        if (filtrado.Count == 0)
        {
            MostrarSinResultados(accion, idLocalFiltro);
            return;
        }

        var filas = new List<FilaArqueoDetalle>();
        foreach (var grupo in filtrado.GroupBy(r => r.Usuario).OrderBy(g => g.Key))
        {
            var localesDelUsuario = grupo.Select(r => r.Local).Distinct().ToList();
            var etiquetaLocal = localesDelUsuario.Count == 1 ? localesDelUsuario[0] : "";
            filas.Add(new FilaArqueoDetalle(true, grupo.Key, etiquetaLocal, "", "", 0, "", ""));
            foreach (var r in grupo.OrderBy(r => r.Fecha))
            {
                var tipo = r.Accion == "Entrada" ? "Ingreso" : r.Accion == "Salida" ? "Egreso" : "";
                filas.Add(new FilaArqueoDetalle(false, "", r.Local, r.Concepto, r.Metodo, r.Monto, r.FechaStr, r.Obs, tipo));
            }
        }

        var tituloTipo = accion switch { 0 => "ENTRADAS", 1 => "SALIDAS", _ => "ENTRADA - SALIDA" };
        var pagina = new CajaArqueoDetallePagina {
            Filas      = filas,
            TituloTipo = tituloTipo,
            Desde      = (_dpDesde.SelectedDate ?? DateTime.Today).ToString("d/M/yyyy"),
            Hasta      = (_dpHasta.SelectedDate ?? DateTime.Today).ToString("d/M/yyyy"),
            FechaImp   = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            Usuario    = _session.UsuarioActual?.NombreUsuario ?? "",
            LogoPath   = CajaArqueoDetallePagina.ResolverLogoPath(),
        };
        new CajaArqueoDetallePreviewWindow(pagina) { Owner = this }.ShowDialog();
    }

    // Pedido explícito: el MessageBox genérico "No se encontraron movimientos para los
    // criterios seleccionados" no decía CUÁLES eran esos criterios — el usuario tenía que
    // acordarse de memoria qué había tipeado en los filtros para saber si el problema era
    // un filtro mal puesto o realmente no hubo actividad. Repite tipo, local y rango de
    // fechas elegidos, tal como se buscaron.
    private async void MostrarSinResultados(int accion, int? idLocalFiltro)
    {
        var tituloTipo = accion switch { 0 => "Entradas", 1 => "Salidas", _ => "Entrada - Salida" };
        // Esta ventana no mantiene su propia lista de locales cacheada (a diferencia de
        // CajaAperturaWindow/CajaHistorialWindow) — se resuelve el nombre con una consulta
        // puntual, más simple que cargar y mantener otra lista solo para este mensaje.
        var nombreLocal = "Todos los locales";
        if (idLocalFiltro.HasValue)
        {
            using var conn = _db.Create();
            nombreLocal = await conn.ExecuteScalarAsync<string>(
                "SELECT NOMBRE FROM LOCALES WHERE ID_LOCAL = @id", new { id = idLocalFiltro.Value })
                ?? $"Local #{idLocalFiltro}";
        }
        var desdeTxt = (_dpDesde.SelectedDate ?? DateTime.Today).ToString("dd/MM/yyyy");
        var hastaTxt = (_dpHasta.SelectedDate ?? DateTime.Today).ToString("dd/MM/yyyy");
        new SinResultadosArqueoModal(tituloTipo, nombreLocal, desdeTxt, hastaTxt) { Owner = this }.ShowDialog();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) Close();
    }

    // ── Una columna de criterios (Entrada / Salida / Entrada-Salida) ─────────────
    private class ColumnaArqueo
    {
        public Border Root { get; }
        private readonly CajaArqueoWindow _owner;
        private readonly int _accion;
        private readonly ComboBox _cboLocales;
        private readonly ComboBox _cboAgrupar;
        private int? _idLocalDetalle;
        private int? _idLocalResumen;

        public ColumnaArqueo(string titulo, System.Windows.Media.SolidColorBrush color, int accion, CajaArqueoWindow owner)
        {
            _owner  = owner;
            _accion = accion;

            var card = new Border {
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = CB("#DDE1F5"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(0, 0, 0, 14),
                Effect = new System.Windows.Media.Effects.DropShadowEffect {
                    Color = CB("#000000").Color, Opacity = 0.10, BlurRadius = 10, ShadowDepth = 2, Direction = 270 },
            };
            var sp = new StackPanel();

            // Banda de título con fondo del color de la columna — más presencia visual
            // que solo texto de color sobre blanco.
            var tituloBanda = new Border {
                Background = color, CornerRadius = new CornerRadius(7, 7, 0, 0),
                Padding = new Thickness(0, 10, 0, 10), Margin = new Thickness(0, 0, 0, 12),
            };
            tituloBanda.Child = new TextBlock { Text = titulo, HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold, FontSize = 12.5 };
            sp.Children.Add(tituloBanda);

            var body = new StackPanel { Margin = new Thickness(14, 0, 14, 0) };
            sp.Children.Add(body);
            var spOriginal = sp;
            sp = body; // el resto del constructor sigue agregando a "sp" — ahora es el body con margen interno

            TextBlock Lbl(string t) => new() { Text = t, FontSize = 10.5, Foreground = CB("#495057"),
                FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 3) };

            var esAdmin = owner.EsAdmin;

            // ── Selector de local para el reporte DETALLADO ──────────────────
            sp.Children.Add(Lbl("Ver local"));
            _cboLocales = new ComboBox { Height = 30, Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(8, 4, 8, 4), Background = System.Windows.Media.Brushes.White,
                BorderBrush = CB("#CED4DA"), FontSize = 12 };
            _cboLocales.Items.Add(new ComboBoxItem { Content = "Todos los locales" });
            _cboLocales.Items.Add(new ComboBoxItem { Content = "Un local específico..." });
            sp.Children.Add(_cboLocales);

            // Selector de local de solo lectura: en vez de tipear el código de memoria,
            // un clic abre ArqueoLocalSelectorModal con el listado de locales (nombre + id)
            // para elegir con el mouse.
            var lblLocal = Lbl("Local");
            lblLocal.Visibility = Visibility.Collapsed;
            sp.Children.Add(lblLocal);
            var (selLocalBorder, selLocalTxt) = CrearSelectorLocal(owner, id =>
            {
                _idLocalDetalle = id;
            });
            selLocalBorder.Visibility = Visibility.Collapsed;
            sp.Children.Add(selLocalBorder);

            if (!esAdmin)
            {
                // Usuario normal: fijo en su local, sin poder ver "Todos" ni otro específico.
                _cboLocales.SelectedIndex = 1;
                _cboLocales.IsEnabled = false;
                _idLocalDetalle = owner.LocalSesion;
                selLocalTxt.Text = owner.LocalNombreSesion ?? owner.LocalSesion?.ToString() ?? "";
                selLocalBorder.IsEnabled = false;
                lblLocal.Visibility = Visibility.Visible;
                selLocalBorder.Visibility = Visibility.Visible;
            }
            else
            {
                _cboLocales.SelectedIndex = 0;
                _cboLocales.SelectionChanged += (_, _) =>
                {
                    var mostrar = _cboLocales.SelectedIndex == 1;
                    lblLocal.Visibility       = mostrar ? Visibility.Visible : Visibility.Collapsed;
                    selLocalBorder.Visibility = mostrar ? Visibility.Visible : Visibility.Collapsed;
                };
            }

            // Botón "Buscar" → reporte DETALLADO
            sp.Children.Add(BotonBuscar(color, "reporte detallado",
                () => owner.AbrirReporteDetalle(_accion, ObtenerIdLocalFiltro()), margenInferior: 14));

            sp.Children.Add(new Border { Height = 1, Background = CB("#E9ECEF"), Margin = new Thickness(0, 0, 0, 12) });

            // ── Selector de local para el reporte RESUMEN (independiente del de arriba) ──
            sp.Children.Add(Lbl("Ver local (resumen)"));
            _cboAgrupar = new ComboBox { Height = 30, Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(8, 4, 8, 4), Background = System.Windows.Media.Brushes.White,
                BorderBrush = CB("#CED4DA"), FontSize = 12 };
            _cboAgrupar.Items.Add(new ComboBoxItem { Content = "Todos los locales" });
            _cboAgrupar.Items.Add(new ComboBoxItem { Content = "Un local específico..." });
            sp.Children.Add(_cboAgrupar);

            var lblLocalResumen = Lbl("Local");
            lblLocalResumen.Visibility = Visibility.Collapsed;
            sp.Children.Add(lblLocalResumen);
            var (selResumenBorder, selResumenTxt) = CrearSelectorLocal(owner, id =>
            {
                _idLocalResumen = id;
            });
            selResumenBorder.Visibility = Visibility.Collapsed;
            sp.Children.Add(selResumenBorder);

            if (!esAdmin)
            {
                // Usuario normal: el resumen tambien queda fijo en su local.
                _cboAgrupar.SelectedIndex = 1;
                _cboAgrupar.IsEnabled = false;
                _idLocalResumen = owner.LocalSesion;
                selResumenTxt.Text = owner.LocalNombreSesion ?? owner.LocalSesion?.ToString() ?? "";
                selResumenBorder.IsEnabled = false;
                lblLocalResumen.Visibility = Visibility.Visible;
                selResumenBorder.Visibility = Visibility.Visible;
            }
            else
            {
                _cboAgrupar.SelectedIndex = 0;
                _cboAgrupar.SelectionChanged += (_, _) =>
                {
                    var mostrar = _cboAgrupar.SelectedIndex == 1;
                    lblLocalResumen.Visibility  = mostrar ? Visibility.Visible : Visibility.Collapsed;
                    selResumenBorder.Visibility = mostrar ? Visibility.Visible : Visibility.Collapsed;
                };
            }

            // Botón "Buscar" → reporte RESUMEN
            sp.Children.Add(BotonBuscar(color, "reporte resumen",
                () => owner.AbrirReporteResumen(_accion, ObtenerIdLocalFiltroResumen()), margenInferior: 0));

            card.Child = spOriginal;
            Root = card;
        }

        // Control de solo lectura que abre ArqueoLocalSelectorModal al hacer clic —
        // evita que el usuario tenga que memorizar/tipear el código numérico del local.
        private static (Border Root, TextBlock Txt) CrearSelectorLocal(CajaArqueoWindow owner, Action<int?> onSeleccion)
        {
            var border = new Border { Height = 30, Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(10, 0, 8, 0), Background = System.Windows.Media.Brushes.White,
                BorderBrush = CB("#CED4DA"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4), Cursor = Cursors.Hand };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var txt = new TextBlock { Text = "Seleccionar local...", FontSize = 12,
                Foreground = CB("#ADB5BD"), VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis };
            var ico = new TextBlock { Text = "🔽", FontSize = 9, Foreground = CB("#6C757D"),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
            Grid.SetColumn(txt, 0); Grid.SetColumn(ico, 1);
            grid.Children.Add(txt); grid.Children.Add(ico);
            border.Child = grid;

            border.MouseLeftButtonUp += (_, _) =>
            {
                if (!border.IsEnabled) return;
                var modal = new ArqueoLocalSelectorModal(owner._db) { Owner = owner };
                if (modal.ShowDialog() == true)
                {
                    txt.Text = modal.LocalNombre;
                    txt.Foreground = CB("#212529");
                    onSeleccion(modal.LocalId);
                }
            };

            return (border, txt);
        }

        // Border clickeable en vez de Button — un Button con Background=Transparent y
        // BorderThickness=0 colapsa su área de contenido con el ControlTemplate por
        // defecto de WPF (mismo problema ya visto antes en otros módulos), dejando la
        // lupa invisible o apenas un texto flotando sin fondo/click area reconocible.
        private static Border BotonBuscar(System.Windows.Media.SolidColorBrush color, string tooltipSufijo, Action onClick, double margenInferior)
        {
            var border = new Border {
                Background = color, CornerRadius = new CornerRadius(5),
                Padding = new Thickness(14, 8, 14, 8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 0, margenInferior),
                ToolTip = $"Buscar — {tooltipSufijo}" };
            var rowSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            rowSp.Children.Add(new TextBlock { Text = "🔍", FontSize = 12, Foreground = System.Windows.Media.Brushes.White,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            rowSp.Children.Add(new TextBlock { Text = "Buscar", FontSize = 11.5, FontWeight = FontWeights.SemiBold,
                Foreground = System.Windows.Media.Brushes.White, VerticalAlignment = VerticalAlignment.Center });
            border.Child = rowSp;
            border.MouseLeftButtonUp += (_, _) => onClick();
            border.MouseEnter += (_, _) => border.Opacity = 0.88;
            border.MouseLeave += (_, _) => border.Opacity = 1.0;
            return border;
        }

        private int? ObtenerIdLocalFiltro()
        {
            if (_cboLocales.SelectedIndex != 1) return null; // "Todos"
            return _idLocalDetalle;
        }

        private int? ObtenerIdLocalFiltroResumen()
        {
            if (_cboAgrupar.SelectedIndex != 1) return null; // "Todos"
            return _idLocalResumen;
        }
    }
}

public class LocalItem
{
    public int    Id     { get; set; }
    public string Nombre { get; set; } = "";
}

// Reemplaza el MessageBox genérico de "Sin resultados" en Arqueo de Caja — repite los
// criterios exactos que se buscaron (tipo, local, rango de fechas) para que el usuario pueda
// confirmar de un vistazo si fue un filtro mal puesto, sin tener que recordarlos de memoria.
public class SinResultadosArqueoModal : Window
{
    private static System.Windows.Media.SolidColorBrush RB(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    public SinResultadosArqueoModal(string tipo, string local, string desde, string hasta)
    {
        Title = "Sin resultados";
        Width = 480; Height = 420;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = RB("#F5F5F5");
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");

        var root = new DockPanel();

        var hdr = new Border { Background = RB("#0E2F44"), Padding = new Thickness(18,12,18,12) };
        hdr.Child = new TextBlock { Text = "SIN RESULTADOS",
            Foreground = System.Windows.Media.Brushes.White, FontSize = 16, FontWeight = FontWeights.Bold };
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        var pie = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = RB("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0),
            Padding = new Thickness(16,10,16,10) };
        var btnCerrar = new Button { Content = "✖  Cerrar", Height = 36,
            Padding = new Thickness(20,0,20,0), Background = RB("#546E7A"),
            Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.SemiBold,
            FontSize = 13, BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Right };
        btnCerrar.Click += (_, _) => Close();
        pie.Child = btnCerrar;
        DockPanel.SetDock(pie, Dock.Bottom); root.Children.Add(pie);

        var body = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = RB("#BDBDBD"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4), Margin = new Thickness(20,16,20,16),
            Padding = new Thickness(16,14,16,14) };
        var sp = new StackPanel();

        // Grid con columna Auto (ícono) + Star (texto), no StackPanel horizontal — un
        // StackPanel horizontal mide a sus hijos con ancho infinito disponible, así que
        // TextWrapping.Wrap por sí solo (o incluso con MaxWidth aproximado) no alcanzaba: el
        // texto largo quedaba cortado por el borde del diálogo en vez de bajar de línea.
        var topGrid = new Grid { Margin = new Thickness(0,0,0,14) };
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var icono = new TextBlock { Text = "ℹ", FontSize = 22,
            Foreground = RB("#F9A825"), Margin = new Thickness(0,0,10,0),
            VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(icono, 0); topGrid.Children.Add(icono);
        var msgSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        msgSp.Children.Add(new TextBlock {
            Text = "No se encontró ningún movimiento de caja con estos criterios.",
            FontSize = 13, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap });
        msgSp.Children.Add(new TextBlock {
            Text = "Revisá el tipo, el local o el rango de fechas — puede que la caja no haya tenido actividad en ese período.",
            FontSize = 11, Foreground = RB("#757575"), Margin = new Thickness(0,2,0,0), TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(msgSp, 1); topGrid.Children.Add(msgSp);
        sp.Children.Add(topGrid);

        sp.Children.Add(new Border { BorderBrush = RB("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0),
            Margin = new Thickness(0,0,0,12) });

        void DI(string lbl, string val)
        {
            var s = new StackPanel { Margin = new Thickness(0,0,0,8) };
            s.Children.Add(new TextBlock { Text = lbl, FontSize = 10,
                Foreground = RB("#757575"), FontWeight = FontWeights.SemiBold });
            s.Children.Add(new TextBlock { Text = val, FontSize = 12, FontWeight = FontWeights.Bold });
            sp.Children.Add(s);
        }
        DI("TIPO BUSCADO", tipo);
        DI("LOCAL", local);
        DI("PERÍODO", $"{desde} al {hasta}");

        // ScrollViewer de respaldo — con Height fijo en la ventana, si algún dato (ej. nombre
        // de local muy largo) empuja el contenido más alto de lo previsto, se desplaza dentro
        // del cuerpo en vez de recortarse en silencio contra el borde inferior.
        var bodyScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = sp };
        body.Child = bodyScroll;
        root.Children.Add(body);
        Content = root;
    }
}

public class ArqueoLocalSelectorModal : Window
{
    private readonly IDbConnectionFactory _db;
    public int?   LocalId     { get; private set; }
    public string LocalNombre { get; private set; } = "";

    private DataGrid _grid    = null!;
    private TextBox  _txtBusc = null!;
    private List<LocalItem> _todos = new();

    private static System.Windows.Media.SolidColorBrush SB(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    public ArqueoLocalSelectorModal(IDbConnectionFactory db)
    {
        _db = db;
        Title  = "Seleccionar local";
        Width  = 420; Height = 440;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        FontSize   = 13;
        BuildUI();
        Loaded += async (_, _) => await CargarLocales();
    }

    private void BuildUI()
    {
        var AzulOscuro = SB("#0E2F44");
        var AzulBase   = SB("#1A4F6E");
        var AzulClaro  = SB("#1F6089");
        var AzulMedio  = SB("#154360");
        var AzulMuted  = SB("#7FB3D3");
        var Blanco     = System.Windows.Media.Brushes.White;

        Background = SB("#F0F2F5");

        var dp = new DockPanel();

        // ── Header ────────────────────────────────────────────────────────
        var hdr = new Border { Background = AzulOscuro, Padding = new Thickness(18, 13, 18, 13) };
        var hdrStack = new StackPanel();
        hdrStack.Children.Add(new TextBlock {
            Text = "SELECCIONAR LOCAL",
            Foreground = Blanco, FontWeight = FontWeights.Bold, FontSize = 14 });
        hdrStack.Children.Add(new TextBlock {
            Text = "Doble clic o Enter para confirmar",
            Foreground = AzulMuted, FontSize = 10.5, Margin = new Thickness(0, 2, 0, 0) });
        hdr.Child = hdrStack;
        DockPanel.SetDock(hdr, Dock.Top); dp.Children.Add(hdr);

        // ── Buscador ──────────────────────────────────────────────────────
        var busqBar = new Border { Background = AzulBase, Padding = new Thickness(12, 9, 12, 9) };
        var busqG = new Grid();
        busqG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        busqG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _txtBusc = new TextBox {
            Padding = new Thickness(10, 7, 10, 7), FontSize = 12.5,
            Background = AzulClaro, Foreground = Blanco, CaretBrush = Blanco,
            BorderBrush = SB("#4A7FA5"), BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center };
        _txtBusc.TextChanged += OnBuscar;
        var btnBuscar = new Button {
            Content = "🔍", Width = 38, Height = 34, Margin = new Thickness(6, 0, 0, 0),
            Background = AzulMedio, Foreground = Blanco,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(btnBuscar, 1);
        busqG.Children.Add(_txtBusc); busqG.Children.Add(btnBuscar);
        busqBar.Child = busqG;
        DockPanel.SetDock(busqBar, Dock.Top); dp.Children.Add(busqBar);

        // ── Footer ────────────────────────────────────────────────
        var pie = new Border { Background = AzulMedio,
            BorderBrush = AzulOscuro, BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(14, 10, 14, 10) };
        var pieSp = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right };
        var btnSel = new Button {
            Content = "✔  Seleccionar", Padding = new Thickness(18, 8, 18, 8),
            Background = AzulClaro, Foreground = Blanco,
            FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 8, 0) };
        btnSel.Click += (_, _) => Confirmar();
        var btnCan = new Button {
            Content = "✕  Cancelar", Padding = new Thickness(14, 8, 14, 8),
            Background = SB("#37474F"), Foreground = Blanco,
            FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand };
        btnCan.Click += (_, _) => Close();
        pieSp.Children.Add(btnSel); pieSp.Children.Add(btnCan);
        pie.Child = pieSp;
        DockPanel.SetDock(pie, Dock.Bottom); dp.Children.Add(pie);

        // ── Grid locales ────────────────────────────────────────────────
        var colHdr = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        colHdr.Setters.Add(new Setter(Control.BackgroundProperty, AzulBase));
        colHdr.Setters.Add(new Setter(Control.ForegroundProperty, Blanco));
        colHdr.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        colHdr.Setters.Add(new Setter(Control.FontSizeProperty, 11.5));
        colHdr.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 8, 12, 8)));
        colHdr.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 0)));
        colHdr.Setters.Add(new Setter(Control.BorderBrushProperty, SB("#155980")));

        _grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true,
            RowHeight = 36, FontSize = 12.5, BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.White,
            AlternatingRowBackground = SB("#F4F8FA"),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = SB("#E0E8EE"),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            ColumnHeaderStyle = colHdr,
            SelectionMode = DataGridSelectionMode.Single,
            CanUserAddRows = false, CanUserResizeRows = false,
            Margin = new Thickness(0) };
        _grid.Columns.Add(new DataGridTextColumn { Header = "ID",
            Binding = new System.Windows.Data.Binding("Id"),
            Width = new DataGridLength(55, DataGridLengthUnitType.Pixel) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Local",
            Binding = new System.Windows.Data.Binding("Nombre"),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.MouseDoubleClick += (_, _) => Confirmar();
        _grid.PreviewKeyDown   += (_, e) => { if (e.Key == Key.Enter) { Confirmar(); e.Handled = true; } };
        dp.Children.Add(_grid);

        Content = dp;
        Loaded += (_, _) => { _txtBusc.Focus(); if (_grid.Items.Count > 0) _grid.SelectedIndex = 0; };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    private async Task CargarLocales()
    {
        try
        {
            using var conn = _db.Create();
            _todos = (await conn.QueryAsync<LocalItem>(
                "SELECT ID_LOCAL AS Id, NOMBRE AS Nombre FROM LOCALES ORDER BY ID_LOCAL")).ToList();
            _grid.ItemsSource = _todos;
            if (_grid.Items.Count > 0) _grid.SelectedIndex = 0;
        }
        catch { /* sin locales */ }
    }

    private void OnBuscar(object s, TextChangedEventArgs e)
    {
        var t = _txtBusc.Text.Trim().ToUpperInvariant();
        _grid.ItemsSource = string.IsNullOrEmpty(t)
            ? _todos
            : _todos.Where(l => l.Nombre.ToUpperInvariant().Contains(t) || l.Id.ToString().Contains(t)).ToList();
        if (_grid.Items.Count > 0) _grid.SelectedIndex = 0;
    }

    private void Confirmar()
    {
        if (_grid.SelectedItem is LocalItem item)
        {
            LocalId      = item.Id;
            LocalNombre  = item.Nombre;
            DialogResult = true;
            Close();
        }
    }
}

internal class FilaCajaRaw
{
    public int      IDCAJA       { get; set; }
    public int      IDCABCAJA    { get; set; }
    public int      ID_LOCAL     { get; set; }
    public string   LOCAL_NOMBRE { get; set; } = "";
    public string   USUARIO      { get; set; } = "";
    public int      CAJA         { get; set; }
    public string   ACCION       { get; set; } = "";
    public string   CONCEPTO     { get; set; } = "";
    public decimal  MONTO        { get; set; }
    public string   METODO       { get; set; } = "";
    public string   NUMERO       { get; set; } = "";
    public int      RECIBIDOR    { get; set; }
    public DateTime FECHA        { get; set; }
    public string?  OBSERVACION  { get; set; }
    public int      EJECUTOR     { get; set; }
}

internal class FilaCaja
{
    public int      IdLocal  { get; set; }
    public string   Local    { get; set; } = "";
    public string   Usuario  { get; set; } = "";
    public string   Concepto { get; set; } = "";
    public string   Metodo   { get; set; } = "";
    public decimal  Monto    { get; set; }
    public DateTime Fecha    { get; set; }
    public string   FechaStr { get; set; } = "";
    public string   Obs      { get; set; } = "";
    // "Entrada" / "Salida" — ya venía calculado en FilaCajaRaw.ACCION (CASE D.TIPO), pero se
    // perdía acá al no propagarse; hacía falta para poder mostrar Ingreso/Egreso en el reporte
    // detallado (antes esa columna quedaba duplicando "Monto Gs." por error de copiado).
    public string   Accion   { get; set; } = "";
}

internal class FilaCajaAgrup
{
    public string  Local    { get; set; } = "";
    public string  Usuario  { get; set; } = "";
    public string  Concepto { get; set; } = "";
    public string  Metodo   { get; set; } = "";
    public decimal Monto    { get; set; }
    public string  FechaStr { get; set; } = "";
    public string  Obs      { get; set; } = "";
}

// ── Registrar movimiento de caja (delega a CajaExploradorWindow) ─────────────

public class CajaRegistrarWindow : CajaExploradorWindow { }

// ── Historial de Caja ─────────────────────────────────────────────────────────

public class CajaHistorialWindow : Window
{
    private readonly IDbConnectionFactory _db;
    private readonly ISessionService     _session;

    private List<LocalItem> _locales = new();

    private static System.Windows.Media.SolidColorBrush HB(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    public CajaHistorialWindow()
    {
        _db      = App.Services.GetRequiredService<IDbConnectionFactory>();
        _session = SessionService.Instance;
        Title  = "Historial de Caja";
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = HB("#F0F2F5");
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        FontSize   = 13;
        _ = MostrarFiltroInicialAsync();
    }

    private Border  _tblResultadoArqueo = null!;
    private TextBlock _lblSinResultadosArqueo = null!;

    // ── Pantalla única "Arqueo de caja" (réplica del sistema viejo): fondo lila,
    // Desde/Hasta/Local/Buscar arriba, y la tabla de totales (SALDO INICIAL/VENTAS/COBROS/
    // TOTAL INGRESOS/COMPRAS/GASTOS/PAGOS/ANTICIPOS/TOTAL EGRESOS/TOTAL NETO) aparece DEBAJO
    // del botón, en la misma ventana — no navega a otra pantalla.
    private async Task MostrarFiltroInicialAsync()
    {
        Width = 700; Height = 640; MinWidth = 640; MinHeight = 520;
        ResizeMode = ResizeMode.CanResize;

        using var conn = _db.Create();
        _locales = (await conn.QueryAsync<LocalItem>(
            "SELECT ID_LOCAL AS Id, NOMBRE AS Nombre FROM LOCALES ORDER BY NOMBRE")).ToList();
        var esAdmin = _session.UsuarioActual?.PuedeVerTodosLosLocales == true;

        var root = new Grid { Background = HB("#C9C3F0") };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var hdr = new Border { Background = HB("#B3ABE8"), Padding = new Thickness(20,16,20,16) };
        hdr.Child = new TextBlock { Text = "ARQUEO DE CAJA", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = HB("#14141E") };
        Grid.SetRow(hdr, 0); root.Children.Add(hdr);

        var body = new StackPanel { Margin = new Thickness(24,20,24,10) };

        UIElement Campo(string lbl, UIElement ctrl) {
            var sp = new StackPanel { Margin = new Thickness(0,0,0,14) };
            sp.Children.Add(new TextBlock { Text = lbl, FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = HB("#14141E"), Margin = new Thickness(0,0,0,4) });
            sp.Children.Add(ctrl); return sp;
        }

        var fechasGrid = new Grid();
        fechasGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fechasGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fechasGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        fechasGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fechasGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var lblDesde = new TextBlock { Text = "Desde", FontSize = 11, Foreground = HB("#14141E"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0) };
        var dpDesde  = new DatePicker { SelectedDate = DateTime.Today, Background = System.Windows.Media.Brushes.White };
        var lblHasta = new TextBlock { Text = "Hasta", FontSize = 11, Foreground = HB("#14141E"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0) };
        var dpHasta  = new DatePicker { SelectedDate = DateTime.Today, Background = System.Windows.Media.Brushes.White };
        Grid.SetColumn(lblDesde, 0); fechasGrid.Children.Add(lblDesde);
        Grid.SetColumn(dpDesde, 1);  fechasGrid.Children.Add(dpDesde);
        Grid.SetColumn(lblHasta, 3); fechasGrid.Children.Add(lblHasta);
        Grid.SetColumn(dpHasta, 4);  fechasGrid.Children.Add(dpHasta);
        body.Children.Add(fechasGrid);
        body.Children.Add(new Border { Height = 14 });

        var cboLocal = new ComboBox { Padding = new Thickness(6,5,6,5), Background = System.Windows.Media.Brushes.White };
        if (esAdmin)
        {
            cboLocal.Items.Add(new ComboBoxItem { Content = "Todos los locales", Tag = 0 });
            foreach (var l in _locales) cboLocal.Items.Add(new ComboBoxItem { Content = l.Nombre, Tag = l.Id });
            cboLocal.SelectedIndex = 0;
        }
        else
        {
            var idLocalSesion = _session.LocalActual?.IdLocal ?? 0;
            var localSesion = _locales.FirstOrDefault(l => l.Id == idLocalSesion);
            cboLocal.Items.Add(new ComboBoxItem { Content = localSesion?.Nombre ?? _session.LocalActual?.NombreLocal ?? "Mi local", Tag = idLocalSesion });
            cboLocal.SelectedIndex = 0;
            cboLocal.IsEnabled = false;
        }
        body.Children.Add(Campo("Locales", cboLocal));

        var btnBuscar = new Border {
            Background = HB("#7C6FD4"), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14,12,14,12), Margin = new Thickness(0,10,0,0), Cursor = Cursors.Hand,
            Child = new TextBlock { Text = "🔍  Buscar", Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.SemiBold, FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center }
        };

        // Búsqueda automática: se dispara sola al cambiar Desde/Hasta/Local, sin necesidad
        // de apretar el botón — pedido explícito (2026-07-30). El botón sigue funcionando
        // igual, por si el usuario prefiere tocarlo (ej. tras escribir en varios campos
        // seguidos sin esperar el disparo de cada uno).
        async Task EjecutarBusqueda()
        {
            var desde   = dpDesde.SelectedDate ?? DateTime.Today;
            var hasta   = dpHasta.SelectedDate ?? DateTime.Today;
            var idLocal = (cboLocal.SelectedItem as ComboBoxItem)?.Tag as int?;
            await BuscarYMostrarTablaAsync(desde, hasta, idLocal);
        }
        btnBuscar.MouseLeftButtonUp += async (_, _) => await EjecutarBusqueda();
        dpDesde.SelectedDateChanged += async (_, _) => await EjecutarBusqueda();
        dpHasta.SelectedDateChanged += async (_, _) => await EjecutarBusqueda();
        cboLocal.SelectionChanged   += async (_, _) => await EjecutarBusqueda();

        body.Children.Add(btnBuscar);

        Grid.SetRow(body, 1); root.Children.Add(body);

        // Tabla de resultado — vacía/oculta hasta la primera búsqueda.
        _tblResultadoArqueo = new Border { Margin = new Thickness(24,4,24,20), Visibility = Visibility.Collapsed };
        _lblSinResultadosArqueo = new TextBlock {
            Text = "No se encontraron movimientos con los criterios indicados.", FontSize = 12,
            Foreground = HB("#14141E"), Margin = new Thickness(24,10,24,20),
            HorizontalAlignment = HorizontalAlignment.Center, Visibility = Visibility.Collapsed };
        var resScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        var resStack = new StackPanel();
        resStack.Children.Add(_tblResultadoArqueo);
        resStack.Children.Add(_lblSinResultadosArqueo);
        resScroll.Content = resStack;
        Grid.SetRow(resScroll, 2); root.Children.Add(resScroll);

        Content = root;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    // Categorías reales de CAJA_DETALLE.SUBTIPO (confirmadas contra la base 2026-07-30):
    // Ingresos: VENTA / VENTA CREDITO (ventas), COBRO_SISTEMA (cobros de cuotas), COBRO
    // (cobros manuales/otros), APERTURA (saldo inicial, tratado aparte). Egresos: COMPRA,
    // GASTOS, PAGO, ANTICIPO.
    private async Task BuscarYMostrarTablaAsync(DateTime desde, DateTime hasta, int? idLocal)
    {
        try
        {
            using var conn = _db.Create();
            var movs = (await conn.QueryAsync<(string Tipo, string Subtipo, decimal Monto, string FormaPago)>(@"
                SELECT D.TIPO AS Tipo, ISNULL(D.SUBTIPO,'') AS Subtipo, D.MONTO AS Monto,
                       ISNULL(D.FORMA_PAGO,'') AS FormaPago
                FROM CAJA_DETALLE D
                WHERE D.ESTADO_REG = 'V' AND D.FECHA_HORA >= @desde AND D.FECHA_HORA < @hasta
                  AND (@idLocal IS NULL OR D.ID_LOCAL = @idLocal)",
                new { desde, hasta = hasta.Date.AddDays(1), idLocal })).ToList();

            if (movs.Count == 0)
            {
                _tblResultadoArqueo.Visibility = Visibility.Collapsed;
                _lblSinResultadosArqueo.Visibility = Visibility.Visible;
                return;
            }

            decimal Suma(Func<(string Tipo, string Subtipo, decimal Monto, string FormaPago), bool> pred, string? fp = null) =>
                movs.Where(m => pred(m) && (fp == null || m.FormaPago.ToUpper() == fp)).Sum(m => m.Monto);

            (decimal ef, decimal tar, decimal tra, decimal che, decimal otr, decimal tot) Fila(Func<(string,string,decimal,string), bool> pred)
            {
                var ef  = movs.Where(m => pred((m.Tipo,m.Subtipo,m.Monto,m.FormaPago)) && m.FormaPago.ToUpper()=="EFECTIVO").Sum(m=>m.Monto);
                var tar = movs.Where(m => pred((m.Tipo,m.Subtipo,m.Monto,m.FormaPago)) && m.FormaPago.ToUpper()=="TARJETA").Sum(m=>m.Monto);
                var tra = movs.Where(m => pred((m.Tipo,m.Subtipo,m.Monto,m.FormaPago)) && m.FormaPago.ToUpper()=="TRANSFERENCIA").Sum(m=>m.Monto);
                var che = movs.Where(m => pred((m.Tipo,m.Subtipo,m.Monto,m.FormaPago)) && m.FormaPago.ToUpper()=="CHEQUE").Sum(m=>m.Monto);
                var otr = movs.Where(m => pred((m.Tipo,m.Subtipo,m.Monto,m.FormaPago)) &&
                    !new[]{"EFECTIVO","TARJETA","TRANSFERENCIA","CHEQUE"}.Contains(m.FormaPago.ToUpper())).Sum(m=>m.Monto);
                var tot = ef+tar+tra+che+otr;
                return (ef,tar,tra,che,otr,tot);
            }

            var saldoIni  = Fila(m => m.Item1=="I" && m.Item2=="APERTURA");
            var ventas    = Fila(m => m.Item1=="I" && (m.Item2=="VENTA" || m.Item2=="VENTA CREDITO"));
            var cobrosSis = Fila(m => m.Item1=="I" && m.Item2=="COBRO_SISTEMA");
            var cobros    = Fila(m => m.Item1=="I" && m.Item2=="COBRO");
            var compras   = Fila(m => m.Item1=="E" && m.Item2=="COMPRA");
            var gastos    = Fila(m => m.Item1=="E" && m.Item2=="GASTOS");
            var pagos     = Fila(m => m.Item1=="E" && m.Item2=="PAGO");
            var anticipos = Fila(m => m.Item1=="E" && m.Item2=="ANTICIPO");

            decimal TotIngresos(Func<(decimal ef,decimal tar,decimal tra,decimal che,decimal otr,decimal tot),decimal> sel) =>
                sel(saldoIni)+sel(ventas)+sel(cobrosSis)+sel(cobros);
            decimal TotEgresos(Func<(decimal ef,decimal tar,decimal tra,decimal che,decimal otr,decimal tot),decimal> sel) =>
                sel(compras)+sel(gastos)+sel(pagos)+sel(anticipos);

            var totIngresos = (TotIngresos(f=>f.ef), TotIngresos(f=>f.tar), TotIngresos(f=>f.tra), TotIngresos(f=>f.che), TotIngresos(f=>f.otr), TotIngresos(f=>f.tot));
            var totEgresos  = (TotEgresos(f=>f.ef),  TotEgresos(f=>f.tar),  TotEgresos(f=>f.tra),  TotEgresos(f=>f.che),  TotEgresos(f=>f.otr),  TotEgresos(f=>f.tot));
            var totNeto     = (totIngresos.Item1-totEgresos.Item1, totIngresos.Item2-totEgresos.Item2, totIngresos.Item3-totEgresos.Item3,
                                totIngresos.Item4-totEgresos.Item4, totIngresos.Item5-totEgresos.Item5, totIngresos.Item6-totEgresos.Item6);

            var tblG = new Grid();
            var colWidths = new[] { 1.5, 1.0, 1.0, 1.1, 1.0, 1.0, 1.1 };
            foreach (var w in colWidths) tblG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w, GridUnitType.Star) });
            var thdrs = new[] { "CONCEPTO","EFECTIVO","TARJETA","TRANSFERENCIA","CHEQUE","OTROS","TOTALES" };
            int trow = 0;
            tblG.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int c = 0; c < thdrs.Length; c++) {
                var cell = new Border { Background = HB("#7C6FD4"), Padding = new Thickness(6,5,6,5) };
                cell.Child = new TextBlock { Text = thdrs[c], Foreground = System.Windows.Media.Brushes.White,
                    FontSize = 10.5, FontWeight = FontWeights.Bold,
                    TextAlignment = c==0 ? TextAlignment.Left : TextAlignment.Right };
                Grid.SetRow(cell,trow); Grid.SetColumn(cell,c); tblG.Children.Add(cell);
            }
            void TRow(string lbl, (decimal ef,decimal tar,decimal tra,decimal che,decimal otr,decimal tot) f, string? bg = null, bool bold = false) {
                tblG.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); trow++;
                var vals = new[] { lbl, f.ef.ToString("N0"), f.tar.ToString("N0"), f.tra.ToString("N0"), f.che.ToString("N0"), f.otr.ToString("N0"), f.tot.ToString("N0") };
                var rowBg = bg != null ? HB(bg) : System.Windows.Media.Brushes.White;
                for (int c = 0; c < vals.Length; c++) {
                    var cell = new Border { Background = rowBg, Padding = new Thickness(6,4,6,4),
                        BorderBrush = HB("#C9C3F0"), BorderThickness = new Thickness(0,0,0,1) };
                    cell.Child = new TextBlock { Text = vals[c], FontSize = 11,
                        TextAlignment = c==0 ? TextAlignment.Left : TextAlignment.Right,
                        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, Foreground = HB("#14141E") };
                    Grid.SetRow(cell,trow); Grid.SetColumn(cell,c); tblG.Children.Add(cell);
                }
            }
            void Separador() {
                tblG.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); trow++;
                var cell = new Border { Background = HB("#E0E0E0"), Height = 6 };
                Grid.SetRow(cell,trow); Grid.SetColumn(cell,0); Grid.SetColumnSpan(cell,7); tblG.Children.Add(cell);
            }

            TRow("SALDO INICIAL (+)", saldoIni);
            TRow("VENTAS (+)", ventas);
            TRow("COBROS SIST. (+)", cobrosSis);
            TRow("COBROS (+)", cobros);
            TRow("TOTAL INGRESOS (++)", totIngresos, bg: "#BBDEFB", bold: true);
            Separador();
            TRow("COMPRAS (-)", compras);
            TRow("GASTOS (-)", gastos);
            TRow("PAGOS (-)", pagos);
            TRow("ANTICIPOS (-)", anticipos);
            TRow("TOTAL EGRESOS (--)", totEgresos, bg: "#BBDEFB", bold: true);
            Separador();
            TRow("TOTAL NETO EN CAJA", totNeto, bg: "#C8E6C9", bold: true);

            _tblResultadoArqueo.Child = tblG;
            _tblResultadoArqueo.Visibility = Visibility.Visible;
            _lblSinResultadosArqueo.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al buscar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

}

// ── Detalle de un concepto del resumen (Ventas, Cobros del sistema, Gastos, etc.) —
// pedido explícito: el resumen solo mostraba el total agregado por forma de pago, sin
// forma de saber qué movimientos concretos (de qué caja/local/cajero) lo componen. Puede
// abarcar varias cajas y locales a la vez (el resumen es de TODO el período/local
// filtrado), a diferencia de CajaHistorialDetalleModal que es de una sola caja puntual.
internal class CajaResumenDetalleModal : Window
{
    private static System.Windows.Media.SolidColorBrush RB(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    public CajaResumenDetalleModal(string concepto, List<FilaHistCajaRaw> movimientos)
    {
        Title = $"Detalle de \"{concepto}\"";
        Width = 950; Height = 600;
        MinWidth = 700; MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = RB("#F4F6F8");
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");

        var root = new DockPanel();

        var fmt = System.Globalization.CultureInfo.GetCultureInfo("es-PY");
        var total = movimientos.Sum(m => m.MONTO);

        var hdr = new Border { Background = RB("#0E2F44"), Padding = new Thickness(20,14,20,14) };
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock { Text = concepto.ToUpper(),
            FontSize = 15, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White });
        hdrSp.Children.Add(new TextBlock { Text = $"{movimientos.Count} movimiento(s) — Total: Gs. {total.ToString("N0", fmt)}",
            FontSize = 11, Foreground = RB("#90A4AE"), Margin = new Thickness(0,2,0,0) });
        hdr.Child = hdrSp;
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        var pie = new Border { Padding = new Thickness(16,10,16,10), Background = System.Windows.Media.Brushes.White,
            BorderBrush = RB("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0) };
        var btnCerrar = new Button { Content = "✕ Cerrar", Height = 34, Padding = new Thickness(18,0,18,0),
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = RB("#546E7A"), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        btnCerrar.Click += (_, _) => Close();
        pie.Child = btnCerrar;
        DockPanel.SetDock(pie, Dock.Bottom); root.Children.Add(pie);

        var grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = RB("#EEEEEE"),
            Background = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(1), BorderBrush = RB("#D0D7DE"),
            RowHeight = 32, FontSize = 11.5, Margin = new Thickness(16),
            ColumnHeaderStyle = MkHdrStyleResumen()
        };
        Style TxR() { var s = new Style(typeof(TextBlock)); s.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right)); return s; }
        grid.Columns.Add(new DataGridTextColumn { Header = "Fecha/Hora", Binding = new System.Windows.Data.Binding("FechaHora"), Width = new DataGridLength(1.0, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Local",      Binding = new System.Windows.Data.Binding("LocalNombre"), Width = new DataGridLength(1.0, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Cajero",     Binding = new System.Windows.Data.Binding("Cajero"), Width = new DataGridLength(1.0, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Forma de Pago", Binding = new System.Windows.Data.Binding("FORMA_PAGO"), Width = new DataGridLength(0.9, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Monto",      Binding = new System.Windows.Data.Binding("MONTO") { StringFormat = "N0" }, Width = new DataGridLength(0.8, DataGridLengthUnitType.Star), ElementStyle = TxR() });
        grid.Columns.Add(new DataGridTextColumn { Header = "Concepto",   Binding = new System.Windows.Data.Binding("CONCEPTO"), Width = new DataGridLength(2.2, DataGridLengthUnitType.Star) });

        grid.ItemsSource = movimientos.OrderBy(m => m.FechaHora).ToList();
        root.Children.Add(grid);

        Content = root;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    private static Style MkHdrStyleResumen()
    {
        var s = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        s.Setters.Add(new Setter(Control.BackgroundProperty, RB("#37474F")));
        s.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
        s.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        s.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8,6,8,6)));
        s.Setters.Add(new Setter(Control.FontSizeProperty, 11.0));
        return s;
    }
}

// ── Detalle de una caja del Historial (explica por qué Neto sistema y Dinero en caja
// física no coinciden) — pedido explícito tras varios casos reales donde la diferencia
// se debía a movimientos por transferencia/tarjeta (ese dinero nunca puede aparecer en el
// conteo de billetes) o a un egreso mal clasificado como EFECTIVO cuando en realidad se
// pagó en otra moneda/forma. "Neto sistema" sigue sumando TODOS los medios de pago (mismo
// número que ya conocían cajeros/encargadas — no se cambió ese cálculo pese a que un
// análisis con datos de junio mostró que "solo efectivo" acierta más seguido contra lo
// contado, para no generar confusión con un número al que ya están acostumbrados). Este
// modal agrega, como referencia aparte, el neto contando solo efectivo — y cada movimiento
// se lista con su FORMA_PAGO, resaltando los que NO son efectivo.
internal class CajaHistorialDetalleModal : Window
{
    private static System.Windows.Media.SolidColorBrush DB(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    public CajaHistorialDetalleModal(int idMaster, string localNombre, string cajero,
        List<FilaHistCajaRaw> movimientos, decimal ingresosEf, decimal egresosEf, decimal netoSistema,
        decimal contado, bool sinCargar, bool esDesfinanciadaPorDiseno)
    {
        Title = $"Detalle de Caja N° {idMaster} — {localNombre}";
        Width = 900; Height = 640;
        MinWidth = 700; MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = DB("#F4F6F8");
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");

        var root = new DockPanel();

        // Header
        var hdr = new Border { Background = DB("#0E2F44"), Padding = new Thickness(20,14,20,14) };
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock { Text = $"CAJA N° {idMaster} — {localNombre}",
            FontSize = 15, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White });
        hdrSp.Children.Add(new TextBlock { Text = $"Cajero: {cajero}",
            FontSize = 11, Foreground = DB("#90A4AE"), Margin = new Thickness(0,2,0,0) });
        hdr.Child = hdrSp;
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        // Pie
        var pie = new Border { Padding = new Thickness(16,10,16,10), Background = System.Windows.Media.Brushes.White,
            BorderBrush = DB("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0) };
        var btnCerrar = new Button { Content = "✕ Cerrar", Height = 34, Padding = new Thickness(18,0,18,0),
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = DB("#546E7A"), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        btnCerrar.Click += (_, _) => Close();
        pie.Child = btnCerrar;
        DockPanel.SetDock(pie, Dock.Bottom); root.Children.Add(pie);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var body = new StackPanel { Margin = new Thickness(16) };

        // ── Resumen del cálculo ──────────────────────────────────────────
        var resumen = new Border { Background = System.Windows.Media.Brushes.White,
            BorderBrush = DB("#D0D7DE"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16,12,16,12), Margin = new Thickness(0,0,0,12) };
        var resumenSp = new StackPanel();
        resumenSp.Children.Add(new TextBlock { Text = "CÓMO SE CALCULA \"NETO SISTEMA\"", FontSize = 11,
            FontWeight = FontWeights.Bold, Foreground = DB("#37474F"), Margin = new Thickness(0,0,0,6) });
        resumenSp.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 11.5,
            Foreground = DB("#546E7A"),
            Text = "\"Neto sistema\" (el número oficial, arriba en la tabla) suma TODOS los medios de pago — " +
                   "efectivo, transferencia, tarjeta. Como referencia, abajo también se muestra el neto contando " +
                   "SOLO efectivo: si difieren bastante, suele ser porque hubo transferencias/tarjetas ese día " +
                   "(resaltadas en amarillo abajo), que nunca pueden aparecer en el conteo de billetes físicos — " +
                   "no es necesariamente un error, es información para entender la diferencia." });

        void FilaResumen(string lbl, string val, bool bold = false, string? color = null) {
            var r = new Grid { Margin = new Thickness(0,6,0,0) };
            r.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            r.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var l = new TextBlock { Text = lbl, FontSize = 12, Foreground = DB("#37474F") };
            var v = new TextBlock { Text = val, FontSize = 12, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                Foreground = color != null ? DB(color) : DB("#212529") };
            Grid.SetColumn(l, 0); Grid.SetColumn(v, 1);
            r.Children.Add(l); r.Children.Add(v);
            resumenSp.Children.Add(r);
        }
        var fmt = System.Globalization.CultureInfo.GetCultureInfo("es-PY");
        FilaResumen("Neto sistema (todos los medios de pago):", $"Gs. {netoSistema.ToString("N0", fmt)}", bold: true,
            color: netoSistema >= 0 ? "#1B5E20" : "#B71C1C");
        FilaResumen("Dinero contado en caja física:", sinCargar ? "— sin cargar —" : $"Gs. {contado.ToString("N0", fmt)}", bold: true);
        if (!sinCargar)
        {
            if (esDesfinanciadaPorDiseno)
                FilaResumen("Estado:", $"Desfinanciada (Gs. {Math.Abs(netoSistema).ToString("N0", fmt)}) — no es faltante real, ver egresos abajo",
                    bold: true, color: "#E65100");
            else
            {
                var dif = contado - netoSistema;
                FilaResumen("Diferencia:", $"Gs. {dif.ToString("N0", fmt)}", bold: true,
                    color: dif == 0 ? "#1B5E20" : "#B71C1C");
            }
        }
        resumenSp.Children.Add(new Border { Height = 1, Background = DB("#E0E0E0"), Margin = new Thickness(0,10,0,10) });
        resumenSp.Children.Add(new TextBlock { Text = "Referencia — solo movimientos en efectivo:", FontSize = 10.5,
            FontWeight = FontWeights.SemiBold, Foreground = DB("#78909C") });
        FilaResumen("Ingresos en efectivo:", $"Gs. {ingresosEf.ToString("N0", fmt)}");
        FilaResumen("Egresos en efectivo:",  $"Gs. {egresosEf.ToString("N0", fmt)}");
        FilaResumen("= Neto solo efectivo:", $"Gs. {(ingresosEf - egresosEf).ToString("N0", fmt)}");
        resumen.Child = resumenSp;
        body.Children.Add(resumen);

        // ── Grilla de movimientos ────────────────────────────────────────
        body.Children.Add(new TextBlock { Text = "MOVIMIENTOS DE ESTA CAJA", FontSize = 11,
            FontWeight = FontWeights.Bold, Foreground = DB("#37474F"), Margin = new Thickness(2,0,0,6) });

        var grid = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = DB("#EEEEEE"),
            Background = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(1), BorderBrush = DB("#D0D7DE"),
            RowHeight = 32, FontSize = 11.5, MaxHeight = 360,
            ColumnHeaderStyle = MkHdrStyleDetalle()
        };
        Style TxR() { var s = new Style(typeof(TextBlock)); s.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right)); return s; }
        grid.Columns.Add(new DataGridTextColumn { Header = "Hora",     Binding = new System.Windows.Data.Binding("FechaHora"), Width = new DataGridLength(0.9, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Tipo",     Binding = new System.Windows.Data.Binding("TipoDesc"),  Width = new DataGridLength(0.6, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "SubTipo",  Binding = new System.Windows.Data.Binding("SUBTIPO"),   Width = new DataGridLength(0.8, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Forma de Pago", Binding = new System.Windows.Data.Binding("FORMA_PAGO"), Width = new DataGridLength(0.9, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Monto",    Binding = new System.Windows.Data.Binding("MONTO") { StringFormat = "N0" }, Width = new DataGridLength(0.8, DataGridLengthUnitType.Star), ElementStyle = TxR() });
        grid.Columns.Add(new DataGridTextColumn { Header = "Concepto", Binding = new System.Windows.Data.Binding("CONCEPTO"), Width = new DataGridLength(2.0, DataGridLengthUnitType.Star) });

        foreach (var m in movimientos) m.TipoDesc = m.TIPO == "I" ? "INGRESO" : "EGRESO";

        grid.RowStyle = new Style(typeof(DataGridRow));
        grid.LoadingRow += (_, e) => {
            if (e.Row.Item is FilaHistCajaRaw m && m.FORMA_PAGO.ToUpper() != "EFECTIVO")
                e.Row.Background = DB("#FFF9C4"); // resalta lo que NO es efectivo — candidato a explicar la diferencia
        };
        grid.ItemsSource = movimientos.OrderBy(m => m.FechaHora).ToList();
        body.Children.Add(grid);

        scroll.Content = body;
        root.Children.Add(scroll);
        Content = root;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    private static Style MkHdrStyleDetalle()
    {
        var s = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        s.Setters.Add(new Setter(Control.BackgroundProperty, DB("#37474F")));
        s.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.White));
        s.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        s.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8,6,8,6)));
        s.Setters.Add(new Setter(Control.FontSizeProperty, 11.0));
        return s;
    }
}

internal class FilaHistCajaRaw
{
    public long    ID_DETALLE   { get; set; }
    public int     ID_MASTER    { get; set; }
    public string  LocalNombre  { get; set; } = "";
    // "Cajero" = quién operaba físicamente esta caja al momento del movimiento
    // (CAJA_DETALLE.ID_CAJERO). "Cobrador" = a quién se le atribuye la venta/cobro
    // (CAJA_DETALLE.ID_VENDEDOR, ej. "Cobrado por" en CobrosWindow) — pueden ser
    // personas distintas (un vendedor pide a otro cajero que le cobre una cuota), y
    // sin distinguirlos un movimiento quedaba atribuido por completo al cajero, aunque
    // el dinero/comisión realmente correspondiera a otro vendedor.
    public string  Cajero       { get; set; } = "";
    public string  Cobrador     { get; set; } = "";
    public string  TIPO         { get; set; } = "";
    public string  SUBTIPO      { get; set; } = "";
    public string  FORMA_PAGO   { get; set; } = "";
    public decimal MONTO        { get; set; }
    public string  CONCEPTO     { get; set; } = "";
    public string  REFERENCIA   { get; set; } = "";
    public string  ESTADO_REG   { get; set; } = "";
    public string  FechaHora    { get; set; } = "";
    public string  TipoDesc     { get; set; } = "";
}

internal class FilaCierreCajaRaw
{
    public int       ID_MASTER         { get; set; }
    public string    LocalNombre       { get; set; } = "";
    public string    Cajero            { get; set; } = "";
    public string    ESTADO            { get; set; } = "";
    public DateTime  FECHA_APERTURA    { get; set; }
    public DateTime? FECHA_CIERRE      { get; set; }
    public decimal   TOT_INGRESOS      { get; set; }
    public decimal   TOT_EGRESOS       { get; set; }
    public decimal?  MONTO_CIERRE_REAL { get; set; }
}

internal class FilaHistDetalle
{
    public string  Cajero    { get; set; } = "";
    public string  Concepto  { get; set; } = "";
    public decimal Efectivo  { get; set; }
    public decimal Tarjeta   { get; set; }
    public decimal Transf    { get; set; }
    public decimal Cheque    { get; set; }
    public decimal Otros     { get; set; }
    public decimal Total     { get; set; }
    public string  FechaHora { get; set; } = "";
    public bool    EsEgreso  { get; set; }
}

// ── Comprobantes de Depósito Pendientes ──────────────────────────────────────
// El dueño del negocio pidió exigir un comprobante de depósito (N° + foto) por cada cierre de
// caja, SIN bloquear el cierre en sí — se puede cargar en el momento (ver CajaCierreWindow) o
// acá, dentro de las 48hs siguientes al cierre (CajaMaster.DentroDePlazoComprobante). Pasado
// ese plazo, según lo acordado, no pasa nada automático — solo queda visible como "Vencido"
// para que el dueño revise quién no completó a tiempo.
internal class FilaComprobantePendiente
{
    public int      IdMaster        { get; set; }
    public string   LocalNombre     { get; set; } = "";
    public string   NombreCajero    { get; set; } = "";
    public DateTime? FechaCierre    { get; set; }
    public string   FechaCierreStr  { get; set; } = "";
    public string   Estado          { get; set; } = "";  // Pendiente / Cargado / Vencido
    public string   HorasRestantes  { get; set; } = "";
    public bool     PuedeCompletar  { get; set; }
    public string   NroComprobante  { get; set; } = "";
}

public class CajaComprobantesPendientesWindow : Window
{
    private readonly ICajaRepository      _caja;
    private readonly IDbConnectionFactory _db;
    private readonly ISessionService      _session;

    private ComboBox _cboLocal = null!;
    private DataGrid _grid     = null!;
    private List<LocalItem> _locales = new();

    private static System.Windows.Media.SolidColorBrush PB(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    public CajaComprobantesPendientesWindow()
    {
        _caja    = App.Services.GetRequiredService<ICajaRepository>();
        _db      = App.Services.GetRequiredService<IDbConnectionFactory>();
        _session = SessionService.Instance;
        Title  = "Comprobantes de Depósito";
        Width  = 1080; Height = 620;
        MinWidth = 1000; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = PB("#F5F5F5");
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        BuildUI();
        Loaded += async (_, _) => await CargarAsync();
    }

    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // filtros
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // grid
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // pie

        var hdr = new Border { Background = PB("#0E2F44"), Padding = new Thickness(20, 14, 20, 14) };
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock {
            Text = "COMPROBANTES DE DEPÓSITO", FontSize = 15, FontWeight = FontWeights.Bold,
            Foreground = System.Windows.Media.Brushes.White
        });
        hdrSp.Children.Add(new TextBlock {
            Text = "Cierres de los últimos 3 días (pendientes y ya cargados) — 48hs de plazo desde el cierre para completarlo",
            FontSize = 10.5, Foreground = PB("#90A4AE")
        });
        hdr.Child = hdrSp;
        Grid.SetRow(hdr, 0);
        root.Children.Add(hdr);

        var filtros = new Border { Background = System.Windows.Media.Brushes.White,
            Padding = new Thickness(20, 10, 20, 10), BorderBrush = PB("#E0E0E0"), BorderThickness = new Thickness(0,0,0,1) };
        var filtrosSp = new StackPanel { Orientation = Orientation.Horizontal };
        filtrosSp.Children.Add(new TextBlock { Text = "Local:", VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,0,8,0) });
        _cboLocal = new ComboBox { Width = 220, Height = 30 };
        _cboLocal.SelectionChanged += async (_, _) => await CargarAsync();
        filtrosSp.Children.Add(_cboLocal);
        var btnActualizar = new Button { Content = "🔄 Actualizar", Height = 30, Margin = new Thickness(12,0,0,0),
            Padding = new Thickness(12,0,12,0), Background = PB("#0E2F44"), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        btnActualizar.Click += async (_, _) => await CargarAsync();
        filtrosSp.Children.Add(btnActualizar);
        filtros.Child = filtrosSp;
        Grid.SetRow(filtros, 1);
        root.Children.Add(filtros);

        _grid = new DataGrid {
            IsReadOnly = true, AutoGenerateColumns = false, Margin = new Thickness(12),
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            CanUserResizeRows = false, RowHeight = 54, ColumnHeaderHeight = 38,
            BorderThickness = new Thickness(1), BorderBrush = PB("#E0E0E0"),
            Background = System.Windows.Media.Brushes.White,
            AlternatingRowBackground = PB("#FAFAFA"),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        var hdrStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        hdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BackgroundProperty, PB("#37474F")));
        hdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.ForegroundProperty, System.Windows.Media.Brushes.White));
        hdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontWeightProperty, FontWeights.Bold));
        hdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.PaddingProperty, new Thickness(10,6,4,6)));
        _grid.ColumnHeaderStyle = hdrStyle;

        var rowStyle = new Style(typeof(DataGridRow));
        var trigVencido = new DataTrigger { Binding = new System.Windows.Data.Binding("Estado"), Value = "Vencido" };
        trigVencido.Setters.Add(new Setter(DataGridRow.BackgroundProperty, PB("#FFEBEE")));
        rowStyle.Triggers.Add(trigVencido);
        var trigCargado = new DataTrigger { Binding = new System.Windows.Data.Binding("Estado"), Value = "Cargado" };
        trigCargado.Setters.Add(new Setter(DataGridRow.BackgroundProperty, PB("#E8F5E9")));
        rowStyle.Triggers.Add(trigCargado);
        var trigNoAplica = new DataTrigger { Binding = new System.Windows.Data.Binding("Estado"), Value = "No aplica" };
        trigNoAplica.Setters.Add(new Setter(DataGridRow.BackgroundProperty, PB("#F5F5F5")));
        rowStyle.Triggers.Add(trigNoAplica);
        _grid.RowStyle = rowStyle;

        _grid.Columns.Add(new DataGridTextColumn { Header = "N° Cierre", Binding = new System.Windows.Data.Binding("IdMaster"), Width = 75 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Local", Binding = new System.Windows.Data.Binding("LocalNombre"), Width = 110 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Cajero", Binding = new System.Windows.Data.Binding("NombreCajero"), Width = 110 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Fecha de cierre", Binding = new System.Windows.Data.Binding("FechaCierreStr"), Width = 125 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "N° Comprobante", Binding = new System.Windows.Data.Binding("NroComprobante"), Width = 110 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Estado", Binding = new System.Windows.Data.Binding("Estado"), Width = 95 });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Plazo restante", Binding = new System.Windows.Data.Binding("HorasRestantes"), Width = 100 });

        // Botón redondeado reutilizable para las celdas de acción de la grilla — un Button de
        // WPF sin Template usa el chrome nativo cuadrado del sistema, no respeta CornerRadius.
        Style RoundedBtnStyle(string bg, string bgHover)
        {
            var st = new Style(typeof(Button));
            var tpl = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "BtnBorder";
            border.SetValue(Border.BackgroundProperty, PB(bg));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));
            border.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(content);
            tpl.VisualTree = border;
            var hoverTrig = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hoverTrig.Setters.Add(new Setter(Border.BackgroundProperty, PB(bgHover)) { TargetName = "BtnBorder" });
            tpl.Triggers.Add(hoverTrig);
            st.Setters.Add(new Setter(Button.TemplateProperty, tpl));
            st.Setters.Add(new Setter(Button.ForegroundProperty, System.Windows.Media.Brushes.White));
            st.Setters.Add(new Setter(Button.FontWeightProperty, FontWeights.SemiBold));
            st.Setters.Add(new Setter(Button.FontSizeProperty, 11.0));
            st.Setters.Add(new Setter(Button.CursorProperty, Cursors.Hand));
            st.Setters.Add(new Setter(Button.PaddingProperty, new Thickness(14,0,14,0)));
            st.Setters.Add(new Setter(Button.HeightProperty, 28.0));
            return st;
        }

        var colDetalle = new DataGridTemplateColumn { Header = "", Width = 130, MinWidth = 130 };
        var factoryDet = new FrameworkElementFactory(typeof(Button));
        factoryDet.SetValue(Button.ContentProperty, "Ver detalles");
        factoryDet.SetValue(Button.MarginProperty, new Thickness(4,2,4,2));
        factoryDet.SetValue(Button.StyleProperty, RoundedBtnStyle("#37474F", "#455A64"));
        factoryDet.AddHandler(Button.ClickEvent, new RoutedEventHandler(OnVerDetallesClick));
        colDetalle.CellTemplate = new DataTemplate { VisualTree = factoryDet };
        _grid.Columns.Add(colDetalle);

        var colAccion = new DataGridTemplateColumn { Header = "", Width = new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 140 };
        var factory = new FrameworkElementFactory(typeof(Button));
        factory.SetValue(Button.ContentProperty, "Completar");
        factory.SetValue(Button.MarginProperty, new Thickness(4,2,4,2));
        factory.SetValue(Button.StyleProperty, RoundedBtnStyle("#1565C0", "#1976D2"));
        factory.SetBinding(Button.VisibilityProperty, new System.Windows.Data.Binding("PuedeCompletar") {
            Converter = new BoolToVisConv() });
        factory.AddHandler(Button.ClickEvent, new RoutedEventHandler(OnCompletarClick));
        colAccion.CellTemplate = new DataTemplate { VisualTree = factory };
        _grid.Columns.Add(colAccion);

        Grid.SetRow(_grid, 2);
        root.Children.Add(_grid);

        var pie = new Border { Background = PB("#0E2F44"), Padding = new Thickness(16, 10, 16, 10) };
        var btnCerrar = new Button { Content = "✕ Cerrar", Height = 32, Padding = new Thickness(16,0,16,0),
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = PB("#546E7A"), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        btnCerrar.Click += (_, _) => Close();
        pie.Child = btnCerrar;
        Grid.SetRow(pie, 3);
        root.Children.Add(pie);

        Content = root;
    }

    private async Task CargarAsync()
    {
        try
        {
            if (_locales.Count == 0)
            {
                using var conn = _db.Create();
                _locales = (await conn.QueryAsync<LocalItem>(
                    "SELECT ID_LOCAL AS Id, NOMBRE AS Nombre FROM LOCALES ORDER BY NOMBRE")).ToList();

                // Un usuario normal solo ve/filtra por SU propio local — solo un ADMINISTRADOR
                // o el usuario con excepción puntual (Usuario.PuedeVerTodosLosLocales, hoy
                // código "67") puede elegir "Todos los locales" u otro distinto al propio.
                // Mismo criterio que CajaExploradorWindow/CajaHistorialWindow.
                var puedeVerTodos = _session.UsuarioActual?.PuedeVerTodosLosLocales == true;
                if (puedeVerTodos)
                {
                    var itemsCombo = new List<object> { new { Id = 0, Nombre = "Todos los locales" } };
                    itemsCombo.AddRange(_locales.Select(l => (object)l));
                    _cboLocal.ItemsSource = itemsCombo;
                    _cboLocal.DisplayMemberPath = "Nombre";
                    _cboLocal.SelectedIndex = 0;
                }
                else
                {
                    var idLocalSesion = _session.LocalActual?.IdLocal ?? 0;
                    var localSesion = _locales.FirstOrDefault(l => l.Id == idLocalSesion);
                    var itemsCombo = localSesion != null ? new List<object> { localSesion } : new List<object>();
                    _cboLocal.ItemsSource = itemsCombo;
                    _cboLocal.DisplayMemberPath = "Nombre";
                    if (itemsCombo.Count > 0) _cboLocal.SelectedIndex = 0;
                    _cboLocal.IsEnabled = false;
                }
            }

            int? idLocalFiltro = _cboLocal.SelectedItem is LocalItem l2 ? l2.Id : (int?)null;
            var cierres = await _caja.ListarCierresRecientesAsync(idLocalFiltro, dias: 3);

            var filas = cierres.Select(c => new FilaComprobantePendiente
            {
                IdMaster       = c.IdMaster,
                LocalNombre    = c.LocalNombre,
                NombreCajero   = c.NombreCajero,
                FechaCierre    = c.FechaCierre,
                FechaCierreStr = c.FechaCierre?.ToString("dd/MM/yyyy HH:mm") ?? "",
                NroComprobante = !c.AplicaComprobanteDeposito
                    ? "Se registró como pago de salario, no como depósito"
                    : c.NroComprobanteDeposito ?? "",
                Estado = !c.AplicaComprobanteDeposito ? "No aplica"
                    : c.ComprobanteCompleto ? "Cargado" : c.DentroDePlazoComprobante ? "Pendiente" : "Vencido",
                PuedeCompletar = c.AplicaComprobanteDeposito && !c.ComprobanteCompleto && c.DentroDePlazoComprobante,
                HorasRestantes = !c.AplicaComprobanteDeposito ? "—"
                    : !c.FechaCierre.HasValue ? "—"
                    : c.ComprobanteCompleto ? "—"
                    : !c.DentroDePlazoComprobante ? "Vencido"
                    : $"{Math.Max(0, 48 - (int)(DateTime.Now - c.FechaCierre.Value).TotalHours)} hs",
            }).ToList();

            _grid.ItemsSource = filas;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error al cargar comprobantes: " + ex.Message, "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnCompletarClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not FilaComprobantePendiente fila) return;

        var modal = new CajaCompletarComprobanteModal(fila.IdMaster, fila.LocalNombre, fila.NombreCajero, fila.NroComprobante) { Owner = this };
        if (modal.ShowDialog() == true)
            await CargarAsync();
    }

    private void OnVerDetallesClick(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not FilaComprobantePendiente fila) return;
        var modal = new CajaDetalleCierreModal(fila.IdMaster, fila.LocalNombre, fila.NombreCajero) { Owner = this };
        modal.ShowDialog();
    }
}

// Converter simple bool->Visibility para el botón "Completar" (solo visible si PuedeCompletar).
internal sealed class BoolToVisConv : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type t, object p, System.Globalization.CultureInfo c)
        => (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c)
        => throw new NotImplementedException();
}

// ── Modal para completar UN comprobante puntual ──────────────────────────────
public class CajaCompletarComprobanteModal : Window
{
    private readonly ICajaRepository _caja;
    private readonly int _idMaster;
    private TextBox _txtNro = null!;
    private TextBlock _lblFoto = null!;
    private Image _imgMini = null!;
    private Border _miniBorder = null!;
    private Button _btnGuardar = null!;
    private Button _btnCancelar = null!;
    private byte[]? _fotoSeleccionada;
    private byte[]? _fotoMostrada;

    private static System.Windows.Media.SolidColorBrush QB(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    public CajaCompletarComprobanteModal(int idMaster, string localNombre, string cajero, string nroComprobantePrevio = "")
    {
        _caja = App.Services.GetRequiredService<ICajaRepository>();
        _idMaster = idMaster;
        Title = "Completar comprobante de depósito";
        Width = 460; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        // Techo de alto según la pantalla real — sin esto, en monitores chicos (notebooks de
        // resolución baja) el modal podía crecer más alto que la pantalla disponible y dejar
        // el botón "Guardar" del pie fuera del área visible, aunque siguiera ahí y funcionara
        // (el usuario lo veía como "no aparece el botón"). El ScrollViewer del body (ver abajo)
        // permite scrollear el contenido si no entra todo en el alto disponible.
        MaxHeight = SystemParameters.WorkArea.Height - 40;

        var root = new DockPanel();

        var hdr = new Border { Background = QB("#0E2F44"), Padding = new Thickness(20,14,20,14) };
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock { Text = $"CIERRE N° {idMaster} — {localNombre}",
            FontSize = 14, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White });
        hdrSp.Children.Add(new TextBlock { Text = $"Cajero: {cajero}",
            FontSize = 11, Foreground = QB("#90A4AE"), Margin = new Thickness(0,2,0,0) });
        hdr.Child = hdrSp;
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        var pie = new Border { Padding = new Thickness(18,12,18,12),
            BorderBrush = QB("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0) };
        var pieSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        _btnGuardar = new Button { Content = "✔ Guardar", Height = 36, Padding = new Thickness(18,0,18,0),
            Background = QB("#1B5E20"), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, FontSize = 13,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Margin = new Thickness(0,0,8,0) };
        _btnCancelar = new Button { Content = "Cancelar", Height = 36, Padding = new Thickness(18,0,18,0),
            Background = QB("#546E7A"), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, FontSize = 13,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        _btnGuardar.Click += async (_, _) => await OnGuardarAsync();
        _btnCancelar.Click += (_, _) => { DialogResult = false; Close(); };
        pieSp.Children.Add(_btnGuardar); pieSp.Children.Add(_btnCancelar);
        pie.Child = pieSp;
        DockPanel.SetDock(pie, Dock.Bottom); root.Children.Add(pie);

        var body = new StackPanel { Margin = new Thickness(20,16,20,16) };

        body.Children.Add(new TextBlock {
            Text = "Cargue el N° de comprobante y/o la foto del depósito para este cierre. " +
                   "No hace falta completar ambos campos ahora si todavía no cuenta con ellos.",
            FontSize = 11.5, Foreground = QB("#607D8B"), TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0,0,0,16)
        });

        body.Children.Add(new TextBlock { Text = "N° DE COMPROBANTE", FontSize = 10,
            FontWeight = FontWeights.Bold, Foreground = QB("#546E7A") });
        _txtNro = new TextBox { Height = 34, Padding = new Thickness(8,6,8,6),
            Background = System.Windows.Media.Brushes.White, BorderBrush = QB("#BDBDBD"),
            Text = nroComprobantePrevio, Margin = new Thickness(0,5,0,16) };
        body.Children.Add(_txtNro);

        body.Children.Add(new TextBlock { Text = "FOTO DEL COMPROBANTE", FontSize = 10,
            FontWeight = FontWeights.Bold, Foreground = QB("#546E7A"), Margin = new Thickness(0,0,0,5) });
        var btnFoto = new Button {
            Content = "Adjuntar foto de comprobante", Height = 36,
            Background = QB("#0E2F44"), Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.SemiBold, FontSize = 12.5,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand
        };

        _imgMini = new Image { Stretch = System.Windows.Media.Stretch.Uniform, MaxHeight = 130, Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Left };
        _miniBorder = new Border { BorderBrush = QB("#BDBDBD"), BorderThickness = new Thickness(1),
            Background = System.Windows.Media.Brushes.White, Padding = new Thickness(3),
            Margin = new Thickness(0,10,0,0), HorizontalAlignment = HorizontalAlignment.Left,
            Visibility = Visibility.Collapsed, Child = _imgMini };
        _imgMini.MouseLeftButtonUp += (_, _) => VerFotoAmpliada();

        btnFoto.Click += (_, _) =>
        {
            var dlg = new OpenFileDialog { Title = "Seleccionar foto de comprobante",
                Filter = "Imágenes|*.jpg;*.jpeg;*.png;*.bmp|Todos|*.*" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                // Fotos de celular sin comprimir pueden pesar varios MB — el UPDATE a
                // FOTO_COMPROBANTE_DEPOSITO (varbinary(MAX)) tardaba tanto que el usuario
                // interpretaba el botón "Guardar" como roto (sin ningún feedback de progreso
                // mientras tanto, ver más abajo). Se recodifica acá a JPEG con ancho máximo
                // 1600px, que reduce el peso real ~10x sin pérdida visible para un comprobante.
                var original = System.IO.File.ReadAllBytes(dlg.FileName);
                _fotoSeleccionada = ComprimirParaSubida(original);
                _fotoMostrada = _fotoSeleccionada;
                _lblFoto.Text = "✔ Foto seleccionada — haga clic en la imagen para verla en grande.";
                _lblFoto.Foreground = QB("#2E7D32");

                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = new System.IO.MemoryStream(_fotoSeleccionada);
                bmp.CacheOption  = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 300;
                bmp.EndInit();
                bmp.Freeze();
                _imgMini.Source = bmp;
                _miniBorder.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo leer el archivo: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        body.Children.Add(btnFoto);
        _lblFoto = new TextBlock { Text = "Sin foto seleccionada.", FontSize = 10.5,
            Foreground = QB("#90A4AE"), Margin = new Thickness(0,6,0,0), TextWrapping = TextWrapping.Wrap };
        body.Children.Add(_lblFoto);
        body.Children.Add(_miniBorder);

        var scrollBody = new ScrollViewer {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = body
        };
        root.Children.Add(scrollBody);
        Content = root;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { DialogResult = false; Close(); } };
        Loaded += async (_, _) => await CargarFotoExistenteAsync();
    }

    // Si el cierre ya tiene una foto guardada, se precarga como miniatura para que el usuario
    // vea qué hay antes de decidir si la reemplaza — sin esto, el modal parecía "vacío" aunque
    // ya se hubiera cargado algo previamente (solo se mostraba el N° de comprobante, no la foto).
    private async Task CargarFotoExistenteAsync()
    {
        byte[]? fotoExistente;
        try { fotoExistente = await _caja.ObtenerFotoComprobanteDepositoAsync(_idMaster); }
        catch { return; }
        if (fotoExistente == null || fotoExistente.Length == 0) return;

        // No se asigna a _fotoSeleccionada: si el usuario no elige una foto nueva, no hace
        // falta reenviar la misma foto que ya está guardada — solo se usa para mostrarla.
        _fotoMostrada = fotoExistente;
        var bmp = new System.Windows.Media.Imaging.BitmapImage();
        bmp.BeginInit();
        bmp.StreamSource = new System.IO.MemoryStream(fotoExistente);
        bmp.CacheOption  = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        bmp.DecodePixelWidth = 300;
        bmp.EndInit();
        bmp.Freeze();
        _imgMini.Source = bmp;
        _miniBorder.Visibility = Visibility.Visible;
        _lblFoto.Text = "Ya tiene una foto cargada — haga clic para verla en grande, o adjunte una nueva para reemplazarla.";
        _lblFoto.Foreground = QB("#546E7A");
    }

    private void VerFotoAmpliada()
    {
        if (_fotoMostrada == null) return;
        var bmp = new System.Windows.Media.Imaging.BitmapImage();
        bmp.BeginInit();
        bmp.StreamSource = new System.IO.MemoryStream(_fotoMostrada);
        bmp.CacheOption  = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        bmp.EndInit();
        bmp.Freeze();
        var img = new Image { Source = bmp, Stretch = System.Windows.Media.Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8) };
        var win = new Window {
            Title = "Comprobante de depósito", Width = 580, Height = 500,
            MinWidth = 420, MinHeight = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.CanResize,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30,30,30))
        };
        win.Content = new ScrollViewer {
            Content = img,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30,30,30))
        };
        win.ShowDialog();
    }

    private async Task OnGuardarAsync()
    {
        var nro = _txtNro.Text.Trim();
        if (string.IsNullOrWhiteSpace(nro) && _fotoSeleccionada == null)
        {
            MessageBox.Show("Ingrese el N° de comprobante y/o adjunte la foto.", "Validación",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        // Reportado real: con foto adjuntada, el guardado (UPDATE varbinary(MAX)) podía tardar
        // varios segundos y el botón no daba ningún indicio de estar trabajando — el usuario
        // interpretaba que "no respondía". Se deshabilitan los botones y se muestra progreso
        // mientras dura la llamada; la compresión de imagen (ver ComprimirParaSubida) además
        // reduce el tiempo real de espera, no solo lo comunica mejor.
        _btnGuardar.IsEnabled = false;
        _btnCancelar.IsEnabled = false;
        _btnGuardar.Content = "Guardando...";
        try
        {
            await _caja.GuardarComprobanteDepositoAsync(_idMaster, nro, _fotoSeleccionada);
            MessageBox.Show("Comprobante guardado correctamente.", "Éxito",
                MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error al guardar: " + ex.Message, "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _btnGuardar.IsEnabled = true;
            _btnCancelar.IsEnabled = true;
            _btnGuardar.Content = "✔ Guardar";
        }
    }

    // Recodifica a JPEG con ancho máximo 1600px — una foto de comprobante de celular sin
    // comprimir puede pesar 3-8MB; a este tamaño el archivo queda en cientos de KB sin
    // pérdida visible de legibilidad, y el UPDATE a la base deja de sentirse "colgado".
    private static byte[] ComprimirParaSubida(byte[] original)
    {
        const int anchoMaximo = 1600;
        try
        {
            // Primera pasada solo para conocer el ancho real de la imagen.
            var sondeo = new System.Windows.Media.Imaging.BitmapImage();
            sondeo.BeginInit();
            sondeo.StreamSource = new System.IO.MemoryStream(original);
            sondeo.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            sondeo.EndInit();

            System.Windows.Media.Imaging.BitmapSource fuente = sondeo;
            if (sondeo.PixelWidth > anchoMaximo)
            {
                var reescalado = new System.Windows.Media.Imaging.BitmapImage();
                reescalado.BeginInit();
                reescalado.StreamSource = new System.IO.MemoryStream(original);
                reescalado.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                reescalado.DecodePixelWidth = anchoMaximo;
                reescalado.EndInit();
                fuente = reescalado;
            }

            var encoder = new System.Windows.Media.Imaging.JpegBitmapEncoder { QualityLevel = 75 };
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(fuente));
            using var ms = new System.IO.MemoryStream();
            encoder.Save(ms);
            var comprimido = ms.ToArray();

            // Por si la recodificación no ayudó (ej. imagen ya pequeña), usar la que pese menos.
            return comprimido.Length > 0 && comprimido.Length < original.Length ? comprimido : original;
        }
        catch
        {
            // Si algo falla al recodificar (formato no soportado, etc.), se sube el original
            // tal cual — mejor una foto pesada que ninguna foto.
            return original;
        }
    }
}

// ── Detalle completo de un cierre (solo lectura) ─────────────────────────────
// Para que el dueño del negocio pueda ver, desde "Comprobantes de Depósito Pendientes",
// toda la info de un cierre puntual sin tener que ir a Historial de Caja aparte: montos,
// movimientos, y el estado + foto del comprobante de depósito si ya fue cargado.
public class CajaDetalleCierreModal : Window
{
    private readonly ICajaRepository      _caja;
    private readonly int                  _idMaster;
    private readonly string               _localNombre;
    private readonly string               _cajero;

    private static System.Windows.Media.SolidColorBrush DB(string h) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(h));

    public CajaDetalleCierreModal(int idMaster, string localNombre, string cajero)
    {
        _caja        = App.Services.GetRequiredService<ICajaRepository>();
        _idMaster    = idMaster;
        _localNombre = localNombre;
        _cajero      = cajero;

        Title = $"Detalle del Cierre N° {idMaster}";
        Width = 760; Height = 640;
        MinWidth = 620; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = System.Windows.Media.Brushes.White;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");

        var root = new DockPanel();

        var hdr = new Border { Background = DB("#0E2F44"), Padding = new Thickness(20,14,20,14) };
        var hdrSp = new StackPanel();
        hdrSp.Children.Add(new TextBlock { Text = $"CIERRE N° {idMaster} — {localNombre}",
            FontSize = 15, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White });
        hdrSp.Children.Add(new TextBlock { Text = $"Cajero: {cajero}",
            FontSize = 11, Foreground = DB("#90A4AE"), Margin = new Thickness(0,2,0,0) });
        hdr.Child = hdrSp;
        DockPanel.SetDock(hdr, Dock.Top); root.Children.Add(hdr);

        var pie = new Border { Padding = new Thickness(16,10,16,10),
            BorderBrush = DB("#E0E0E0"), BorderThickness = new Thickness(0,1,0,0) };
        var btnCerrar = new Button { Content = "✕ Cerrar", Height = 34, Padding = new Thickness(18,0,18,0),
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = DB("#546E7A"), Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        btnCerrar.Click += (_, _) => Close();
        pie.Child = btnCerrar;
        DockPanel.SetDock(pie, Dock.Bottom); root.Children.Add(pie);

        var loading = new TextBlock { Text = "Cargando...", Margin = new Thickness(20),
            Foreground = DB("#78909C") };
        DockPanel.SetDock(loading, Dock.Top);
        root.Children.Add(loading);
        Content = root;

        Loaded += async (_, _) =>
        {
            await CargarYConstruirAsync(root, loading);
        };
    }

    private async Task CargarYConstruirAsync(DockPanel root, TextBlock loading)
    {
        CrediSoft.Core.Models.CajaMaster? cierre;
        IEnumerable<CrediSoft.Core.Models.CajaDetalle> movimientos;
        byte[]? foto;
        try
        {
            cierre      = await _caja.ObtenerCierrePorIdAsync(_idMaster);
            movimientos = await _caja.ObtenerMovimientosAsync(_idMaster);
            foto        = await _caja.ObtenerFotoComprobanteDepositoAsync(_idMaster);
        }
        catch (Exception ex)
        {
            loading.Text = "Error al cargar el detalle: " + ex.Message;
            loading.Foreground = DB("#B71C1C");
            return;
        }

        root.Children.Remove(loading);
        if (cierre == null)
        {
            var err = new TextBlock { Text = $"No se encontró el cierre N° {_idMaster}.",
                Margin = new Thickness(20), Foreground = DB("#B71C1C") };
            DockPanel.SetDock(err, Dock.Top);
            root.Children.Add(err);
            return;
        }

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var body = new StackPanel { Margin = new Thickness(20,16,20,16) };

        // Resumen del cierre
        var resumen = new Border { Background = DB("#F5F5F5"), BorderBrush = DB("#E0E0E0"),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
            Padding = new Thickness(14,10,14,10) };
        var resumenG = new Grid();
        for (int i = 0; i < 3; i++)
            resumenG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        int resRow = 0;
        void NewResRow() { resumenG.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); resRow++; }
        void ResItem(string lbl, string val, int col) {
            var sp = new StackPanel { Margin = new Thickness(0,0,0,10) };
            Grid.SetRow(sp, resRow - 1); Grid.SetColumn(sp, col);
            sp.Children.Add(new TextBlock { Text = lbl, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = DB("#78909C") });
            sp.Children.Add(new TextBlock { Text = val, FontSize = 14, FontWeight = FontWeights.Bold, Foreground = DB("#0E2F44"), Margin = new Thickness(0,2,0,0) });
            resumenG.Children.Add(sp);
        }
        NewResRow();
        ResItem("FECHA APERTURA", cierre.FechaApertura.ToString("dd/MM/yyyy HH:mm"), 0);
        ResItem("FECHA CIERRE", cierre.FechaCierre?.ToString("dd/MM/yyyy HH:mm") ?? "—", 1);
        ResItem("BASE EN CAJA", $"Gs. {cierre.MontoBase:N0}", 2);
        NewResRow();
        ResItem("TOTAL INGRESOS", $"Gs. {cierre.TotIngresos:N0}", 0);
        ResItem("TOTAL EGRESOS", $"Gs. {cierre.TotEgresos:N0}", 1);
        ResItem("MONTO CIERRE REAL", $"Gs. {cierre.MontoGierreReal:N0}", 2);
        resumen.Child = resumenG;
        body.Children.Add(resumen);

        // Desglose de lo que el cajero declaró por medio de pago al cerrar — solo si el cierre
        // ya viene con este dato nuevo cargado (cierres viejos, previos a este cambio, quedan
        // con estas columnas NULL y no muestran esta sección).
        var tieneDesglose = cierre.MontoCierreEfectivo.HasValue || cierre.MontoCierreTarjeta.HasValue ||
            cierre.MontoCierreTransf.HasValue || cierre.MontoCierreQr.HasValue || cierre.MontoCierreCheque.HasValue;
        if (tieneDesglose)
        {
            body.Children.Add(new TextBlock { Text = "DECLARADO POR MEDIO DE PAGO (AL CERRAR)", FontSize = 10,
                FontWeight = FontWeights.Bold, Foreground = DB("#78909C"), Margin = new Thickness(0,14,0,6) });
            var desgloseBorder = new Border { Background = DB("#F5F5F5"), BorderBrush = DB("#E0E0E0"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
                Padding = new Thickness(14,10,14,10) };
            var desgloseG = new Grid();
            for (int i = 0; i < 5; i++)
                desgloseG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            desgloseG.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var desgloseVals = new (string Lbl, decimal? Val)[] {
                ("EFECTIVO", cierre.MontoCierreEfectivo), ("TRANSFERENCIA", cierre.MontoCierreTransf),
                ("QR", cierre.MontoCierreQr), ("TARJETA", cierre.MontoCierreTarjeta), ("CHEQUE", cierre.MontoCierreCheque)
            };
            for (int i = 0; i < desgloseVals.Length; i++)
            {
                var sp = new StackPanel();
                Grid.SetRow(sp, 0); Grid.SetColumn(sp, i);
                sp.Children.Add(new TextBlock { Text = desgloseVals[i].Lbl, FontSize = 10,
                    FontWeight = FontWeights.Bold, Foreground = DB("#78909C") });
                sp.Children.Add(new TextBlock { Text = $"Gs. {desgloseVals[i].Val ?? 0:N0}", FontSize = 13,
                    FontWeight = FontWeights.Bold, Foreground = DB("#0E2F44"), Margin = new Thickness(0,2,0,0) });
                desgloseG.Children.Add(sp);
            }
            desgloseBorder.Child = desgloseG;
            body.Children.Add(desgloseBorder);
        }

        if (!string.IsNullOrWhiteSpace(cierre.Observaciones))
        {
            body.Children.Add(new TextBlock { Text = "OBSERVACIONES DEL CIERRE", FontSize = 10,
                FontWeight = FontWeights.Bold, Foreground = DB("#78909C"), Margin = new Thickness(0,14,0,3) });
            body.Children.Add(new TextBlock { Text = cierre.Observaciones, FontSize = 12,
                TextWrapping = TextWrapping.Wrap, Foreground = DB("#37474F") });
        }

        // Movimientos
        body.Children.Add(new TextBlock { Text = "MOVIMIENTOS", FontSize = 10, FontWeight = FontWeights.Bold,
            Foreground = DB("#78909C"), Margin = new Thickness(0,18,0,6) });
        var movsList = movimientos.ToList();
        // MaxHeight con tope + scroll propio del DataGrid — con 50+ movimientos, la altura no
        // puede crecer sin límite (competiría mal con el ScrollViewer general del modal), así
        // que se acota a ~10 filas visibles y el resto se recorre con el scroll interno.
        const double movRowH = 30, movHeaderH = 32;
        var movAlturaDeseada = movHeaderH + Math.Max(1, movsList.Count) * movRowH + 4;
        var movAlturaMax = movHeaderH + 10 * movRowH;
        var dg = new DataGrid {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            CanUserResizeRows = false, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = DB("#EEEEEE"), Background = System.Windows.Media.Brushes.White,
            RowBackground = System.Windows.Media.Brushes.White, AlternatingRowBackground = DB("#FAFAFA"),
            BorderThickness = new Thickness(1), BorderBrush = DB("#BDBDBD"), FontSize = 12,
            RowHeight = movRowH, ColumnHeaderHeight = movHeaderH,
            Height = Math.Min(movAlturaDeseada, movAlturaMax),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HeadersVisibility = DataGridHeadersVisibility.Column
        };
        var movHdrStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        movHdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BackgroundProperty, DB("#37474F")));
        movHdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.ForegroundProperty, System.Windows.Media.Brushes.White));
        movHdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontWeightProperty, FontWeights.Bold));
        movHdrStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.PaddingProperty, new Thickness(10,6,10,6)));
        dg.ColumnHeaderStyle = movHdrStyle;
        dg.Columns.Add(new DataGridTextColumn { Header = "Hora",
            Binding = new System.Windows.Data.Binding("FechaHora") { StringFormat = "HH:mm:ss" }, Width = 85 });
        dg.Columns.Add(new DataGridTextColumn { Header = "Tipo",
            Binding = new System.Windows.Data.Binding("Tipo"), Width = 55 });
        dg.Columns.Add(new DataGridTextColumn { Header = "Concepto",
            Binding = new System.Windows.Data.Binding("Subtipo"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        dg.Columns.Add(new DataGridTextColumn { Header = "Método",
            Binding = new System.Windows.Data.Binding("FormaPago"), Width = 110 });
        dg.Columns.Add(new DataGridTextColumn { Header = "Monto",
            Binding = new System.Windows.Data.Binding("Monto") { StringFormat = "N0" }, Width = 110,
            ElementStyle = new Style(typeof(TextBlock)) {
                Setters = { new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right) } } });
        dg.ItemsSource = movsList;
        body.Children.Add(dg);

        // Comprobante de depósito
        body.Children.Add(new TextBlock { Text = "COMPROBANTE DE DEPÓSITO", FontSize = 10, FontWeight = FontWeights.Bold,
            Foreground = DB("#78909C"), Margin = new Thickness(0,18,0,6) });

        var (estadoTexto, estadoBg, estadoFg) =
            !cierre.AplicaComprobanteDeposito ? ("ℹ No aplica — se registró como pago de salario", "#E3F2FD", "#0D47A1") :
            cierre.ComprobanteCompleto ? ("✔ Cargado", "#E8F5E9", "#1B5E20") :
            cierre.DentroDePlazoComprobante ? ("⏳ Pendiente — dentro de plazo", "#FFF8E1", "#8D6E00") :
            ("⚠ Vencido — no se completó a tiempo", "#FFEBEE", "#B71C1C");

        var compBox = new Border { Background = DB(estadoBg), BorderBrush = DB(estadoFg),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
            Padding = new Thickness(14,10,14,10) };
        var compSp = new StackPanel();
        compSp.Children.Add(new TextBlock { Text = estadoTexto, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = DB(estadoFg) });
        compSp.Children.Add(new TextBlock {
            Text = !cierre.AplicaComprobanteDeposito
                ? "El efectivo salió como sueldo, no como depósito bancario. No corresponde exigir un comprobante que nunca va a existir."
                : string.IsNullOrWhiteSpace(cierre.NroComprobanteDeposito)
                    ? "Sin N° de comprobante cargado."
                    : $"N° de comprobante: {cierre.NroComprobanteDeposito}",
            FontSize = 12, Foreground = DB("#37474F"), Margin = new Thickness(0,6,0,0) });
        compBox.Child = compSp;
        body.Children.Add(compBox);

        if (foto != null && foto.Length > 0)
        {
            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = new System.IO.MemoryStream(foto);
            bmp.CacheOption  = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = 320;
            bmp.EndInit();
            bmp.Freeze();
            var imgMini = new Image { Source = bmp, Stretch = System.Windows.Media.Stretch.Uniform,
                MaxHeight = 160, Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Left };
            var miniBorder = new Border { BorderBrush = DB("#BDBDBD"), BorderThickness = new Thickness(1),
                Background = System.Windows.Media.Brushes.White, Padding = new Thickness(3),
                Margin = new Thickness(0,10,0,0), HorizontalAlignment = HorizontalAlignment.Left,
                Child = imgMini };
            imgMini.MouseLeftButtonUp += (_, _) => VerFotoAmpliada(foto);
            body.Children.Add(miniBorder);
            body.Children.Add(new TextBlock { Text = "Haga clic en la imagen para verla en grande.",
                FontSize = 10, Foreground = DB("#90A4AE"), Margin = new Thickness(0,4,0,0) });
        }
        else if (cierre.NroComprobanteDeposito is not (null or ""))
        {
            body.Children.Add(new TextBlock { Text = "No se adjuntó foto del comprobante.",
                FontSize = 11, Foreground = DB("#90A4AE"), Margin = new Thickness(0,8,0,0) });
        }

        scroll.Content = body;
        DockPanel.SetDock(scroll, Dock.Top);
        root.Children.Add(scroll);
    }

    private void VerFotoAmpliada(byte[] datos)
    {
        var bmp = new System.Windows.Media.Imaging.BitmapImage();
        bmp.BeginInit();
        bmp.StreamSource = new System.IO.MemoryStream(datos);
        bmp.CacheOption  = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        bmp.EndInit();
        bmp.Freeze();
        var img = new Image { Source = bmp, Stretch = System.Windows.Media.Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8) };
        var win = new Window {
            Title = "Comprobante de depósito", Width = 580, Height = 500,
            MinWidth = 420, MinHeight = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            ResizeMode = ResizeMode.CanResize,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30,30,30))
        };
        win.Content = new ScrollViewer {
            Content = img,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30,30,30))
        };
        win.ShowDialog();
    }
}
