using CrediSoft.Data;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;

namespace CrediSoft.UI.Views.Shared;

// ─────────────────────────────────────────────────────────────────────────────
//  Datos para imprimir
// ─────────────────────────────────────────────────────────────────────────────
public record DatosTicketCobro(
    string   NombreEmpresa,
    string   NombreLocal,
    DateTime Fecha,
    long     NumeroTicket,
    string   Comprobante,
    string   Concepto,
    int      NCuota,
    decimal  EntregaCapital,
    int      DiasAtraso,
    decimal  Punitorio,
    decimal  Reajuste,
    decimal  EntregaAnterior,
    decimal  TotalPagado,
    string   Timbrado,
    string   VigenciaDesde,
    string   VigenciaHasta,
    string   NombreCliente,
    string   Cajero            = "",
    string   MetodoPago        = "EFECTIVO",
    // Solo en EFECTIVO cuando el cliente entrega más que el total a cobrar: lo realmente
    // recibido en mano y el vuelto que se le devolvió — para que el comprobante muestre el
    // desglose completo (Total a Cobrar / Total Entregado / Vuelto), no solo lo acreditado.
    decimal  EfectivoRecibido  = 0,
    decimal  Vuelto            = 0,
    // Teléfono del local (LOCALES.TELEFONO) — se imprime debajo del nombre del local en el
    // encabezado. Default "" para no romper las llamadas existentes; si el local no tiene
    // teléfono cargado, esa línea simplemente no se dibuja (ver DibujarEncabezadoProfesional).
    string   TelefonoLocal     = ""
);

public record ArticuloTicket(string Descripcion, decimal Cantidad, decimal Precio, decimal Entrega);

public record DatosTicketVenta(
    string   NombreEmpresa,
    string   NombreLocal,
    DateTime Fecha,
    long     NumeroTicket,
    string   NroSolicitud,
    string   Vendedor,
    string   NombreCliente,
    decimal  TotalVenta,
    decimal  TotalEntrega,
    decimal  TotalConInteres,
    int      CantCuotas,
    decimal  CostoCuota,
    List<ArticuloTicket> Articulos,
    string   Timbrado,
    string   VigenciaDesde,
    string   VigenciaHasta,
    // Venta a crédito (default) dice "SOLICITUD DE VENTA A CRÉDITO"/"SOLICITUD APROBADA" —
    // Venta Contado pasa EsContado:true para que el mismo ticket diga "VENTA AL CONTADO" en
    // vez de hablar de una solicitud que en este caso no existe.
    bool     EsContado = false,
    // Teléfono del local (LOCALES.TELEFONO) — mismo criterio que DatosTicketCobro.
    string   TelefonoLocal = ""
);

// ─────────────────────────────────────────────────────────────────────────────
//  Servicio de impresión
// ─────────────────────────────────────────────────────────────────────────────
public static class TicketPrinter
{
    private const int PaperWidth = 302; // ~80mm at 96dpi

    // ── Datos empresa desde BD ────────────────────────────────────────────────
    public static async Task<(string Nombre, string Direccion, string Ciudad, string Telefono,
                               string Ruc, string Timbrado, string Desde, string Hasta)> ObtenerDatosEmpresaAsync()
    {
        try
        {
            using var conn = App.Services.GetRequiredService<IDbConnectionFactory>().Create();
            var row = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT TOP 1 NOMBRE_EMPRESA, DIRECCION_EMPRESA, CIUDAD_EMPRESA, TELEFONO_EMPRESA," +
                " RUC_EMPRESA, TIMBRADO, INICIO_DE_VIGENCIA, VALIDO_HASTA FROM EMPRESA");
            if (row == null) return ("", "", "", "", "", "", "", "");
            string Str(object? v) => v?.ToString()?.Trim() ?? "";
            string Dt(object? v)  => v is DateTime d ? d.ToString("dd/MM/yyyy") : "";
            return (Str(row.NOMBRE_EMPRESA), Str(row.DIRECCION_EMPRESA), Str(row.CIUDAD_EMPRESA),
                    Str(row.TELEFONO_EMPRESA), Str(row.RUC_EMPRESA), Str(row.TIMBRADO),
                    Dt(row.INICIO_DE_VIGENCIA), Dt(row.VALIDO_HASTA));
        }
        catch { return ("", "", "", "", "", "", "", ""); }
    }

    // ── Obtener número de ticket ──────────────────────────────────────────────
    public static async Task<long> ObtenerNumeroTicketAsync(int idSucursal)
    {
        try
        {
            using var conn = App.Services.GetRequiredService<IDbConnectionFactory>().Create();
            var prm = new DynamicParameters();
            prm.Add("@IdSucursal",  idSucursal);
            prm.Add("@NuevoNumero", dbType: System.Data.DbType.Int32,
                    direction: System.Data.ParameterDirection.Output);
            await conn.ExecuteAsync("sp_ObtenerNumeroTicket_cs", prm,
                commandType: System.Data.CommandType.StoredProcedure);
            return prm.Get<int>("@NuevoNumero");
        }
        catch { return 0; }
    }

    // ── Registro histórico de comprobantes (para reimpresión posterior) ────────
    // "Bibliorato" central: cada ticket impreso queda guardado acá con un snapshot JSON
    // completo del objeto usado para dibujarlo (DatosTicketCobro/DatosTicketVenta) — la
    // reimpresión deserializa ese JSON y vuelve a dibujar exactamente lo mismo, sin depender
    // de reconstruir el dato desde otras tablas (que pueden haber cambiado desde entonces,
    // ej. GENERADAS recalculado). Tabla self-provisioning, mismo patrón ya usado en
    // CuotaRepository.AsegurarTablaAsignacionesAsync para COBRANZA_ASIGNACIONES — se crea
    // sola la primera vez que se necesita, sin requerir un script de migración manual.
    private static readonly object _schemaLockComprobantes = new();
    private static bool _schemaVerificadoComprobantes = false;

    private static async Task AsegurarTablaComprobantesAsync(System.Data.IDbConnection conn)
    {
        if (_schemaVerificadoComprobantes) return;
        lock (_schemaLockComprobantes) { if (_schemaVerificadoComprobantes) return; }

        var existe = await conn.ExecuteScalarAsync<int>(
            "SELECT CASE WHEN OBJECT_ID('dbo.COMPROBANTES_GENERADOS', 'U') IS NULL THEN 0 ELSE 1 END");
        if (existe == 0)
        {
            var ddl = @"
                CREATE TABLE dbo.COMPROBANTES_GENERADOS
                (
                    IDCOMPROBANTE      INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    TIPO               VARCHAR(20)  NOT NULL,
                    NUMERO_TICKET      BIGINT       NOT NULL,
                    ID_LOCAL           INT          NOT NULL,
                    FECHA_EMISION      DATETIME     NOT NULL CONSTRAINT DF_COMPROBANTES_GENERADOS_FECHA DEFAULT(GETDATE()),
                    ID_USUARIO_CAJERO  INT          NULL,
                    NOMBRE_CAJERO      VARCHAR(100) NULL,
                    ID_CLIENTE         INT          NULL,
                    NOMBRE_CLIENTE     VARCHAR(150) NULL,
                    ID_CAB             INT          NULL,
                    ID_GENERADAS       INT          NULL,
                    NRO_SOLICITUD      VARCHAR(30)  NULL,
                    NRECIBO            VARCHAR(30)  NULL,
                    MONTO_TOTAL        DECIMAL(18,2) NULL,
                    DATOS_JSON         NVARCHAR(MAX) NOT NULL,
                    ID_USUARIO_REGISTRO INT         NULL
                );

                CREATE INDEX IX_COMPROBANTES_GENERADOS_TICKET
                    ON dbo.COMPROBANTES_GENERADOS (NUMERO_TICKET, ID_LOCAL, TIPO);

                CREATE INDEX IX_COMPROBANTES_GENERADOS_FECHA
                    ON dbo.COMPROBANTES_GENERADOS (FECHA_EMISION DESC);

                CREATE INDEX IX_COMPROBANTES_GENERADOS_CLIENTE
                    ON dbo.COMPROBANTES_GENERADOS (ID_CLIENTE, FECHA_EMISION DESC);

                CREATE INDEX IX_COMPROBANTES_GENERADOS_CAB
                    ON dbo.COMPROBANTES_GENERADOS (ID_CAB, FECHA_EMISION DESC);

                CREATE INDEX IX_COMPROBANTES_GENERADOS_GENERADAS
                    ON dbo.COMPROBANTES_GENERADOS (ID_GENERADAS, FECHA_EMISION DESC);";
            await conn.ExecuteAsync(ddl);
        }
        lock (_schemaLockComprobantes) { _schemaVerificadoComprobantes = true; }
    }

    // Se llama justo después de generar cada DatosTicketCobro/DatosTicketVenta, antes o
    // después de imprimir — falla en silencio (catch vacío) para que un problema al guardar
    // el histórico nunca bloquee la impresión real del comprobante, que es lo urgente para
    // el cajero en ese momento.
    public static async Task RegistrarComprobanteAsync(
        string tipo, long numeroTicket, int idLocal, object datosTicket,
        int? idUsuarioCajero = null, string? nombreCajero = null,
        int? idCliente = null, string? nombreCliente = null,
        int? idCab = null, int? idGeneradas = null,
        string? nroSolicitud = null, string? nrecibo = null, decimal? montoTotal = null)
    {
        try
        {
            using var conn = App.Services.GetRequiredService<IDbConnectionFactory>().Create();
            await AsegurarTablaComprobantesAsync(conn);
            var json = System.Text.Json.JsonSerializer.Serialize(datosTicket, datosTicket.GetType());
            await conn.ExecuteAsync(
                @"INSERT INTO COMPROBANTES_GENERADOS
                    (TIPO, NUMERO_TICKET, ID_LOCAL, ID_USUARIO_CAJERO, NOMBRE_CAJERO,
                     ID_CLIENTE, NOMBRE_CLIENTE, ID_CAB, ID_GENERADAS, NRO_SOLICITUD,
                     NRECIBO, MONTO_TOTAL, DATOS_JSON, ID_USUARIO_REGISTRO)
                  VALUES
                    (@tipo, @numeroTicket, @idLocal, @idUsuarioCajero, @nombreCajero,
                     @idCliente, @nombreCliente, @idCab, @idGeneradas, @nroSolicitud,
                     @nrecibo, @montoTotal, @json, @idUsuarioCajero)",
                new {
                    tipo, numeroTicket, idLocal, idUsuarioCajero, nombreCajero,
                    idCliente, nombreCliente, idCab, idGeneradas, nroSolicitud,
                    nrecibo, montoTotal, json
                });
        }
        catch { /* nunca debe romper la impresión */ }
    }

    // ── Configuración de impresoras desde BD ─────────────────────────────────
    // columna: "comprobante" | "factura" | "reporte"
    public static async Task<(string Nombre, bool EsTicket)> ObtenerImpresoraAsync(string columna)
    {
        try
        {
            using var conn = App.Services.GetRequiredService<IDbConnectionFactory>().Create();
            var col  = columna == "factura"  ? "NOMBRE_FACTURA,  TIPO_FACTURA"
                     : columna == "reporte"  ? "NOMBRE_REPORTE,  0 AS TIPO_REPORTE"
                                             : "NOMBRE_COMPROBANTE, TIPO_COMPROBANTE";
            var row  = await conn.QueryFirstOrDefaultAsync<dynamic>(
                $"SELECT TOP 1 {col} FROM IMPRESORAS WHERE NOMBRE_PC=@pc",
                new { pc = Environment.MachineName.ToUpper() });
            if (row == null) return ("", false);

            string nombre = columna == "factura" ? ((string?)row.NOMBRE_FACTURA)?.Trim()       ?? ""
                          : columna == "reporte" ? ((string?)row.NOMBRE_REPORTE)?.Trim()       ?? ""
                                                 : ((string?)row.NOMBRE_COMPROBANTE)?.Trim()   ?? "";
            byte tipo  = columna == "reporte" ? (byte)0
                       : columna == "factura" ? (byte)(row.TIPO_FACTURA ?? 0)
                                              : (byte)(row.TIPO_COMPROBANTE ?? 0);
            return (nombre, tipo == 1);
        }
        catch { return ("", false); }
    }

    // Verifica si una impresora existe en el sistema
    private static bool ImpresoraExiste(string nombre) =>
        !string.IsNullOrWhiteSpace(nombre) &&
        PrinterSettings.InstalledPrinters.Cast<string>()
            .Any(p => string.Equals(p, nombre, StringComparison.OrdinalIgnoreCase));

    private static bool EsImpresoraPdf(string nombre) =>
        nombre.Contains("PDF", StringComparison.OrdinalIgnoreCase) ||
        nombre.Contains("XPS", StringComparison.OrdinalIgnoreCase);

    // Imprime en la impresora configurada.
    // Si la impresora no existe o es PDF → genera PDF en Documents y lo abre.
    public static void ImprimirConConfig(PrintDocument doc, string nombreImpresora)
    {
        try
        {
            bool usarPdf = string.IsNullOrWhiteSpace(nombreImpresora)
                        || EsImpresoraPdf(nombreImpresora)
                        || !ImpresoraExiste(nombreImpresora);

            if (usarPdf)
            {
                ImprimirComoPdf(doc);
            }
            else
            {
                doc.PrinterSettings.PrinterName = nombreImpresora;
                doc.Print();
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error al imprimir:\n{ex.Message}",
                "Impresión", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
    }

    // Genera el ticket como PDF usando "Microsoft Print to PDF" y lo abre automáticamente
    public static void ImprimirComoPdf(PrintDocument doc)
    {
        const string pdfPrinter = "Microsoft Print to PDF";
        if (!ImpresoraExiste(pdfPrinter))
        {
            System.Windows.MessageBox.Show(
                "No se encontró impresora configurada ni 'Microsoft Print to PDF'.\n" +
                "Configure una impresora en Herramientas → Configuración.",
                "Sin impresora", System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        var rutaPdf = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            $"{doc.DocumentName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

        doc.PrinterSettings.PrinterName = pdfPrinter;
        doc.PrinterSettings.PrintFileName = rutaPdf;
        doc.PrinterSettings.PrintToFile   = true;
        doc.Print();

        // Abrir el PDF generado automáticamente
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(rutaPdf)
                { UseShellExecute = true });
        }
        catch { }
    }

    // ── Vista previa ─────────────────────────────────────────────────────────
    public static void MostrarVistaPrevia(PrintDocument doc, string impresora, System.Windows.Window? owner = null)
    {
        var prev = new ReportePrevisualizacion(doc, impresora);
        if (owner != null) prev.Owner = owner;
        prev.ShowDialog();
    }

    private static readonly string _logoPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "credimarlogopng.png");

    // Logo específico para tickets térmicos (80/58/40mm) — versión adaptada al diseño tipo
    // ticketera (pedido explícito 2026-08-10), distinta del logo usado en el encabezado A4.
    private static readonly string _logoPathTicketera =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logotipocredimarticketera.png");

    // ── Encabezado profesional — logo Credimar ────────────────────────────────
    // Devuelve la Y desde donde debe continuar el contenido del reporte.
    public static int DibujarEncabezadoProfesional(
        Graphics g, int lx, int pw, int pgW,
        string titulo, string subtitulo,
        string? logoPath = null,
        string nombreEmpresa = "", string nombreLocal = "", string telefonoLocal = "")
    {
        g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var rojo    = Color.FromArgb(204, 0, 0);
        var azulOsc = Color.FromArgb(13, 71, 161);

        g.FillRectangle(Brushes.White, 0, 0, pgW, 90);

        // Línea superior roja
        using var penRojo = new Pen(rojo, 3f);
        g.DrawLine(penRojo, 0, 0, pgW, 0);

        // ── Logo (icono de casa) — ocupa 56×56 px ────────────────────────────
        int logoX = lx, logoY = 10;
        string rutaLogo = logoPath ?? _logoPath;
        int logoW = 56;

        if (File.Exists(rutaLogo))
        {
            try {
                using var img = System.Drawing.Image.FromFile(rutaLogo);
                int logoH2 = 56;
                logoW = (int)(img.Width * (logoH2 / (float)img.Height));
                g.DrawImage(img, logoX, logoY, logoW, logoH2);
            }
            catch { DibujarIconoCasa(g, logoX, logoY); logoW = 56; }
        }
        else { DibujarIconoCasa(g, logoX, logoY); logoW = 56; }

        // ── Nombre empresa + local (columna izquierda tras el logo) ──────────
        int empX  = lx + logoW + 10;
        int empW  = (pw / 2) - logoW - 10;
        using var fntEmpresa = new Font("Arial", 15, FontStyle.Bold);
        using var fntLocal   = new Font("Arial",  8, FontStyle.Regular);
        using var fntTel     = new Font("Arial",  7, FontStyle.Regular);
        using var sfL = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
        string empresa = string.IsNullOrWhiteSpace(nombreEmpresa) ? "CREDIMAR S.A." : nombreEmpresa.ToUpper();
        string local   = nombreLocal ?? "";
        g.DrawString(empresa, fntEmpresa, new SolidBrush(rojo),    new RectangleF(empX, logoY,      empW, 30), sfL);
        g.DrawString(local,   fntLocal,   new SolidBrush(Color.DimGray), new RectangleF(empX, logoY + 30, empW, 15), sfL);
        // Teléfono del local (LOCALES.TELEFONO) — línea nueva, solo si está cargado; si el
        // local no tiene teléfono en la base, no se dibuja nada acá (no queda un renglón
        // vacío ni un "Tel: " suelto).
        if (!string.IsNullOrWhiteSpace(telefonoLocal))
            g.DrawString($"Tel: {telefonoLocal}", fntTel, new SolidBrush(Color.DimGray), new RectangleF(empX, logoY + 45, empW, 13), sfL);

        // Línea divisoria vertical
        using var penGris = new Pen(Color.FromArgb(180, 180, 180), 1f);
        g.DrawLine(penGris, lx + pw / 2, 12, lx + pw / 2, 72);

        // ── Título del documento (columna derecha) ────────────────────────────
        int titleX  = lx + pw / 2 + 10;
        int titlePw = pw / 2 - 10;
        using var sfC = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var fntSubtit = new Font("Arial", 8, FontStyle.Regular);

        // Títulos largos (ej. "COMPROBANTE DE VENTA AL CONTADO") no entraban en una sola
        // línea a 14pt dentro del ancho disponible — GDI+ hacía wrap a 2 líneas, que
        // desbordaban el rectángulo de 34px de alto y se veían cortadas contra el borde
        // superior del encabezado. Se reduce el tamaño de fuente hasta que el título entre
        // en una sola línea, en vez de asumir un tamaño fijo para cualquier longitud de texto.
        float tamTitulo = 14f;
        Font fntTitulo;
        while (true)
        {
            fntTitulo = new Font("Arial", tamTitulo, FontStyle.Bold);
            var anchoTexto = g.MeasureString(titulo, fntTitulo, int.MaxValue, StringFormat.GenericTypographic).Width;
            if (anchoTexto <= titlePw - 8 || tamTitulo <= 9.5f) break;
            fntTitulo.Dispose();
            tamTitulo -= 0.5f;
        }
        using (fntTitulo)
        {
            g.DrawString(titulo, fntTitulo, new SolidBrush(azulOsc), new RectangleF(titleX, 8, titlePw, 36), sfC);
        }
        g.DrawString(subtitulo, fntSubtit, Brushes.DimGray, new RectangleF(titleX, 46, titlePw, 18), sfC);

        // ── Línea separadora roja bajo encabezado ─────────────────────────────
        int y = 80;
        g.DrawLine(penRojo, 0, y, pgW, y);
        y += 2;
        using var penGris2 = new Pen(Color.FromArgb(200, 200, 200), 0.5f);
        g.DrawLine(penGris2, 0, y, pgW, y);
        y += 8;

        return y;
    }

    // ── Encabezado con logo para tickets térmicos (80/58/40mm) ────────────────
    // logotipocredimarticketera.png (pedido explícito 2026-08-10) es un logo panorámico que
    // ya incluye "Credimar S.A. / Electrodomésticos" dibujado en negro sobre fondo blanco —
    // a diferencia del logo del encabezado A4, acá NO se dibuja el nombre de empresa aparte
    // como texto: la imagen ya lo trae, y duplicarlo debajo se veía redundante. Se centra al
    // ancho del papel (limitado por w, no por una altura fija) porque es panorámico y el
    // texto interno se vuelve ilegible si se lo prioriza por alto en un papel angosto.
    // Devuelve la Y desde donde debe continuar el resto del ticket.
    private static int DibujarLogoYEncabezadoTicket(
        Graphics g, int x, int w, int y, string nombreEmpresa, Font fEmpresa)
    {
        string rutaLogo = _logoPathTicketera;
        try
        {
            if (File.Exists(rutaLogo))
            {
                using var img = Image.FromFile(rutaLogo);
                int logoAncho = Math.Min(w, (int)(w * 0.92f));
                int logoAlto  = (int)(img.Height * (logoAncho / (float)img.Width));
                int logoX = x + Math.Max(0, (w - logoAncho) / 2);
                g.DrawImage(img, logoX, y, logoAncho, logoAlto);
                y += logoAlto + 4;
            }
            else
            {
                DibujarIconoCasa(g, x + Math.Max(0, (w - 50) / 2), y - 2);
                y += 34 + 3;
                var sfFallback = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.None };
                var szFallback = g.MeasureString(nombreEmpresa.ToUpper(), fEmpresa, w, sfFallback);
                g.DrawString(nombreEmpresa.ToUpper(), fEmpresa, Brushes.Black,
                    new RectangleF(x, y, w, szFallback.Height + 2), sfFallback);
                y += (int)(szFallback.Height + 1);
            }
        }
        catch
        {
            var sf = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.None };
            var sz = g.MeasureString(nombreEmpresa.ToUpper(), fEmpresa, w, sf);
            g.DrawString(nombreEmpresa.ToUpper(), fEmpresa, Brushes.Black,
                new RectangleF(x, y, w, sz.Height + 2), sf);
            y += (int)(sz.Height + 1);
        }
        return y;
    }

    // Icono de casa vectorial (sin texto — el nombre empresa se dibuja aparte)
    private static void DibujarIconoCasa(Graphics g, int x, int y)
    {
        var azul = Color.FromArgb(30, 100, 200);
        var rojo = Color.FromArgb(204, 0, 0);
        using var bCirc   = new SolidBrush(azul);
        using var bBlanco = new SolidBrush(Color.White);
        using var bRojo   = new SolidBrush(rojo);
        using var bAzul2  = new SolidBrush(azul);

        g.FillEllipse(bCirc, x, y + 2, 50, 50);
        var techo = new System.Drawing.Point[] {
            new(x + 25, y + 8),
            new(x + 8,  y + 26),
            new(x + 42, y + 26),
        };
        g.FillPolygon(bBlanco, techo);
        g.FillRectangle(bBlanco, x + 14, y + 25, 22, 18);
        g.FillRectangle(bAzul2,  x + 22, y + 32,  6, 11);
        g.FillRectangle(bRojo,   x + 2,  y + 42,  8,  8);
    }

    // Mantener compatibilidad con llamadas sin nombreEmpresa/nombreLocal
    private static void DibujarLogoCasa(Graphics g, int x, int y, Color rojo)
        => DibujarIconoCasa(g, x, y);

    private static void DibujarMarcaAguaLateral(Graphics g, int pgW, string? logoPath) { }

    // ── Obtener formato desde BD ──────────────────────────────────────────────
    // 0 = A4 Normal, 1 = Ticket 58mm, 2 = Ticket 40mm, 3 = Ticket 80mm
    public static async Task<byte> ObtenerFormatoComprobanteAsync()
    {
        try
        {
            using var conn = App.Services.GetRequiredService<IDbConnectionFactory>().Create();
            var val = await conn.QueryFirstOrDefaultAsync<byte?>(
                "SELECT TOP 1 FORMATO_COMPROBANTE FROM IMPRESORAS WHERE NOMBRE_PC=@pc",
                new { pc = Environment.MachineName.ToUpper() });
            return val ?? 0;
        }
        catch { return 0; }
    }

    // Ancho del papel en milímetros según el formato guardado en BD.
    private static int AnchoMmFormato(byte fmt) => fmt switch { 1 => 58, 2 => 40, 3 => 80, _ => 210 };

    // Anchos en pixels a 96dpi:
    //   A4 portrait  = 794px  (210mm)
    //   Ticket 80mm  = 302px  (~80mm)
    //   Ticket 58mm  = 220px  (~58mm)
    //   Ticket 40mm  = 151px  (~40mm)
    private static int AnchoFormato(byte fmt) => fmt switch { 1 => 220, 2 => 151, 3 => 302, _ => 794 };

    // ── Configura el PrintDocument con el tamaño del papel correcto ───────────
    private static void ConfigurarPapel(PrintDocument doc, byte fmt)
    {
        if (fmt == 0) return; // A4 por defecto del sistema

        // Alto del papel de rollo: usamos 1200 pixels (suficiente para un ticket)
        int anchoPx  = AnchoFormato(fmt);
        int altoPx   = 1200;

        doc.DefaultPageSettings.PaperSize = new PaperSize("Ticket", anchoPx * 100 / 96, altoPx * 100 / 96);
        doc.DefaultPageSettings.Margins   = new Margins(5, 5, 5, 5);
    }

    // ── IMPRIMIR TICKET DE COBRO ──────────────────────────────────────────────
    public static async Task ImprimirCobroAsync(DatosTicketCobro d)
    {
        try
        {
            var (impresora, _) = await ObtenerImpresoraAsync("comprobante");
            var fmt = await ObtenerFormatoComprobanteAsync();
            var doc = new PrintDocument { DocumentName = "Comprobante_Cobro" };
            ConfigurarPapel(doc, fmt);
            doc.PrintPage += (_, e) => {
                e.HasMorePages = false;
                switch (fmt) {
                    // Ni el ancho lógico fijo (AnchoFormato=302) ni e.MarginBounds ni
                    // e.PageSettings.PrintableArea coinciden con lo que la Epson TM-T20IV/
                    // TM-220 (driver APD "Receipt") imprime realmente — los tres se probaron
                    // contra la impresora física y los tres seguían cortando texto por la
                    // derecha. Se midió el ancho REAL con una regla de calibración impresa
                    // directo en el rollo (marcas numeradas en las coordenadas crudas del
                    // Graphics, sin ninguna corrección): el usuario confirmó que la marca "260"
                    // se ve completa pero "270" ya empieza a cortarse — bastante menos que
                    // cualquiera de los valores que reportaba la API del driver. Se usa ese
                    // ancho medido empíricamente en vez de seguir confiando en el driver para
                    // esta impresora específica.
                    case 1:
                    case 2:
                    case 3:  DibujarCobroTicket(e.Graphics!, d, AnchoMmFormato(fmt), AnchoRealMedidoTicket80mm); break;
                    default: DibujarCobroA4(e.Graphics!, d);                          break;
                }
            };
            ImprimirConConfig(doc, impresora);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error al imprimir:\n{ex.Message}",
                "Impresión", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
    }

    public static void ImprimirCobro(DatosTicketCobro d) => _ = ImprimirCobroAsync(d);

    // ── IMPRIMIR TICKET DE VENTA ──────────────────────────────────────────────
    public static async Task ImprimirVentaAsync(DatosTicketVenta d)
    {
        try
        {
            var (impresora, _) = await ObtenerImpresoraAsync("comprobante");
            var fmt = await ObtenerFormatoComprobanteAsync();
            var doc = new PrintDocument { DocumentName = "Comprobante_Venta" };
            ConfigurarPapel(doc, fmt);
            doc.PrintPage += (_, e) => {
                e.HasMorePages = false;
                switch (fmt) {
                    // Ver comentario grande en ImprimirCobroAsync sobre el ancho medido.
                    case 1:
                    case 2:
                    case 3:  DibujarVentaTicket(e.Graphics!, d, AnchoMmFormato(fmt), AnchoRealMedidoTicket80mm); break;
                    default: DibujarVentaA4(e.Graphics!, d);                          break;
                }
            };
            ImprimirConConfig(doc, impresora);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error al imprimir:\n{ex.Message}",
                "Impresión", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
    }

    public static void ImprimirVenta(DatosTicketVenta d) => _ = ImprimirVentaAsync(d);

    // Ancho real (en las unidades crudas del Graphics de impresión — NO pixels a 96dpi, ver
    // AnchoFormato) medido empíricamente contra la Epson TM-T20IV/TM-220 real: se imprimió una
    // regla de calibración con marcas numeradas directo en el papel, sin ninguna corrección de
    // driver/margen de por medio, y el usuario confirmó con una regla física que "260" se veía
    // completo pero "270" ya se cortaba. El ancho lógico que se venía usando (302, calculado
    // como si el papel de 80mm fuera 302px @ 96dpi) y también e.MarginBounds/PrintableArea
    // (que en teoría deberían reflejar el hardware real) se probaron contra la misma impresora
    // y los tres seguían cortando texto — para ESTA impresora/driver ninguna de esas fuentes es
    // confiable, así que se usa directamente el valor medido con margen de seguridad incluido.
    // Si en el futuro se conecta una impresora térmica distinta y vuelve a cortar o queda con
    // demasiado margen sin usar, repetir la calibración (Program.cs de este mismo diagnóstico
    // queda documentado en el historial de la sesión que introdujo este fix).
    private const int AnchoRealMedidoTicket80mm = 260;

    // ── Número a letras (Guaraníes) ──────────────────────────────────────────
    private static string NumeroALetras(decimal n)
    {
        var entero = (long)n;
        string[] u  = {"","UNO","DOS","TRES","CUATRO","CINCO","SEIS","SIETE","OCHO","NUEVE",
                        "DIEZ","ONCE","DOCE","TRECE","CATORCE","QUINCE","DIECISÉIS","DIECISIETE",
                        "DIECIOCHO","DIECINUEVE"};
        string[] d2 = {"","","VEINTE","TREINTA","CUARENTA","CINCUENTA","SESENTA","SETENTA","OCHENTA","NOVENTA"};
        string[] c2 = {"","CIENTO","DOSCIENTOS","TRESCIENTOS","CUATROCIENTOS","QUINIENTOS",
                        "SEISCIENTOS","SETECIENTOS","OCHOCIENTOS","NOVECIENTOS"};

        string Decenas(long v)  { if (v < 20) return u[v]; var un = v%10; return un==0 ? d2[v/10] : d2[v/10]+" Y "+u[un]; }
        string Centenas(long v) { if (v==0) return ""; if (v==100) return "CIEN"; var r=c2[v/100]; var re=v%100; return re==0?r:(r+" "+Decenas(re)).Trim(); }
        string Miles(long v)    { if (v==0) return "CERO"; if (v<1000) return Centenas(v); var m=v/1000; var re=v%1000; var ms=m==1?"MIL":Centenas(m)+" MIL"; return re==0?ms:ms+" "+Centenas(re); }
        string Millones(long v) { if (v<1_000_000) return Miles(v); var m=v/1_000_000; var re=v%1_000_000; var ms=m==1?"UN MILLÓN":Miles(m)+" MILLONES"; return re==0?ms:ms+" "+Miles(re); }

        return Millones(entero) + " GUARANÍES";
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  FORMATO A4 — COBRO
    // ─────────────────────────────────────────────────────────────────────────
    internal static void DibujarCobroA4Public(Graphics g, DatosTicketCobro d) => DibujarCobroA4(g, d);
    private static void DibujarCobroA4(Graphics g, DatosTicketCobro d)
    {
        g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int pgW  = 794;   // A4 portrait
        int lx   = 20;
        int pw   = pgW - 40;
        int y    = DibujarEncabezadoProfesional(g, lx, pw, pgW,
                        "COMPROBANTE DE COBRO",
                        $"Fecha: {d.Fecha:dd/MM/yyyy HH:mm}  |  N° {d.NumeroTicket:D9}",
                        nombreEmpresa: d.NombreEmpresa, nombreLocal: d.NombreLocal, telefonoLocal: d.TelefonoLocal);

        // ── Colores corporativos ─────────────────────────────────────────────
        var azulOsc = Color.FromArgb(13, 71, 161);
        var azulMed = Color.FromArgb(21, 101, 192);
        var grisClr = Color.FromArgb(245, 245, 245);
        var negro   = Color.Black;
        var bAzul   = new SolidBrush(azulOsc);
        var bBlanco = new SolidBrush(Color.White);
        var bGris   = new SolidBrush(grisClr);
        var bNegro  = Brushes.Black;
        var bDkGray = new SolidBrush(Color.FromArgb(60, 60, 60));

        var fBold14 = new Font("Arial",  14, FontStyle.Bold);
        var fBold11 = new Font("Arial",  11, FontStyle.Bold);
        var fBold10 = new Font("Arial",  10, FontStyle.Bold);
        var fReg9   = new Font("Arial",   9, FontStyle.Regular);
        var fReg8   = new Font("Arial",   8, FontStyle.Regular);
        var fBold9  = new Font("Arial",   9, FontStyle.Bold);

        var sfL = new StringFormat { Alignment = StringAlignment.Near,   LineAlignment = StringAlignment.Center };
        var sfR = new StringFormat { Alignment = StringAlignment.Far,    LineAlignment = StringAlignment.Center };
        var sfC = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        // ── Cuadro datos del cliente / ticket ────────────────────────────────
        int cx = lx, cy = y;
        int halfW = pw / 2 - 5;
        int rh = 24;

        // Panel izquierdo: datos del ticket
        void CeldaA(string lbl, string val, int row)
        {
            int rx = cx, ry = cy + row * rh;
            g.FillRectangle(row % 2 == 0 ? bGris : bBlanco, rx, ry, halfW, rh);
            using var penBorde = new Pen(Color.FromArgb(200,200,200));
            g.DrawRectangle(penBorde, rx, ry, halfW, rh);
            g.DrawString(lbl, fBold9,  bAzul,  new RectangleF(rx+6,  ry, halfW/2-6,  rh), sfL);
            g.DrawString(val, fReg9,   bNegro, new RectangleF(rx+halfW/2, ry, halfW/2, rh), sfR);
        }

        // Panel derecho: datos del pago
        void CeldaB(string lbl, string val, int row, bool highlight = false)
        {
            int rx = cx + halfW + 10, ry = cy + row * rh;
            if (highlight) { g.FillRectangle(bAzul, rx, ry, halfW, rh); g.DrawString(lbl, fBold10, bBlanco, new RectangleF(rx+6, ry, halfW*0.55f, rh), sfL); g.DrawString(val, fBold11, bBlanco, new RectangleF(rx + halfW*0.55f, ry, halfW*0.45f-6, rh), sfR); return; }
            g.FillRectangle(row % 2 == 0 ? bGris : bBlanco, rx, ry, halfW, rh);
            using var penBorde = new Pen(Color.FromArgb(200,200,200));
            g.DrawRectangle(penBorde, rx, ry, halfW, rh);
            g.DrawString(lbl, fBold9, bAzul,   new RectangleF(rx+6, ry, halfW*0.55f, rh), sfL);
            g.DrawString(val, fReg9,  bNegro, new RectangleF(rx + halfW*0.55f, ry, halfW*0.45f-6, rh), sfR);
        }

        CeldaA("TICKET N°:",     d.NumeroTicket.ToString("D9"), 0);
        CeldaA("FECHA:",         d.Fecha.ToString("dd/MM/yyyy HH:mm"), 1);
        CeldaA("COMPROBANTE:",   d.Comprobante, 2);
        CeldaA("CAJERO:",        d.Cajero, 3);

        // Total que correspondía pagar por esta cuota (capital + punitorio + reajuste) —
        // referencia para que el cliente vea contra qué se calculó el saldo pendiente.
        var totalCuota = d.EntregaCapital + d.Punitorio + d.Reajuste;

        CeldaB("ENTREGA CAPITAL:", d.EntregaCapital.ToString("N0")+" Gs.", 0);
        CeldaB("DÍAS ATRASO:",     d.DiasAtraso.ToString()+" días",         1);
        CeldaB("PUNITORIO:",       d.Punitorio.ToString("N0")+" Gs.",       2);
        int filaExtra = 3;
        if (d.Reajuste != 0)    { CeldaB("REAJUSTE:",  d.Reajuste.ToString("N0")+" Gs.",       filaExtra); filaExtra++; }
        if (d.EntregaAnterior!=0) { CeldaB("ENT. ANTERIOR:", d.EntregaAnterior.ToString("N0")+" Gs.", filaExtra); filaExtra++; }
        CeldaB("TOTAL CUOTA:", totalCuota.ToString("N0")+" Gs.", filaExtra);
        filaExtra++;

        y = cy + Math.Max(4, filaExtra) * rh + 10;

        // ── Fila cliente (solo contorno + texto negro, sin fondo — ahorra tinta) ────────
        using (var penCliente = new Pen(negro, 1.4f))
            g.DrawRectangle(penCliente, lx, y, pw, 30);
        g.DrawString("CLIENTE:", fBold10, bNegro, new RectangleF(lx+8, y, 90, 30), sfL);
        g.DrawString(d.NombreCliente.ToUpper(), fBold10, bNegro, new RectangleF(lx+100, y, pw/2-100, 30), sfL);
        g.DrawString($"CONCEPTO: {d.Concepto} — CUOTA Nro. {d.NCuota}", fBold9, bNegro,
            new RectangleF(lx+pw/2, y, pw/2-4, 30), sfR);
        y += 36;

        // Caja con solo contorno + texto negro (no fondo sólido de color) — ahorra tinta al
        // imprimir, a diferencia del resto del ticket que sí usa fondos de color.
        void CajaContorno(string lbl, string val, ref int yy, Font? fVal = null)
        {
            using var pen = new Pen(negro, 1.4f);
            g.DrawRectangle(pen, lx, yy, pw, 44);
            g.DrawString(lbl, fBold11, bNegro, new RectangleF(lx+10, yy, pw/2, 44), sfL);
            g.DrawString(val, fVal ?? new Font("Arial",16,FontStyle.Bold), bNegro,
                new RectangleF(lx+pw/2, yy, pw/2-6, 44), sfR);
            yy += 50;
        }

        // ── TOTAL PAGADO (caja con contorno) ──────────────────────────────────
        CajaContorno("TOTAL PAGADO:", d.TotalPagado.ToString("N0")+" Gs.", ref y);

        // Si el cliente entregó más que lo que se cobró (vuelto en EFECTIVO), se muestra el
        // desglose completo — más práctico que solo ver "total pagado" sin saber cuánto
        // entregó en mano ni cuánto se le devolvió.
        if (d.Vuelto > 0)
        {
            CajaContorno("TOTAL ENTREGADO:", d.EfectivoRecibido.ToString("N0")+" Gs.", ref y);
            CajaContorno("VUELTO:", d.Vuelto.ToString("N0")+" Gs.", ref y);
        }

        // ── SALDO PENDIENTE / CUOTA CANCELADA (caja con contorno) ────────────
        // Contra el total que correspondía pagar (capital+punitorio+reajuste), descontando
        // entregas previas de este mismo episodio y lo pagado hoy — para que el cliente vea
        // de un vistazo si la cuota quedó saldada o cuánto le queda.
        var saldoPendiente = Math.Max(0, totalCuota - d.EntregaAnterior - d.TotalPagado);
        var cuotaCancelada = saldoPendiente == 0;
        if (cuotaCancelada)
        {
            using var pen = new Pen(negro, 1.4f);
            g.DrawRectangle(pen, lx, y, pw, 44);
            g.DrawString("CUOTA CANCELADA", fBold11, bNegro, new RectangleF(lx, y, pw, 44), sfC);
            y += 50;
        }
        else
        {
            CajaContorno("SALDO PENDIENTE:", saldoPendiente.ToString("N0")+" Gs.", ref y);
        }

        // Monto en letras
        try { g.DrawString("Son: "+NumeroALetras(d.TotalPagado), fReg8, bDkGray,
            new RectangleF(lx, y, pw, 18), sfC); } catch {}
        y += 22;

        // ── Método de pago + Timbrado ─────────────────────────────────────────
        g.FillRectangle(new SolidBrush(grisClr), lx, y, pw, 26);
        using var penBorde2 = new Pen(Color.FromArgb(200,200,200));
        g.DrawRectangle(penBorde2, lx, y, pw, 26);
        g.DrawString("MÉTODO DE PAGO:", fBold9, bAzul, new RectangleF(lx+8, y, 140, 26), sfL);
        g.DrawString(d.MetodoPago, fReg9, bNegro, new RectangleF(lx+148, y, pw/2, 26), sfL);
        if (!string.IsNullOrWhiteSpace(d.Timbrado)) {
            g.DrawString($"TIMBRADO: {d.Timbrado}  VIG: {d.VigenciaDesde}–{d.VigenciaHasta}",
                fReg8, bDkGray, new RectangleF(lx+pw/2, y, pw/2-4, 26), sfR);
        }
        y += 32;

        // ── Pie ───────────────────────────────────────────────────────────────
        using var penRojo2 = new Pen(Color.FromArgb(204,0,0), 1.5f);
        g.DrawLine(penRojo2, lx, y, lx+pw, y);
        y += 6;
        g.DrawString("Gracias por su pago",
            fReg8, bDkGray, new RectangleF(lx, y, pw, 18), sfC);

        foreach (var obj in new object[]{fBold14,fBold11,fBold10,fReg9,fReg8,fBold9,
            sfL,sfR,sfC,bAzul,bBlanco,bGris,bDkGray}) (obj as IDisposable)?.Dispose();
        _ = negro;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  FORMATO TICKET (80mm, 58mm o 40mm) — COBRO
    //  anchoOverride: cuando viene de preview pasa el ancho real del canvas
    // ─────────────────────────────────────────────────────────────────────────
    internal static void DibujarCobroTicketPublic(Graphics g, DatosTicketCobro d, int anchoMm, int anchoOverride = 0) => DibujarCobroTicket(g, d, anchoMm, anchoOverride);
    private static void DibujarCobroTicket(Graphics g, DatosTicketCobro d, int anchoMm, int anchoOverride = 0)
    {
        // anchoOverride se usa en DOS casos con intención distinta pero mismo mecanismo:
        // (a) preview en pantalla, donde el canvas es más grande que el papel real y las
        //     fuentes se escalan hacia arriba para aprovechar la resolución extra;
        // (b) impresión física (ver ImprimirCobroAsync), donde se pasa el ancho REAL que
        //     PrintableArea reporta para el hardware — normalmente algo más angosto que el
        //     ancho lógico asumido (anchoBase), así que el escalado de fuentes de abajo achica
        //     levemente en vez de agrandar (Math.Min ya evita que agrande de más). Sin esto, el
        //     ancho de dibujo (w) se ajustaba al área real pero el tamaño de fuente seguía
        //     calculado para el ancho lógico más grande, dejando menos margen de seguridad del
        //     necesario.
        int  anchoBase  = anchoMm switch { 80 => 300, 40 => 145, _ => 210 };
        int  ancho      = anchoOverride > 0 ? anchoOverride : anchoBase;
        int  margen     = 6;
        int  x          = margen;
        int  w          = ancho - margen * 2;   // ancho útil para dibujar
        int  y          = 4;

        float fTitle = anchoMm switch { 80 => 9.5f, 40 => 7f,   _ => 8.5f };
        float fNorm  = anchoMm switch { 80 => 8.5f, 40 => 6.5f, _ => 7.5f };
        float fSmall = anchoMm switch { 80 => 8f,   40 => 6f,   _ => 7f   };
        float fBig   = anchoMm switch { 80 => 11f,  40 => 8f,   _ => 9.5f };
        if (anchoOverride > 0) {
            float k = ancho / (float)anchoBase;
            fTitle = Math.Min(12f, fTitle * k);
            fNorm  = Math.Min(11f, fNorm  * k);
            fSmall = Math.Min(10f, fSmall * k);
            fBig   = Math.Min(15f, fBig   * k);
        }

        var fB    = new Font("Courier New", fTitle, FontStyle.Bold);
        var fR    = new Font("Courier New", fNorm,  FontStyle.Regular);
        var fS    = new Font("Courier New", fSmall, FontStyle.Regular);
        var fBig1 = new Font("Courier New", fBig,   FontStyle.Bold);

        // ── Helpers ──────────────────────────────────────────────────────────
        void Linea(string txt, Font f, bool centrar = false)
        {
            float lh = g.MeasureString("Ag", f).Height;
            // Dibuja con wrap automático dentro del ancho w
            var rf = new RectangleF(x, y, w, lh * 3);
            var sf = new StringFormat { Alignment = centrar ? StringAlignment.Center : StringAlignment.Near,
                                        Trimming = StringTrimming.None };
            // Medir cuántas líneas ocupa
            var sz = g.MeasureString(txt, f, (int)w, sf);
            g.DrawString(txt, f, Brushes.Black, new RectangleF(x, y, w, sz.Height + 2), sf);
            y += (int)(sz.Height + 1);
        }

        void Sep() { g.DrawLine(Pens.Black, x, y + 1, x + w, y + 1); y += 4; }

        // Par: label izquierda, valor derecha. Si no entra en una línea, el valor va debajo
        // con wrap real (RectangleF + StringFormat) — antes se dibujaba con DrawString(x,y)
        // suelto sin límite de ancho, así que un valor más ancho que el papel (ej. "Aida
        // Acosta" con la fuente/ancho de este ticket) se salía del área imprimible y quedaba
        // "cortado" en la impresión real en vez de hacer salto de línea.
        //
        // El branch "caben juntos" seguía teniendo ese mismo bug pese al comentario de arriba:
        // decidía que val cabía usando MeasureString(val,fv) SIN StringFormat, pero dibujaba
        // con DrawString(x,y) suelto — GDI+ mide texto sin StringFormat con un ancho ligeramente
        // distinto al que realmente ocupa al dibujarse (padding de line-breaking incluido en la
        // medición), así que un valor que el guard consideraba "cabe justo" (ej. "Aida Acosta",
        // "0000000001", "EFECTIVO") terminaba dibujándose unos píxeles más ancho que lo medido y
        // se cortaba en el borde físico del papel — confirmado en producción: "Aida Acost",
        // ticket "0000000" y "Efectiv" cortados, todos casi en el límite exacto del guard. Fix:
        // usar SIEMPRE RectangleF + StringFormat para dibujar (como ya hacía el branch de abajo),
        // nunca la sobrecarga DrawString(x,y) sin rectángulo — así medición y render usan
        // exactamente el mismo ancho.
        void Par(string lbl, string val, Font fl, Font fv)
        {
            var szL = g.MeasureString(lbl, fl);
            var sfVR = new StringFormat { Alignment = StringAlignment.Far, Trimming = StringTrimming.None };
            var szV = g.MeasureString(val, fv, (int)w, sfVR);
            float lineH = Math.Max(szL.Height, szV.Height);

            // ¿Caben juntos en el ancho? (medición ya acotada a w, igual que el render)
            if (szL.Width + szV.Width + 4 <= w) {
                g.DrawString(lbl, fl, Brushes.Black, x, y);
                g.DrawString(val, fv, Brushes.Black, new RectangleF(x, y, w, szV.Height + 2), sfVR);
                y += (int)lineH + 1;
            } else {
                // No caben: label + valor en dos líneas, valor con wrap dentro del ancho
                // disponible (alineado a la derecha, puede ocupar más de una línea).
                g.DrawString(lbl, fl, Brushes.Black, x, y);
                y += (int)szL.Height;
                var sfVal = new StringFormat { Alignment = StringAlignment.Far, Trimming = StringTrimming.None };
                var szWrap = g.MeasureString(val, fv, (int)w, sfVal);
                g.DrawString(val, fv, Brushes.Black, new RectangleF(x, y, w, szWrap.Height + 2), sfVal);
                y += (int)szWrap.Height + 1;
            }
        }

        // ── Encabezado ───────────────────────────────────────────────────────
        // Separador superior
        g.DrawLine(Pens.Black, x, y + 1, x + w, y + 1); y += 4;

        // Logo + nombre empresa en grande y negrita centrado — mismo diseño que el
        // encabezado A4 (logo arriba, nombre debajo), adaptado a blanco y negro y ancho
        // angosto de papel térmico.
        y = DibujarLogoYEncabezadoTicket(g, x, w, y, d.NombreEmpresa, fBig1);

        // LOCAL: N° debajo
        if (!string.IsNullOrWhiteSpace(d.NombreLocal))
            Linea(d.NombreLocal.ToUpper(), fS, centrar: true);
        // Teléfono del local (LOCALES.TELEFONO) — solo si está cargado, mismo criterio que
        // el encabezado A4.
        if (!string.IsNullOrWhiteSpace(d.TelefonoLocal))
            Linea($"Tel: {d.TelefonoLocal}", fS, centrar: true);

        Sep();

        Par("FECHA:",        d.Fecha.ToString("dd/MM/yy HH:mm"), fS, fS);
        Par("TICKET:",       d.NumeroTicket.ToString("D9"),       fS, fS);
        Par("COMPROBANTE:",  d.Comprobante,                       fS, fS);
        if (!string.IsNullOrWhiteSpace(d.Cajero)) Par("CAJERO:", d.Cajero, fS, fS);
        Sep();

        // "CLIENTE:" explícito antes del nombre — antes el nombre aparecía solo, sin
        // etiqueta, ambiguo con la línea de CAJERO justo arriba (pedido explícito 2026-08-03).
        Par("CLIENTE:", d.NombreCliente, fS, fS);
        Sep();

        Linea("COBRO DE CUOTA",          fB, centrar: true);
        Linea($"Concepto: {d.Concepto}", fS);
        Linea($"Cuota N° {d.NCuota}",    fS);
        Sep();

        Par("Capital:",     d.EntregaCapital.ToString("N0") + " Gs.", fS, fS);
        Par("Mora:",        d.DiasAtraso + " días",                    fS, fS);
        Par("Punitorio:",   d.Punitorio.ToString("N0")  + " Gs.",     fS, fS);
        if (d.Reajuste      != 0) Par("Reajuste:",      d.Reajuste.ToString("N0")      + " Gs.", fS, fS);
        if (d.EntregaAnterior != 0) Par("Ent. Anterior:", d.EntregaAnterior.ToString("N0") + " Gs.", fS, fS);
        Sep();

        Linea("TOTAL PAGADO:",                    fB,    centrar: true);
        Linea(d.TotalPagado.ToString("N0") + " Gs.", fBig1, centrar: true);
        try { Linea("Son: " + NumeroALetras(d.TotalPagado), fS, centrar: true); } catch { }
        Sep();

        Par("MÉTODO:", d.MetodoPago, fS, fR);
        Sep();

        if (!string.IsNullOrWhiteSpace(d.Timbrado)) {
            Linea($"Timbrado: {d.Timbrado}", fS, centrar: true);
            if (!string.IsNullOrWhiteSpace(d.VigenciaDesde))
                Linea($"Vig: {d.VigenciaDesde} al {d.VigenciaHasta}", fS, centrar: true);
            Sep();
        }

        Linea("¡Gracias por su pago!",    fB, centrar: true);
        Linea("Conserve este comprobante", fS, centrar: true);
        y += 8;

        foreach (var f in new[] { fB, fR, fS, fBig1 }) f.Dispose();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  FORMATO A4 — VENTA
    // ─────────────────────────────────────────────────────────────────────────
    internal static void DibujarVentaA4Public(Graphics g, DatosTicketVenta d) => DibujarVentaA4(g, d);
    private static void DibujarVentaA4(Graphics g, DatosTicketVenta d)
    {
        g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int pgW = 794; int lx = 20; int pw = pgW - 40;
        var titulo = d.EsContado ? "COMPROBANTE DE VENTA AL CONTADO" : "SOLICITUD DE VENTA A CRÉDITO";
        var refEtiqueta = d.EsContado ? "N° Venta" : "Solicitud";
        int y   = DibujarEncabezadoProfesional(g, lx, pw, pgW,
                        titulo,
                        $"Fecha: {d.Fecha:dd/MM/yyyy HH:mm}  |  {refEtiqueta}: {d.NroSolicitud}",
                        nombreEmpresa: d.NombreEmpresa, nombreLocal: d.NombreLocal, telefonoLocal: d.TelefonoLocal);

        var azulOsc = Color.FromArgb(13, 71, 161);
        var azulMed = Color.FromArgb(21, 101, 192);
        var grisClr = Color.FromArgb(245, 245, 245);
        var bAzul   = new SolidBrush(azulOsc);
        var bBlanco = new SolidBrush(Color.White);
        var bGris   = new SolidBrush(grisClr);
        var bNegro  = Brushes.Black;
        var bDkGray = new SolidBrush(Color.FromArgb(60, 60, 60));

        var fBold11 = new Font("Arial", 11, FontStyle.Bold);
        var fBold10 = new Font("Arial", 10, FontStyle.Bold);
        var fBold9  = new Font("Arial",  9, FontStyle.Bold);
        var fReg9   = new Font("Arial",  9, FontStyle.Regular);
        var fReg8   = new Font("Arial",  8, FontStyle.Regular);
        var fReg7   = new Font("Arial",  7, FontStyle.Regular);

        var sfL = new StringFormat { Alignment = StringAlignment.Near,   LineAlignment = StringAlignment.Center };
        var sfR = new StringFormat { Alignment = StringAlignment.Far,    LineAlignment = StringAlignment.Center };
        var sfC = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        int rh = 24;

        // ── Fila cliente ─────────────────────────────────────────────────────
        g.FillRectangle(new SolidBrush(azulMed), lx, y, pw, 30);
        g.DrawString("CLIENTE:", fBold10, bBlanco, new RectangleF(lx+8, y, 80, 30), sfL);
        g.DrawString(d.NombreCliente.ToUpper(), fBold10, bBlanco, new RectangleF(lx+90, y, pw/2-90, 30), sfL);
        g.DrawString($"VENDEDOR: {d.Vendedor}", fBold9, bBlanco, new RectangleF(lx+pw/2, y, pw/2-4, 30), sfR);
        y += 36;

        // ── Tabla artículos ───────────────────────────────────────────────────
        int[] colW = { 60, pw-60-90-90-90, 90, 90, 90 };  // # | Desc | Precio | Entrega | SubTotal
        string[] hdrs = { "N°", "Descripción", "Precio", "Entrega", "SubTotal" };
        int[] colX = new int[5];
        colX[0] = lx;
        for (int i=1;i<5;i++) colX[i] = colX[i-1]+colW[i-1];

        // Cabecera tabla
        g.FillRectangle(bAzul, lx, y, pw, rh);
        for (int i=0;i<5;i++)
            g.DrawString(hdrs[i], fBold9, bBlanco, new RectangleF(colX[i]+2, y, colW[i]-4, rh), i<2?sfL:sfR);
        y += rh;

        int row = 0;
        foreach (var art in d.Articulos)
        {
            g.FillRectangle(row++%2==0 ? bGris : bBlanco, lx, y, pw, rh);
            using var penG = new Pen(Color.FromArgb(220,220,220));
            g.DrawLine(penG, lx, y, lx+pw, y);

            var desc = art.Descripcion.Length > 55 ? art.Descripcion[..55] : art.Descripcion;
            string[] vals = { art.Cantidad.ToString("0.##"), desc,
                art.Precio.ToString("N0"), art.Entrega.ToString("N0"),
                (art.Cantidad * art.Precio).ToString("N0") };
            for (int i=0;i<5;i++)
                g.DrawString(vals[i], fReg8, bNegro, new RectangleF(colX[i]+2, y, colW[i]-4, rh), i<2?sfL:sfR);
            y += rh;
        }
        using var penBorde3 = new Pen(Color.FromArgb(180,180,180));
        g.DrawRectangle(penBorde3, lx, y-d.Articulos.Count*rh-rh, pw, d.Articulos.Count*rh+rh);
        y += 8;

        // ── Resumen de venta ──────────────────────────────────────────────────
        int halfW = pw/2 - 5;
        int sumX  = lx + halfW + 10;

        void Resumen(string lbl, string val, bool total=false) {
            if (total) g.FillRectangle(bAzul, sumX, y, halfW, rh);
            else       g.FillRectangle(y/rh%2==0?bGris:bBlanco, sumX, y, halfW, rh);
            using var pb = new Pen(Color.FromArgb(200,200,200));
            g.DrawRectangle(pb, sumX, y, halfW, rh);
            var fb = total ? fBold10 : fBold9;
            var fv = total ? new Font("Arial",11,FontStyle.Bold) : fReg9;
            var br = total ? bBlanco : (Brush)bNegro;
            g.DrawString(lbl, fb, total?bBlanco:bAzul, new RectangleF(sumX+6, y, halfW*0.5f, rh), sfL);
            g.DrawString(val, fv, br, new RectangleF(sumX+halfW*0.5f, y, halfW*0.5f-6, rh), sfR);
            if (total) fv.Dispose();
            y += rh;
        }

        if (d.EsContado)
        {
            Resumen("TOTAL A PAGAR:", d.TotalVenta.ToString("N0")+" Gs.", total:true);
            y += 6;
            try { g.DrawString("Son: " + NumeroALetras(d.TotalVenta), fReg8, bDkGray,
                new RectangleF(lx, y, pw, 18), sfC); } catch { }
            y += 4;
        }
        else
        {
            Resumen("TOTAL VENTA:",      d.TotalVenta.ToString("N0")+" Gs.");
            Resumen("TOTAL ENTREGA:",    d.TotalEntrega.ToString("N0")+" Gs.");
            Resumen("TOTAL C/ INTERÉS:", d.TotalConInteres.ToString("N0")+" Gs.");
            Resumen("CANT. CUOTAS:",     d.CantCuotas.ToString()+" cuotas");
            Resumen("COSTO CUOTA:",      d.CostoCuota.ToString("N0")+" Gs.");
            Resumen("ENTREGA APROBADA:", d.TotalEntrega.ToString("N0")+" Gs.", total:true);
        }
        y += 8;

        // ── Timbrado ─────────────────────────────────────────────────────────
        // Sin bloque de firma: este comprobante es un recibo/factura, no un pagaré — no hay
        // razón para pedir firma de cliente/vendedor acá (pedido explícito 2026-08-10). El
        // pagaré real de la venta a crédito se firma aparte (ver "N° de Pagaré" en el modal
        // de aprobación de la solicitud), no en este comprobante impreso.
        if (!string.IsNullOrWhiteSpace(d.Timbrado)) {
            g.DrawString($"Timbrado: {d.Timbrado}  Vigencia: {d.VigenciaDesde} al {d.VigenciaHasta}",
                fReg8, bDkGray, new RectangleF(lx, y, pw, 18), sfC);
            y += 22;
        }

        foreach (var obj in new object[]{fBold11,fBold10,fBold9,fReg9,fReg8,fReg7,
            sfL,sfR,sfC,bAzul,bBlanco,bGris,bDkGray}) (obj as IDisposable)?.Dispose();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  FORMATO TICKET (80mm, 58mm o 40mm) — VENTA
    // ─────────────────────────────────────────────────────────────────────────
    internal static void DibujarVentaTicketPublic(Graphics g, DatosTicketVenta d, int anchoMm, int anchoOverride = 0) => DibujarVentaTicket(g, d, anchoMm, anchoOverride);
    private static void DibujarVentaTicket(Graphics g, DatosTicketVenta d, int anchoMm, int anchoOverride = 0)
    {
        // anchoOverride: al imprimir de verdad (no en preview) se pasa PrintableArea.Width, el
        // ancho real del área imprimible que el DRIVER FÍSICO reporta (ver ImprimirVentaAsync y
        // el comentario grande en ImprimirCobroAsync) en vez de asumir un valor fijo — el ancho
        // lógico (AnchoFormato) no coincide siempre con lo que la impresora térmica puede usar.
        int ancho = anchoOverride > 0 ? anchoOverride : anchoMm switch { 80 => 300, 40 => 145, _ => 210 };
        int x = 4, w = ancho - 8;
        int y = 4;

        float fTitle = anchoMm switch { 80 => 9.5f, 40 => 7f,   _ => 8.5f };
        float fNorm  = anchoMm switch { 80 => 8.5f, 40 => 6.5f, _ => 7.5f };
        float fSmall = anchoMm switch { 80 => 8f,   40 => 6f,   _ => 7f   };
        float fBig   = anchoMm switch { 80 => 11f,  40 => 8f,   _ => 9.5f };

        var fB    = new Font("Courier New", fTitle, FontStyle.Bold);
        var fR    = new Font("Courier New", fNorm,  FontStyle.Regular);
        var fS    = new Font("Courier New", fSmall, FontStyle.Regular);
        var fBig1 = new Font("Courier New", fBig,   FontStyle.Bold);

        // Mismo fix ya aplicado en el Linea() del ticket de COBRO (ver arriba, ~línea 633):
        // esta versión anterior media el texto SIN límite de ancho (MeasureString(txt,f) sin
        // rectángulo) y lo dibujaba con DrawString(txt,f,brush,x,y) — la sobrecarga sin
        // RectangleF, que no recorta ni ajusta nada. Con un monto grande centrado en fuente
        // grande (ej. "13.925.000 Gs." en fBig1 para ENTREGA APROBADA), el texto medido podía
        // ser más ancho que el papel térmico (w): el cálculo de centrado daba una posición
        // clampeada al margen izquierdo, pero como no había rectángulo limitando el ancho,
        // GDI+ igual dibujaba el texto a su ancho natural completo, extendiéndose más allá del
        // área imprimible física — la impresora térmica cortaba lo que quedaba fuera del rollo
        // (el caso real reportado: "Gs" cortado a la mitad) y algunas reintentaban imprimiendo
        // el ticket completo de nuevo al recibir contenido fuera de rango.
        void Linea(string txt, Font f, bool centrar = false)
        {
            var sf = new StringFormat { Alignment = centrar ? StringAlignment.Center : StringAlignment.Near,
                                        Trimming = StringTrimming.None };
            var sz = g.MeasureString(txt, f, (int)w, sf);
            g.DrawString(txt, f, Brushes.Black, new RectangleF(x, y, w, sz.Height + 2), sf);
            y += (int)(sz.Height + 1);
        }

        void Sep() { g.DrawLine(Pens.Black, x, y+1, w+x, y+1); y += 4; }

        // Igual criterio que el Par() del ticket de cobro: si label+valor no entran juntos en
        // el ancho del papel, el valor pasa a su propia línea con wrap real — antes se dibujaba
        // siempre en una sola línea sin límite de ancho, así que un valor largo (ej. nombre de
        // vendedor/cliente) se salía del área imprimible y quedaba cortado en la impresión real.
        //
        // Mismo bug de medición que en el Par() del ticket de cobro (ver comentario grande ahí):
        // el branch "caben juntos" medía val SIN StringFormat/ancho acotado pero dibujaba con
        // DrawString(x,y) suelto — la medición y el render usaban rutas de GDI+ ligeramente
        // distintas, así que un valor que el guard aprobaba como "cabe" terminaba dibujándose
        // más ancho de lo medido y se cortaba en el borde del papel. Fix: medir y dibujar SIEMPRE
        // con el mismo RectangleF/StringFormat acotado a w, nunca DrawString(x,y) sin rectángulo.
        void Par(string lbl, string val, Font fl, Font fv)
        {
            var szL = g.MeasureString(lbl, fl);
            var sfVR = new StringFormat { Alignment = StringAlignment.Far, Trimming = StringTrimming.None };
            var szV = g.MeasureString(val, fv, (int)w, sfVR);
            if (szL.Width + szV.Width + 4 <= w) {
                g.DrawString(lbl, fl, Brushes.Black, x, y);
                g.DrawString(val, fv, Brushes.Black, new RectangleF(x, y, w, szV.Height + 2), sfVR);
                y += (int)Math.Max(szL.Height, szV.Height) + 1;
            } else {
                g.DrawString(lbl, fl, Brushes.Black, x, y);
                y += (int)szL.Height;
                var sfVal = new StringFormat { Alignment = StringAlignment.Far, Trimming = StringTrimming.None };
                var szWrap = g.MeasureString(val, fv, (int)w, sfVal);
                g.DrawString(val, fv, Brushes.Black, new RectangleF(x, y, w, szWrap.Height + 2), sfVal);
                y += (int)szWrap.Height + 1;
            }
        }

        g.DrawLine(Pens.Black, x, y + 1, x + w, y + 1); y += 4;
        // Logo + nombre empresa — mismo encabezado que el ticket de cobro (ver
        // DibujarLogoYEncabezadoTicket), cubre tanto venta contado como crédito ya que esta
        // línea corre antes del if (d.EsContado) de más abajo.
        y = DibujarLogoYEncabezadoTicket(g, x, w, y, d.NombreEmpresa, fBig1);
        Sep();

        if (d.EsContado)
        {
            // Formato compacto, alineado al comprobante de referencia del negocio: N° de
            // comprobante con ceros a la izquierda (mismo estilo que el resto de la app),
            // fecha, local y vendedor en líneas simples — sin separar en bloques Par() de
            // a dos columnas, que en 58mm quedaba más espaciado de lo necesario.
            Linea($"Comprobante: {d.NroSolicitud.PadLeft(9, '0')}", fS);
            Linea($"Fecha: {d.Fecha:dd/MM/yyyy HH:mm}", fS);
            Linea($"Local: {d.NombreLocal}", fS);
            if (!string.IsNullOrWhiteSpace(d.TelefonoLocal))
                Linea($"Tel: {d.TelefonoLocal}", fS);
            Linea($"Vendedor: {d.Vendedor}", fS);
            Sep();

            Linea($"Cliente: {d.NombreCliente}", fS);
            Sep();

            Linea("Descripcion", fS);
            Par("Cant.", "Subtotal", fS, fS);
            Sep();
            int maxDescCont = anchoMm switch { 80 => 34, 40 => 16, _ => 22 };
            foreach (var art in d.Articulos)
            {
                var desc = art.Descripcion.Length > maxDescCont ? art.Descripcion[..maxDescCont] : art.Descripcion;
                Linea(desc, fS);
                Par(art.Cantidad.ToString("0.##"), (art.Cantidad * art.Precio).ToString("N0"), fS, fS);
            }
            Sep();

            Par("TOTAL Gs.:", d.TotalVenta.ToString("N0"), fB, fB);
            try { Linea("SON: " + NumeroALetras(d.TotalVenta), fS); } catch { }
            Sep();

            Linea("Metodo: EFECTIVO", fS);
            y += 6;
            Linea("¡Gracias por su compra!", fS, centrar: true);

            foreach (var f in new[] { fB, fR, fS, fBig1 }) f.Dispose();
            return;
        }

        if (!string.IsNullOrWhiteSpace(d.NombreLocal))
            Linea(d.NombreLocal.ToUpper(), fS, centrar: true);
        if (!string.IsNullOrWhiteSpace(d.TelefonoLocal))
            Linea($"Tel: {d.TelefonoLocal}", fS, centrar: true);
        Sep();

        Par("FECHA:",  d.Fecha.ToString("dd/MM/yy HH:mm"), fS, fS);
        Par("TICKET:", d.NumeroTicket.ToString("D9"),       fS, fS);
        Par("SOLICITUD:", d.NroSolicitud, fS, fS);
        Sep();

        Linea("SOLICITUD APROBADA", fB, centrar: true);
        Par("Cliente:",  d.NombreCliente, fS, fS);
        Par("Vendedor:", d.Vendedor,      fS, fS);
        Sep();

        // Artículos
        int maxDesc = anchoMm switch { 80 => 28, 40 => 12, _ => 18 };
        foreach (var art in d.Articulos) {
            var desc = art.Descripcion.Length > maxDesc ? art.Descripcion[..maxDesc] : art.Descripcion;
            Linea($"{art.Cantidad:0.##}x {desc}", fS);
            Par("  Precio:", art.Precio.ToString("N0")+" Gs.",  fS, fS);
            Par("  Entrega:", art.Entrega.ToString("N0")+" Gs.", fS, fS);
        }
        Sep();

        // Desglose explícito para evitar la confusión real reportada: "Cuotas: 2" sin más
        // contexto se leía como "2 cuotas en total", cuando en realidad son las cuotas
        // restantes DESPUÉS de la entrega (mismo criterio que Cuota.NCuotaTexto — "Cuota 1
        // (Entrega)" — ya usado en pantalla). Total de pagos = 1 (entrega) + CantCuotas.
        Par("Total venta:",          d.TotalVenta.ToString("N0")+" Gs.",   fS, fS);
        Par("Cuota 1 (Entrega):",    d.TotalEntrega.ToString("N0")+" Gs.", fS, fS);
        Par("Cuotas restantes:",     d.CantCuotas.ToString(),               fS, fS);
        Par("Costo c/cuota:",        d.CostoCuota.ToString("N0")+" Gs.",   fS, fS);
        Par("Total de pagos:",       (1 + d.CantCuotas).ToString() + " (entrega + cuotas)", fS, fS);
        Sep();

        Linea("ENTREGA APROBADA:", fB, centrar: true);
        Linea(d.TotalEntrega.ToString("N0")+" Gs.", fBig1, centrar: true);
        Sep();

        if (!string.IsNullOrWhiteSpace(d.Timbrado)) {
            Linea($"Timbrado: {d.Timbrado}", fS, centrar: true);
            if (!string.IsNullOrWhiteSpace(d.VigenciaDesde))
                Linea($"Vig: {d.VigenciaDesde} al {d.VigenciaHasta}", fS, centrar: true);
            Sep();
        }

        Linea("¡Gracias por su compra!", fB, centrar: true);
        y += 8;

        foreach (var f in new[]{fB,fR,fS,fBig1}) f.Dispose();
    }
}
