using CrediSoft.Core.Models;

namespace CrediSoft.UI.Views.Maestros;

public class ClienteFichaData
{
    public required Cliente    Cliente   { get; init; }
    public required string     LocalNom  { get; init; }   // nombre del local
    public required string     LogoPath  { get; init; }
    public required string     Usuario   { get; init; }
    public System.Drawing.Image? FotoDoc { get; init; }   // imagen CI (puede ser null)
}

public static class ClienteFichaDibujador
{
    private const int PgW      = 827;
    private const int PgH      = 1169;
    private const int Mx       = 28;   // margen lateral
    private const int Pw       = PgW - Mx * 2;

    // Fuentes
    private static readonly System.Drawing.Font FntTit  = new("Arial", 12f,  System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntSub  = new("Arial",  7f,  System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntSec  = new("Arial",  8f,  System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntLbl  = new("Arial",  7.5f,System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntVal  = new("Arial",  8f,  System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntFoot = new("Arial",  7f,  System.Drawing.FontStyle.Regular);

    // Colores
    private static readonly System.Drawing.SolidBrush BNaranja  = new(System.Drawing.Color.FromArgb(230, 81,   0));
    private static readonly System.Drawing.SolidBrush BRojo     = new(System.Drawing.Color.FromArgb(210,  0,   0));
    private static readonly System.Drawing.SolidBrush BSubTxt   = new(System.Drawing.Color.FromArgb( 80, 80, 100));
    private static readonly System.Drawing.SolidBrush BSubBg    = new(System.Drawing.Color.FromArgb(245,245, 248));
    private static readonly System.Drawing.SolidBrush BSecBg    = new(System.Drawing.Color.FromArgb(224, 90,  20));
    private static readonly System.Drawing.SolidBrush BLbl      = new(System.Drawing.Color.FromArgb(100, 50,   0));
    private static readonly System.Drawing.SolidBrush BMoroso   = new(System.Drawing.Color.FromArgb(180,  0,   0));
    private static readonly System.Drawing.SolidBrush BNormal   = new(System.Drawing.Color.FromArgb( 21,101,  0));
    private static readonly System.Drawing.SolidBrush BGrisClaro= new(System.Drawing.Color.FromArgb(248,249,252));
    private static readonly System.Drawing.SolidBrush BPie      = new(System.Drawing.Color.FromArgb(120,120,140));

    private static readonly System.Drawing.Pen PenRojo   = new(System.Drawing.Color.FromArgb(210,  0,  0), 3.5f);
    private static readonly System.Drawing.Pen PenRojo1  = new(System.Drawing.Color.FromArgb(210,  0,  0), 1.0f);
    private static readonly System.Drawing.Pen PenGris19 = new(System.Drawing.Color.FromArgb(190,190,190), 1.0f);
    private static readonly System.Drawing.Pen PenGris05 = new(System.Drawing.Color.FromArgb(190,190,190), 0.5f);
    private static readonly System.Drawing.Pen PenSubBd  = new(System.Drawing.Color.FromArgb(220,220,230), 0.8f);
    private static readonly System.Drawing.Pen PenBorde  = new(System.Drawing.Color.FromArgb(200,200,210), 0.8f);

    // Texto descriptivo de combos
    private static string EstadoStr(byte v)  => v == 1 ? "Moroso" : "Normal";
    private static string SexoStr(byte v)    => v == 0 ? "Femenino" : "Masculino";
    private static string EcvStr(byte v)     => v switch { 0=>"Soltero",1=>"Casado",2=>"Divorciado",3=>"Separado",4=>"Viudo",_=>"—" };
    private static string CondStr(byte v)    => v switch { 0=>"—",1=>"Dependiente",2=>"Independiente",3=>"Jubilado",_=>"—" };
    private static string TipoStr(byte v)    => v switch { 0=>"Malo",1=>"Regular",2=>"Bueno",3=>"Muy Bueno",4=>"Excelente",_=>"—" };
    private static string InforStr(byte v)   => v == 1 ? "Registrado" : "Libre";
    private static string Dash(string? s)    => string.IsNullOrWhiteSpace(s) ? "—" : s;
    private static string Gs(decimal v)      => v > 0 ? v.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-PY")) + " Gs." : "0 Gs.";

    public static void Dibujar(System.Drawing.Graphics g, ClienteFichaData d)
    {
        var c = d.Cliente;
        g.Clear(System.Drawing.Color.White);
        g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;

        // ── Marca de agua ──────────────────────────────────────────────────────
        if (d.FotoDoc == null && System.IO.File.Exists(d.LogoPath))
        {
            using var logo = System.Drawing.Image.FromFile(d.LogoPath);
            float wmW = 480f, wmH = logo.Height * (wmW / logo.Width);
            DrawImg(g, logo, (int)(PgW/2f-wmW/2f), (int)(PgH/2f-wmH/2f), (int)wmW, (int)wmH, 0.045f);
        }

        // ── Encabezado ─────────────────────────────────────────────────────────
        DrawEncabezado(g, d);

        int y = 100;

        // ── Bloque: DATOS PERSONALES ───────────────────────────────────────────
        y = SeccionTitulo(g, y, "DATOS PERSONALES");
        y = FilaDoble(g, y, "CI",           c.CiCliente,        "RUC",        Dash(c.RucCliente));
        y = FilaSimple(g, y, "Nombre / Razón Social", c.NombreCliente);
        y = FilaDoble(g, y, "Teléfono",     Dash(c.TelefonoCliente), "Ciudad", Dash(c.CiudadCliente));
        y = FilaDoble(g, y, "Dirección",    Dash(c.DireccionCliente), "Zona",  Dash(c.ZonaCliente));
        y = FilaDoble(g, y, "Ref. Geogr.",  Dash(c.ReferenciaGeo),   "E-Mail", Dash(c.EmailCliente));
        y = FilaDoble(g, y, "Sexo",         SexoStr(c.Sexo),     "Estado Civil", EcvStr(c.Ecv));
        y = FilaDoble(g, y, "Cónyuge",      Dash(c.Conyuge),     "CI Cónyuge",   Dash(c.CiConyuge));
        y = FilaDoble(g, y, "Venc. CI",     c.VencCedula.HasValue ? c.VencCedula.Value.ToString("dd/MM/yyyy") : "—",
                             "Registro",    c.FechaRegistro.ToString("dd/MM/yyyy"));
        y += 6;

        // ── Bloque: DATOS CREDITICIOS ──────────────────────────────────────────
        y = SeccionTitulo(g, y, "DATOS CREDITICIOS");
        y = FilaDoble(g, y, "Estado",       EstadoStr(c.Estado), "Inforcom",    InforStr(c.Inforcom), esMoroso: c.Estado == 1);
        y = FilaDoble(g, y, "Crédito Máx.", Gs(c.CredMax),       "Tipo Cliente", TipoStr(c.Tipo));
        y = FilaDoble(g, y, "Condición",    CondStr(c.Condicion), "Local",       Dash(d.LocalNom));
        y += 6;

        // ── Bloque: DATOS LABORALES ────────────────────────────────────────────
        y = SeccionTitulo(g, y, "DATOS LABORALES");
        y = FilaSimple(g, y, "Empresa",          Dash(c.EmpresaLaboral));
        y = FilaDoble(g, y,  "Teléfono Lab.",    Dash(c.TelefonoLaboral), "Antigüedad", Dash(c.Antiguedad));
        y = FilaSimple(g, y, "Dirección Lab.",   Dash(c.DireccionLaboral));
        y += 10;

        // ── Foto documento (CI) ────────────────────────────────────────────────
        if (d.FotoDoc != null)
        {
            y = SeccionTitulo(g, y, "FOTO DOCUMENTO (CI)");
            y += 4;
            // Calcular tamaño manteniendo relación de aspecto, ancho máximo = Pw, alto máximo = 320px
            float maxW = Pw, maxH = 320f;
            float scale = Math.Min(maxW / d.FotoDoc.Width, maxH / d.FotoDoc.Height);
            int fw = (int)(d.FotoDoc.Width  * scale);
            int fh = (int)(d.FotoDoc.Height * scale);
            int fx = Mx + (Pw - fw) / 2;
            g.DrawRectangle(PenBorde, fx - 1, y - 1, fw + 2, fh + 2);
            DrawImg(g, d.FotoDoc, fx, y, fw, fh, 1f);
            y += fh + 6;
        }

        // ── Pie de página ──────────────────────────────────────────────────────
        int pieY = PgH - 30;
        g.DrawLine(PenGris19, Mx, pieY, Mx+Pw, pieY);
        var pie = $"Usuario: {d.Usuario}   ●   Fecha: {DateTime.Now:dd/MM/yyyy HH:mm}";
        var szP = g.MeasureString(pie, FntFoot);
        g.DrawString(pie, FntFoot, BPie, Mx + (Pw - szP.Width)/2f, pieY + 4);
    }

    // ── Encabezado (mismo estilo que listado) ──────────────────────────────────
    private static void DrawEncabezado(System.Drawing.Graphics g, ClienteFichaData d)
    {
        System.Drawing.Image? logo = System.IO.File.Exists(d.LogoPath)
            ? System.Drawing.Image.FromFile(d.LogoPath) : null;

        g.FillRectangle(System.Drawing.Brushes.White, 0, 0, PgW, 88);
        g.DrawLine(PenRojo, 0, 2, PgW, 2);

        float tx = Mx, tpw = Pw;
        if (logo != null)
        {
            float lh = 62f, lw = logo.Width * (lh / logo.Height);
            DrawImg(g, logo, Mx, 8, (int)lw, (int)lh, 1f);
            float sx = Mx + lw + 12f;
            g.DrawLine(PenGris19, sx, 6, sx, 78);
            tx = sx + 10f; tpw = Pw - (sx - Mx) - 10f;
            logo.Dispose();
        }

        // Banda naranja con título
        using (var path = new System.Drawing.Drawing2D.GraphicsPath())
        {
            const float r = 5f;
            path.AddArc(tx,          8,        r*2, r*2, 180, 90);
            path.AddArc(tx+tpw-r*2,  8,        r*2, r*2, 270, 90);
            path.AddArc(tx+tpw-r*2,  8+36-r*2, r*2, r*2, 0,   90);
            path.AddArc(tx,          8+36-r*2, r*2, r*2, 90,  90);
            path.CloseFigure();
            g.FillPath(BNaranja, path);
        }
        g.FillRectangle(BRojo, tx, 8, 4, 36);
        const string titulo = "FICHA DE CLIENTE";
        var szT = g.MeasureString(titulo, FntTit);
        g.DrawString(titulo, FntTit, System.Drawing.Brushes.White,
            tx + 8 + (tpw - 12 - szT.Width) / 2f, 8 + (36 - szT.Height) / 2f);

        // Subtítulo con nombre del cliente
        g.DrawLine(PenRojo1, tx, 50, tx+tpw, 50);
        g.FillRectangle(BSubBg, tx, 53, tpw, 24f);
        g.DrawRectangle(PenSubBd, tx, 53, tpw, 24f);
        DrawC(g, d.Cliente.NombreCliente, FntSub, BSubTxt, tx+4, 53, tpw-8, 12);
        DrawC(g, $"CI: {d.Cliente.CiCliente}   ●   Usuario: {d.Usuario}   ●   Fecha: {DateTime.Now:dd/MM/yyyy HH:mm}",
              FntSub, BSubTxt, tx+4, 65, tpw-8, 12);

        g.DrawLine(PenRojo,   0, 80, PgW, 80);
        g.DrawLine(PenGris05, 0, 82, PgW, 82);
    }

    // ── Sección título (banda naranja) ─────────────────────────────────────────
    private static int SeccionTitulo(System.Drawing.Graphics g, int y, string titulo)
    {
        g.FillRectangle(BSecBg, Mx, y, Pw, 18);
        g.DrawString(titulo, FntSec, System.Drawing.Brushes.White, Mx + 6f, y + 2f);
        return y + 22;
    }

    // ── Fila simple: label + valor (ancho completo) ────────────────────────────
    private static int FilaSimple(System.Drawing.Graphics g, int y, string lbl, string val)
    {
        const int h = 20;
        g.FillRectangle(BGrisClaro, Mx, y, Pw, h);
        g.DrawRectangle(PenBorde, Mx, y, Pw, h);
        g.DrawString(lbl + ":", FntLbl, BLbl, Mx + 6f, y + 3f);
        var szL = g.MeasureString(lbl + ":", FntLbl);
        g.DrawString(val, FntVal, System.Drawing.Brushes.Black, Mx + 8 + szL.Width, y + 3f);
        return y + h;
    }

    // ── Fila doble: 2 pares label/valor lado a lado ────────────────────────────
    private static int FilaDoble(System.Drawing.Graphics g, int y,
        string lbl1, string val1, string lbl2, string val2,
        bool esMoroso = false)
    {
        const int h = 20;
        int half = Pw / 2;
        // celda izquierda
        g.FillRectangle(BGrisClaro, Mx,        y, half,    h);
        g.DrawRectangle(PenBorde,   Mx,        y, half,    h);
        // celda derecha
        g.FillRectangle(BGrisClaro, Mx + half, y, Pw-half, h);
        g.DrawRectangle(PenBorde,   Mx + half, y, Pw-half, h);

        g.DrawString(lbl1 + ":", FntLbl, BLbl, Mx + 6f, y + 3f);
        var szL1 = g.MeasureString(lbl1 + ":", FntLbl);
        var brVal1 = esMoroso ? (System.Drawing.Brush)BMoroso : System.Drawing.Brushes.Black;
        g.DrawString(val1, FntVal, brVal1, Mx + 8 + szL1.Width, y + 3f);

        g.DrawString(lbl2 + ":", FntLbl, BLbl, Mx + half + 6f, y + 3f);
        var szL2 = g.MeasureString(lbl2 + ":", FntLbl);
        g.DrawString(val2, FntVal, System.Drawing.Brushes.Black, Mx + half + 8 + szL2.Width, y + 3f);

        return y + h;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────
    private static void DrawC(System.Drawing.Graphics g, string t, System.Drawing.Font f,
        System.Drawing.Brush b, float x, float y, float w, float h)
    {
        var s = g.MeasureString(t, f);
        g.DrawString(t, f, b, x + Math.Max(0f, (w-s.Width)/2f), y + Math.Max(0f, (h-s.Height)/2f));
    }

    private static void DrawImg(System.Drawing.Graphics g, System.Drawing.Image img,
        int x, int y, int w, int h, float op)
    {
        var cm = new System.Drawing.Imaging.ColorMatrix { Matrix33 = op };
        using var ia = new System.Drawing.Imaging.ImageAttributes();
        ia.SetColorMatrix(cm, System.Drawing.Imaging.ColorMatrixFlag.Default,
            System.Drawing.Imaging.ColorAdjustType.Bitmap);
        g.DrawImage(img, new System.Drawing.Rectangle(x, y, w, h),
            0, 0, img.Width, img.Height, System.Drawing.GraphicsUnit.Pixel, ia);
    }
}
