using CrediSoft.UI.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace CrediSoft.UI.Views.Update;

public class UpdateDialog : Window
{
    private readonly UpdateService _svc;
    private readonly UpdateInfo    _info;

    private Button?       _btnAhora;
    private Button?       _btnDespues;
    private TextBlock?    _txtProgreso;
    private ProgressBar?  _barProgreso;
    private StackPanel?   _panelBotones;

    private static SolidColorBrush B(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex));

    public UpdateDialog(UpdateService svc, UpdateInfo info)
    {
        _svc  = svc;
        _info = info;

        Title                 = info.Obligatoria
            ? "ElectroMar — Actualización obligatoria"
            : "ElectroMar — Actualización disponible";
        Width                 = 480;
        SizeToContent         = SizeToContent.Height;
        ResizeMode            = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background            = B("#EEF4FB");
        FontFamily            = new FontFamily("Segoe UI");
        ShowInTaskbar         = true;

        Content = BuildUI();

        if (info.Obligatoria)
        {
            // Sin botón de cierre en la ventana, sin Alt+F4/Esc y sin "Más tarde" —
            // esta versión corrige un bug crítico con riesgo de pérdida de dinero
            // (ver notas de la versión), así que no se puede seguir usando el sistema
            // con la versión vieja hasta actualizar.
            WindowStyle = WindowStyle.None;
            ResizeMode  = ResizeMode.NoResize;
            Closing    += (_, ce) => { if (!_actualizacionCompletada) ce.Cancel = true; };
            KeyDown    += (_, ke) => { if (ke.Key == System.Windows.Input.Key.Escape) ke.Handled = true; };

            // TODA actualización es obligatoria y se aplica de inmediato, sin esperar un
            // click: apenas la ventana termina de cargar se dispara sola la descarga —
            // no hay "Actualizar ahora" que presionar. Esto corta lo que el cajero esté
            // haciendo en ese momento (venta/cobro a medio tipear) a propósito: la
            // prioridad es que ninguna copia se quede corriendo una versión vieja, ni
            // siquiera unos minutos — ver instrucción explícita del cliente sobre cierre
            // forzado inmediato en cada actualización.
            Loaded += (_, _) => OnActualizarAhora(this, new RoutedEventArgs());
        }
    }

    private bool _actualizacionCompletada = false;

    private UIElement BuildUI()
    {
        var root = new DockPanel();

        // ── Header ──────────────────────────────────────────────────────────
        var header = new Border
        {
            Background = B("#0E2F44"),
            Padding    = new Thickness(24, 18, 24, 18)
        };
        var hRow = new DockPanel();

        var icono = new Border
        {
            Width = 46, Height = 46, CornerRadius = new CornerRadius(23),
            Background = B(_info.Obligatoria ? "#C62828" : "#1565C0"), Margin = new Thickness(0, 0, 16, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = _info.Obligatoria ? "⛔" : "🔄", FontSize = 22,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            }
        };
        DockPanel.SetDock(icono, Dock.Left);
        hRow.Children.Add(icono);

        var hStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        hStack.Children.Add(new TextBlock
        {
            Text       = _info.Obligatoria ? "ACTUALIZACIÓN OBLIGATORIA" : "ACTUALIZACIÓN DISPONIBLE",
            Foreground = Brushes.White,
            FontSize   = 14, FontWeight = FontWeights.Bold
        });
        hStack.Children.Add(new TextBlock
        {
            Text       = $"Versión {_info.Version}  ·  {_info.Fecha}",
            Foreground = B("#90CAF9"),
            FontSize   = 11, Margin = new Thickness(0, 4, 0, 0)
        });
        hRow.Children.Add(hStack);
        header.Child = hRow;
        DockPanel.SetDock(header, Dock.Top);
        // Sin WindowStyle estándar (caso obligatoria) no hay barra de título nativa
        // para arrastrar — el propio header cumple esa función.
        header.MouseLeftButtonDown += (_, _) => { try { DragMove(); } catch { } };
        root.Children.Add(header);

        // ── Footer ──────────────────────────────────────────────────────────
        var footer = new Border
        {
            Background      = Brushes.White,
            BorderBrush     = B("#BBDEFB"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding         = new Thickness(20, 14, 20, 14)
        };

        _panelBotones = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

        if (!_info.Obligatoria)
        {
            _btnDespues = MakeBtn("🕐  Más tarde", "#546E7A");
            _btnDespues.Width   = 130;
            _btnDespues.Click  += (_, _) => { DialogResult = false; Close(); };
            _panelBotones.Children.Add(_btnDespues);

            _btnAhora         = MakeBtn("⬇  Actualizar ahora", "#1565C0");
            _btnAhora.Width   = 180;
            _btnAhora.Margin  = new Thickness(12, 0, 0, 0);
            _btnAhora.Click  += OnActualizarAhora;
            _panelBotones.Children.Add(_btnAhora);
        }
        // Obligatoria: sin botones — la descarga se dispara sola en Loaded, no hay
        // nada que el usuario pueda pulsar para elegir el momento.

        footer.Child = _panelBotones;
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        // ── Body ─────────────────────────────────────────────────────────────
        var body = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };

        // Aviso de bloqueo — solo cuando la actualización es obligatoria
        if (_info.Obligatoria)
        {
            var cardObligatoria = new Border
            {
                Background      = B("#FDECEA"),
                BorderBrush     = B("#F5C6C0"),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(8),
                Padding         = new Thickness(16, 12, 16, 12),
                Margin          = new Thickness(0, 0, 0, 16)
            };
            var obligatoriaSp = new StackPanel();
            obligatoriaSp.Children.Add(new TextBlock
            {
                Text       = "⚠  ACTUALIZACIÓN OBLIGATORIA",
                Foreground = B("#C62828"),
                FontSize   = 12.5, FontWeight = FontWeights.Bold,
                Margin     = new Thickness(0, 0, 0, 6)
            });
            obligatoriaSp.Children.Add(new TextBlock
            {
                Text         = "La actualización se está aplicando automáticamente. El sistema se cerrará y reiniciará en breve — no se puede posponer ni cancelar.",
                Foreground   = B("#8A1C17"),
                FontSize     = 12, TextWrapping = TextWrapping.Wrap
            });
            cardObligatoria.Child = obligatoriaSp;
            body.Children.Add(cardObligatoria);
        }

        // Notas de la versión
        if (!string.IsNullOrWhiteSpace(_info.Notas))
        {
            body.Children.Add(new TextBlock
            {
                Text       = "NOVEDADES",
                Foreground = B("#78909C"),
                FontSize   = 10, FontWeight = FontWeights.Bold,
                Margin     = new Thickness(0, 0, 0, 8)
            });

            var cardNotas = new Border
            {
                Background      = Brushes.White,
                BorderBrush     = B("#BBDEFB"),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(8),
                Padding         = new Thickness(16, 12, 16, 12),
                Margin          = new Thickness(0, 0, 0, 16),
                Effect          = new DropShadowEffect { BlurRadius = 6, Opacity = 0.06, ShadowDepth = 1, Direction = 270, Color = Colors.Black }
            };
            cardNotas.Child = new TextBlock
            {
                Text         = _info.Notas,
                Foreground   = B("#0E2F44"),
                FontSize     = 13,
                TextWrapping = TextWrapping.Wrap,
                LineHeight   = 22
            };
            body.Children.Add(cardNotas);
        }

        // Aviso
        body.Children.Add(new TextBlock
        {
            Text         = _info.Obligatoria
                ? "La aplicación se reiniciará automáticamente al finalizar la actualización."
                : "La aplicación se reiniciará automáticamente al finalizar la actualización. Guardá tu trabajo antes de continuar.",
            Foreground   = B("#546E7A"),
            FontSize     = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin       = new Thickness(0, 0, 0, 16)
        });

        // Barra de progreso (oculta hasta que inicie la descarga)
        var panelProgreso = new StackPanel { Visibility = Visibility.Collapsed };

        _txtProgreso = new TextBlock
        {
            Text       = "Descargando actualización...",
            Foreground = B("#1565C0"),
            FontSize   = 12,
            Margin     = new Thickness(0, 0, 0, 6)
        };

        _barProgreso = new ProgressBar
        {
            Height      = 8,
            Minimum     = 0,
            Maximum     = 100,
            Value       = 0,
            Foreground  = B("#1565C0"),
            Background  = B("#BBDEFB"),
            BorderThickness = new Thickness(0)
        };

        panelProgreso.Children.Add(_txtProgreso);
        panelProgreso.Children.Add(_barProgreso);
        body.Children.Add(panelProgreso);

        // Guardar referencia al panel de progreso
        _barProgreso.Tag = panelProgreso;

        root.Children.Add(new ScrollViewer
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 400
        });

        return root;
    }

    private async void OnActualizarAhora(object s, RoutedEventArgs e)
    {
        if (_barProgreso == null || _txtProgreso == null) return;

        if (_btnAhora != null)
        {
            _btnAhora.IsEnabled = false;
            _btnAhora.Content   = "Descargando...";
        }

        // Mostrar panel de progreso
        if (_barProgreso.Tag is StackPanel panel)
            panel.Visibility = Visibility.Visible;

        // Botón "Más tarde" también se deshabilita durante descarga (si existe — no lo
        // hay cuando la actualización es obligatoria)
        if (_btnDespues != null) _btnDespues.IsEnabled = false;

        var progreso = new Progress<int>(pct =>
        {
            _barProgreso.Value  = pct;
            _txtProgreso.Text   = $"Descargando actualización... {pct}%";
        });

        var ok = await _svc.DescargarYAplicarAsync(_info, progreso);

        if (ok)
        {
            _txtProgreso.Text      = "✔  Actualización completada. Reiniciando...";
            _barProgreso.Value     = 100;
            _barProgreso.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"));
            _actualizacionCompletada = true;

            await Task.Delay(1200);
            _svc.ReiniciarApp();
        }
        else if (_btnAhora != null)
        {
            // Modo opcional: el usuario puede reintentar manualmente.
            _txtProgreso.Text      = "✗  No se pudo descargar la actualización. Intentá más tarde.";
            _txtProgreso.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C62828"));
            _btnAhora.Content      = "⬇  Reintentar";
            _btnAhora.IsEnabled    = true;
            if (_btnDespues != null) _btnDespues.IsEnabled = true;
        }
        else
        {
            // Modo obligatorio sin botones: si falla (ej. red caída), reintentar
            // automáticamente en unos segundos — no hay forma de que el usuario lo
            // dispare a mano, y no se puede dejar la app bloqueada en esta pantalla
            // para siempre sin reintentar.
            _txtProgreso.Text      = "✗  No se pudo descargar. Reintentando...";
            _txtProgreso.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C62828"));
            await Task.Delay(5000);
            OnActualizarAhora(s, e);
        }
    }

    private static Button MakeBtn(string txt, string bg) => new()
    {
        Content         = txt,
        Height          = 40,
        Background      = B(bg),
        Foreground      = Brushes.White,
        BorderThickness = new Thickness(0),
        FontSize        = 13,
        FontWeight      = FontWeights.SemiBold,
        Cursor          = System.Windows.Input.Cursors.Hand
    };
}
