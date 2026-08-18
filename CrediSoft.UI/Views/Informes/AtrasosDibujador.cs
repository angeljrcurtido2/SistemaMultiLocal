namespace CrediSoft.UI.Views.Informes;

// ── Datos para el reporte de morosos (landscape) ────────────────────────────
public class AtrasosFilaMoroso
{
    public string  Cliente   { get; init; } = "";
    public string  Telefono  { get; init; } = "";
    public string  Solicitud { get; init; } = "";
    public int     NCuota    { get; init; }
    public decimal Monto     { get; init; }
    public string  Vto       { get; init; } = "";
    public int     Mora      { get; init; }
    public string  Local     { get; init; } = "";
    public string  Vendedor  { get; init; } = "";

    // Datos adicionales para el reporte de tarjetas (AtrasosTarjetasDibujador) — no se usan
    // en el reporte plano existente, solo en el nuevo formato tipo tarjeta por cliente.
    public string  ClienteCi     { get; init; } = "";
    public string  GaranteNombre { get; init; } = "";
    public string  GaranteTel    { get; init; } = "";
    public string  ArticuloDesc  { get; init; } = "";
    public decimal ArticuloPrecio{ get; init; }
}

public class AtrasosFilaResumen
{
    public string  Local     { get; init; } = "";
    public decimal Total     { get; init; }
    public decimal Atr1a30   { get; init; }
    public decimal Atr31a60  { get; init; }
    public decimal Atr61a90  { get; init; }
    public decimal AtrMas90  { get; init; }
    public bool    EsTotales { get; init; }
}

public class AtrasosPagina
{
    public List<AtrasosFilaMoroso>  Morosos   { get; init; } = new();
    public List<AtrasosFilaResumen> Resumen   { get; init; } = new();
    public decimal                  TotalGs   { get; init; }
    public string                   Filtro    { get; init; } = "";
    public string                   FechaImp  { get; init; } = "";
    public string                   Usuario   { get; init; } = "";
    public string                   LogoPath  { get; init; } = "";
}

// ── Dibujador reporte morosos (LANDSCAPE 1169×827) ─────────────────────────
public static class AtrasosMorososDibujador
{
    private const int PgW      = 1169;
    private const int PgH      = 827;
    private const int Lx       = 20;
    private const int Pw       = PgW - 40;        // 1129
    private const int PieAreaY = PgH - 38;
    private const int FilaH    = 22;
    private const int HdrH     = 22;
    private const int BodyY    = 85 + HdrH + 3;

    // Cliente|Teléfono|Solicitud|N°C|Monto|Vto|Mora|Local|Vendedor
    // 190+110+130+45+100+85+55+50+364 = 1129
    private static readonly int[]    Cw  = { 190, 110, 130, 45, 100, 85, 55, 50, 364 };
    private static readonly float[]  Cx  = { 20f, 210f, 320f, 450f, 495f, 595f, 680f, 735f, 785f };
    private static readonly string[] Hdr = { "Cliente", "Teléfono", "Solicitud", "N°C",
                                              "Monto Gs.", "Vto.", "Mora", "Loc.", "Vendedor" };

    private static readonly System.Drawing.Font FntTit  = new("Arial", 11f, System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntSub  = new("Arial",  6.5f, System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntHdr  = new("Arial",  7.5f, System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntRow  = new("Arial",  7.5f, System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntRowB = new("Arial",  7.5f, System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntFoot = new("Arial",  7f,   System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntPag  = new("Arial",  6f,   System.Drawing.FontStyle.Regular);

    private static readonly System.Drawing.SolidBrush BNaranja = new(System.Drawing.Color.FromArgb(230, 81,   0));
    private static readonly System.Drawing.SolidBrush BRojo    = new(System.Drawing.Color.FromArgb(210,  0,   0));
    private static readonly System.Drawing.SolidBrush BSubTxt  = new(System.Drawing.Color.FromArgb( 80, 80, 100));
    private static readonly System.Drawing.SolidBrush BSubBg   = new(System.Drawing.Color.FromArgb(245,245,248));
    private static readonly System.Drawing.SolidBrush BPagNum  = new(System.Drawing.Color.FromArgb(120,120,140));
    private static readonly System.Drawing.SolidBrush BAlt     = new(System.Drawing.Color.FromArgb(255,248,240));
    private static readonly System.Drawing.SolidBrush BHdr1    = new(System.Drawing.Color.FromArgb(224,112,  0));
    private static readonly System.Drawing.SolidBrush BMoraHi  = new(System.Drawing.Color.FromArgb(198, 40, 40));
    private static readonly System.Drawing.SolidBrush BMoraHiBg= new(System.Drawing.Color.FromArgb(255,235,238));

    private static readonly System.Drawing.Pen PenRojo   = new(System.Drawing.Color.FromArgb(210,  0,  0), 3f);
    private static readonly System.Drawing.Pen PenRojo1  = new(System.Drawing.Color.FromArgb(210,  0,  0), 0.8f);
    private static readonly System.Drawing.Pen PenGris19 = new(System.Drawing.Color.FromArgb(190,190,190), 1.0f);
    private static readonly System.Drawing.Pen PenGris05 = new(System.Drawing.Color.FromArgb(190,190,190), 0.5f);
    private static readonly System.Drawing.Pen PenNar15  = new(System.Drawing.Color.FromArgb(224,112,  0), 1.5f);
    private static readonly System.Drawing.Pen PenSep    = new(System.Drawing.Color.FromArgb(255,224,180), 0.5f);
    private static readonly System.Drawing.Pen PenHdrTop = new(System.Drawing.Color.FromArgb(160,160,160), 0.8f);
    private static readonly System.Drawing.Pen PenSubBd  = new(System.Drawing.Color.FromArgb(220,220,230), 0.8f);

    public static int ContarPaginas(AtrasosPagina p)
    {
        if (p.Morosos.Count == 0) return 1;
        int area = PieAreaY - BodyY;
        int porPag = area / FilaH;
        return (p.Morosos.Count + porPag - 1) / porPag;
    }

    public static void DibujarPagina(
        System.Drawing.Graphics g,
        AtrasosPagina p,
        System.Drawing.Image? logo,
        int pageOffset, int pageNum, int totalPages)
    {
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.None;

        // Marca de agua
        if (logo != null)
        {
            float wmW = 560f, wmH = logo.Height * (wmW / logo.Width);
            DrawImg(g, logo, (int)(PgW/2f-wmW/2f), (int)(PgH/2f-wmH/2f), (int)wmW, (int)wmH, 0.05f);
        }

        // ── Encabezado ────────────────────────────────────────────────────────
        g.FillRectangle(System.Drawing.Brushes.White, 0, 0, PgW, 80);
        g.DrawLine(PenRojo, 0, 2, PgW, 2);

        float tx = Lx, tpw = Pw;
        if (logo != null)
        {
            float lh = 55f, lw = logo.Width * (lh / logo.Height);
            DrawImg(g, logo, Lx, 6, (int)lw, (int)lh, 1f);
            float sx = Lx + lw + 10f;
            g.DrawLine(PenGris19, sx, 5, sx, 72);
            tx = sx + 8f; tpw = Pw - (sx - Lx) - 8f;
        }

        using (var path = new System.Drawing.Drawing2D.GraphicsPath())
        {
            const float r = 4f;
            path.AddArc(tx,          6,       r*2,r*2,180,90);
            path.AddArc(tx+tpw-r*2,  6,       r*2,r*2,270,90);
            path.AddArc(tx+tpw-r*2,  6+30-r*2,r*2,r*2,0,  90);
            path.AddArc(tx,          6+30-r*2,r*2,r*2,90, 90);
            path.CloseFigure();
            g.FillPath(BNaranja, path);
        }
        g.FillRectangle(BRojo, tx, 6, 4, 30);
        const string titulo = "REPORTE DE ATRASOS — MOROSOS";
        var szT = g.MeasureString(titulo, FntTit);
        g.DrawString(titulo, FntTit, System.Drawing.Brushes.White,
            tx + 8f + (tpw - 12f - szT.Width) / 2f, 6f + (30f - szT.Height) / 2f);

        g.DrawLine(PenRojo1, tx, 40f, tx+tpw, 40f);
        g.FillRectangle(BSubBg, tx, 43, tpw, 22f);
        g.DrawRectangle(PenSubBd, tx, 43, tpw, 22f);
        var sub = string.IsNullOrEmpty(p.Filtro) ? "Todos los atrasos" : p.Filtro;
        DrawCL(g, sub, FntSub, BSubTxt, tx+4f, 43f, tpw-8f, 11f);
        DrawCL(g, $"Fecha: {p.FechaImp}   ●   Usuario: {p.Usuario}", FntSub, BSubTxt, tx+4f, 54f, tpw-8f, 11f);

        g.DrawLine(PenRojo,   0, 70, PgW, 70);
        g.DrawLine(PenGris05, 0, 72, PgW, 72);

        // ── Cabecera columnas ─────────────────────────────────────────────────
        int y = 80;
        g.DrawLine(PenHdrTop, Lx, y, Lx+Pw, y); y += 2;
        g.FillRectangle(BHdr1, Lx, y, Pw, HdrH);
        float fhH = FntHdr.GetHeight(g), tyH = HdrH/2f - fhH/2f;
        for (int i = 0; i < Hdr.Length; i++)
            g.DrawString(Hdr[i], FntHdr, System.Drawing.Brushes.White, Cx[i]+3f, y+tyH);
        y += HdrH;
        g.DrawLine(PenNar15, Lx, y, Lx+Pw, y); y += 3;

        // ── Filas ─────────────────────────────────────────────────────────────
        float fhR = FntRow.GetHeight(g), tyR = Math.Max(0f, (FilaH - fhR) / 2f);
        int area = PieAreaY - BodyY;
        int porPag = area / FilaH;
        int start = pageOffset, end = Math.Min(start + porPag, p.Morosos.Count);

        for (int i = start; i < end; i++)
        {
            var f = p.Morosos[i];
            bool moraAlta = f.Mora > 0;

            if (i % 2 == 1)
                g.FillRectangle(BAlt, Lx, y, Pw, FilaH);

            var brush = moraAlta ? BMoraHi : System.Drawing.Brushes.Black;
            var font  = moraAlta ? FntRowB : FntRow;
            float ty1 = y + tyR;

            DC(g, f.Cliente,              font, brush, 0, ty1);
            DC(g, f.Telefono,             font, brush, 1, ty1);
            DC(g, ShortSol(f.Solicitud),  font, brush, 2, ty1);
            DC(g, f.NCuota.ToString(),    font, brush, 3, ty1);
            DCR(g, $"{f.Monto:N0}",       font, brush, 4, ty1);
            DC(g, f.Vto,                  font, brush, 5, ty1);
            DCR(g, f.Mora > 0 ? $"{f.Mora}d" : "—", font, brush, 6, ty1);
            DC(g, f.Local,                font, brush, 7, ty1);
            DC(g, f.Vendedor,             font, brush, 8, ty1);

            y += FilaH;
            g.DrawLine(PenSep, Lx, y, Lx+Pw, y);
        }

        // ── Pie ───────────────────────────────────────────────────────────────
        bool esUltima = end >= p.Morosos.Count;
        g.DrawLine(esUltima ? PenNar15 : new System.Drawing.Pen(System.Drawing.Color.FromArgb(160,160,160), 0.8f),
            Lx, PieAreaY, Lx+Pw, PieAreaY);

        float fhF = FntFoot.GetHeight(g), fyF = PieAreaY + 4f + Math.Max(0f, (16f - fhF)/2f);
        g.DrawString($"{p.Morosos.Count} cuota(s) en atraso", FntFoot, System.Drawing.Brushes.Black, Lx+2f, fyF);
        if (esUltima)
        {
            var totalStr = $"Total en atraso: Gs. {p.TotalGs:N0}";
            var szTot = g.MeasureString(totalStr, FntFoot);
            g.DrawString(totalStr, FntFoot, BMoraHi, Lx+2f + 200f, fyF);
        }

        var pag = $"Página {pageNum} de {totalPages}";
        var szP = g.MeasureString(pag, FntPag);
        g.DrawString(pag, FntPag, BPagNum, Lx+Pw-szP.Width, PgH-16);
    }

    private static void DC(System.Drawing.Graphics g, string txt, System.Drawing.Font f,
        System.Drawing.Brush b, int col, float ty)
    {
        var t = Trunc(g, txt, f, Cw[col]-6f);
        g.DrawString(t, f, b, Cx[col]+3f, ty);
    }

    private static void DCR(System.Drawing.Graphics g, string txt, System.Drawing.Font f,
        System.Drawing.Brush b, int col, float ty)
    {
        var szT = g.MeasureString(txt, f);
        float rx = Cx[col] + Cw[col] - 5f - szT.Width;
        g.DrawString(txt, f, b, Math.Max(Cx[col]+2f, rx), ty);
    }

    private static void DrawCL(System.Drawing.Graphics g, string t, System.Drawing.Font f,
        System.Drawing.Brush b, float x, float y, float w, float h)
    {
        var s = g.MeasureString(t, f);
        g.DrawString(t, f, b, x, y + Math.Max(0f, (h - s.Height)/2f));
    }

    private static string Trunc(System.Drawing.Graphics g, string t, System.Drawing.Font f, float mw)
    {
        if (string.IsNullOrEmpty(t) || g.MeasureString(t, f).Width <= mw) return t;
        float ew = g.MeasureString("…", f).Width;
        while (t.Length > 0 && g.MeasureString(t, f).Width + ew > mw) t = t[..^1];
        return t + "…";
    }

    private static string ShortSol(string s)
    {
        var tr = s.TrimStart('0');
        return string.IsNullOrEmpty(tr) ? s : "#" + tr;
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

// ── Dibujador resumen por local (PORTRAIT 827×1169) ─────────────────────────
public static class AtrasosResumenDibujador
{
    private const int PgW      = 827;
    private const int PgH      = 1169;
    private const int Lx       = 20;
    private const int Pw       = PgW - 40;   // 787
    private const int PieAreaY = PgH - 42;
    private const int FilaH    = 28;
    private const int HdrH     = 24;
    private const int BodyY    = 92 + HdrH + 3;

    // Local | Tot.Atrasos | 1-30d | 31-60d | 61-90d | +90d
    // 200 + 137 + 112 + 112 + 112 + 114 = 787
    private static readonly int[]    Cw  = { 200, 137, 112, 112, 112, 114 };
    private static readonly float[]  Cx  = { 20f, 220f, 357f, 469f, 581f, 693f };
    private static readonly string[] Hdr = { "Local / Sucursal", "Tot. Atrasos Gs.",
                                              "1-30 días", "31-60 días", "61-90 días", "+90 días" };

    private static readonly System.Drawing.Font FntTit  = new("Arial", 12f, System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntSub  = new("Arial",  7f, System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntHdr  = new("Arial",  8f, System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntRow  = new("Arial",  8.5f, System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntRowB = new("Arial",  8.5f, System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntFoot = new("Arial",  7.5f, System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntPag  = new("Arial",  6.5f, System.Drawing.FontStyle.Regular);

    private static readonly System.Drawing.SolidBrush BNaranja = new(System.Drawing.Color.FromArgb(230, 81,   0));
    private static readonly System.Drawing.SolidBrush BRojo    = new(System.Drawing.Color.FromArgb(210,  0,   0));
    private static readonly System.Drawing.SolidBrush BSubTxt  = new(System.Drawing.Color.FromArgb( 80, 80, 100));
    private static readonly System.Drawing.SolidBrush BSubBg   = new(System.Drawing.Color.FromArgb(245,245,248));
    private static readonly System.Drawing.SolidBrush BPagNum  = new(System.Drawing.Color.FromArgb(120,120,140));
    private static readonly System.Drawing.SolidBrush BAlt     = new(System.Drawing.Color.FromArgb(248,249,252));
    private static readonly System.Drawing.SolidBrush BVerde   = new(System.Drawing.Color.FromArgb( 56,142, 60));
    private static readonly System.Drawing.SolidBrush BTotFg   = new(System.Drawing.Color.FromArgb(191, 54, 12));
    private static readonly System.Drawing.SolidBrush BTotBg   = new(System.Drawing.Color.FromArgb(255,243,224));

    private static readonly System.Drawing.Pen PenRojo   = new(System.Drawing.Color.FromArgb(210,  0,  0), 3.5f);
    private static readonly System.Drawing.Pen PenRojo1  = new(System.Drawing.Color.FromArgb(210,  0,  0), 1.0f);
    private static readonly System.Drawing.Pen PenGris19 = new(System.Drawing.Color.FromArgb(190,190,190), 1.0f);
    private static readonly System.Drawing.Pen PenGris05 = new(System.Drawing.Color.FromArgb(190,190,190), 0.5f);
    private static readonly System.Drawing.Pen PenVerde  = new(System.Drawing.Color.FromArgb( 56,142, 60), 1.5f);
    private static readonly System.Drawing.Pen PenSep    = new(System.Drawing.Color.FromArgb(200,210,225), 0.5f);
    private static readonly System.Drawing.Pen PenSubBd  = new(System.Drawing.Color.FromArgb(220,220,230), 0.8f);
    private static readonly System.Drawing.Pen PenHdrTop = new(System.Drawing.Color.FromArgb(160,160,160), 0.8f);

    public static int ContarPaginas(AtrasosPagina p)
    {
        if (p.Resumen.Count == 0) return 1;
        int area = PieAreaY - BodyY;
        int porPag = area / FilaH;
        return (p.Resumen.Count + porPag - 1) / porPag;
    }

    public static void DibujarPagina(
        System.Drawing.Graphics g,
        AtrasosPagina p,
        System.Drawing.Image? logo,
        int pageOffset, int pageNum, int totalPages)
    {
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.None;

        if (logo != null)
        {
            float wmW = 500f, wmH = logo.Height * (wmW / logo.Width);
            DrawImg(g, logo, (int)(PgW/2f-wmW/2f), (int)(PgH/2f-wmH/2f+40f), (int)wmW, (int)wmH, 0.055f);
        }

        g.FillRectangle(System.Drawing.Brushes.White, 0, 0, PgW, 88);
        g.DrawLine(PenRojo, 0, 2, PgW, 2);

        float tx = Lx, tpw = Pw;
        if (logo != null)
        {
            float lh = 62f, lw = logo.Width * (lh / logo.Height);
            DrawImg(g, logo, Lx, 8, (int)lw, (int)lh, 1f);
            float sx = Lx + lw + 12f;
            g.DrawLine(PenGris19, sx, 6, sx, 78);
            tx = sx + 10f; tpw = Pw - (sx - Lx) - 10f;
        }

        using (var path = new System.Drawing.Drawing2D.GraphicsPath())
        {
            const float r = 5f;
            path.AddArc(tx,          8,        r*2,r*2,180,90);
            path.AddArc(tx+tpw-r*2,  8,        r*2,r*2,270,90);
            path.AddArc(tx+tpw-r*2,  8+36-r*2, r*2,r*2,  0,90);
            path.AddArc(tx,          8+36-r*2, r*2,r*2, 90,90);
            path.CloseFigure();
            g.FillPath(BVerde, path);
        }
        g.FillRectangle(BRojo, tx, 8, 4, 36);
        const string titulo = "RESUMEN DE ATRASOS POR LOCAL";
        var szT = g.MeasureString(titulo, FntTit);
        g.DrawString(titulo, FntTit, System.Drawing.Brushes.White,
            tx + 8f + (tpw - 12f - szT.Width)/2f, 8f + (36f - szT.Height)/2f);

        g.DrawLine(PenRojo1, tx, 50, tx+tpw, 50);
        g.FillRectangle(BSubBg, tx, 53, tpw, 24f);
        g.DrawRectangle(PenSubBd, tx, 53, tpw, 24f);
        var sub = string.IsNullOrEmpty(p.Filtro) ? "Todos los atrasos" : p.Filtro;
        DrawC(g, sub, FntSub, BSubTxt, tx+4f, 53f, tpw-8f, 12f);
        DrawC(g, $"Fecha: {p.FechaImp}   ●   Usuario: {p.Usuario}", FntSub, BSubTxt, tx+4f, 65f, tpw-8f, 12f);

        g.DrawLine(PenRojo,   0, 80, PgW, 80);
        g.DrawLine(PenGris05, 0, 82, PgW, 82);

        // Cabecera columnas
        int y = 90;
        g.DrawLine(PenHdrTop, Lx, y, Lx+Pw, y); y += 2;
        g.FillRectangle(BVerde, Lx, y, Pw, HdrH);
        float fhH = FntHdr.GetHeight(g), tyH = HdrH/2f - fhH/2f;
        g.DrawString(Hdr[0], FntHdr, System.Drawing.Brushes.White, Cx[0]+4f, y+tyH);
        for (int i = 1; i < Hdr.Length; i++)
        {
            var szH = g.MeasureString(Hdr[i], FntHdr);
            g.DrawString(Hdr[i], FntHdr, System.Drawing.Brushes.White,
                Cx[i] + Cw[i] - szH.Width - 5f, y+tyH);
        }
        y += HdrH;
        g.DrawLine(PenVerde, Lx, y, Lx+Pw, y); y += 3;

        float fhR = FntRow.GetHeight(g), tyR = Math.Max(0f, (FilaH - fhR)/2f);
        int area = PieAreaY - BodyY;
        int porPag = area / FilaH;
        int start = pageOffset, end = Math.Min(start + porPag, p.Resumen.Count);

        for (int i = start; i < end; i++)
        {
            var f = p.Resumen[i];
            var font  = f.EsTotales ? FntRowB : FntRow;
            var brush = f.EsTotales ? (System.Drawing.Brush)BTotFg : System.Drawing.Brushes.Black;

            if (f.EsTotales)
                g.FillRectangle(BTotBg, Lx, y, Pw, FilaH);
            else if (i % 2 == 1)
                g.FillRectangle(BAlt, Lx, y, Pw, FilaH);

            float ty1 = y + tyR;
            g.DrawString(Trunc(g, f.Local, font, Cw[0]-8f), font, brush, Cx[0]+4f, ty1);
            DCR(g, f.Total    == 0 ? "—" : $"{f.Total:N0}",   font, brush, 1, ty1);
            DCR(g, f.Atr1a30  == 0 ? "—" : $"{f.Atr1a30:N0}", font, brush, 2, ty1);
            DCR(g, f.Atr31a60 == 0 ? "—" : $"{f.Atr31a60:N0}",font, brush, 3, ty1);
            DCR(g, f.Atr61a90 == 0 ? "—" : $"{f.Atr61a90:N0}",font, brush, 4, ty1);
            DCR(g, f.AtrMas90 == 0 ? "—" : $"{f.AtrMas90:N0}",font, brush, 5, ty1);

            y += FilaH;
            g.DrawLine(PenSep, Lx, y, Lx+Pw, y);
        }

        bool esUltima = end >= p.Resumen.Count;
        g.DrawLine(esUltima ? PenVerde
            : new System.Drawing.Pen(System.Drawing.Color.FromArgb(160,160,160), 0.8f),
            Lx, PieAreaY, Lx+Pw, PieAreaY);

        var pag = $"Página {pageNum} de {totalPages}";
        var szP = g.MeasureString(pag, FntPag);
        g.DrawString(pag, FntPag, BPagNum, Lx+Pw-szP.Width, PgH-18);
    }

    private static void DCR(System.Drawing.Graphics g, string txt, System.Drawing.Font f,
        System.Drawing.Brush b, int col, float ty)
    {
        var szT = g.MeasureString(txt, f);
        float rx = Cx[col] + Cw[col] - 5f - szT.Width;
        g.DrawString(txt, f, b, Math.Max(Cx[col]+2f, rx), ty);
    }

    private static void DrawC(System.Drawing.Graphics g, string t, System.Drawing.Font f,
        System.Drawing.Brush b, float x, float y, float w, float h)
    {
        var s = g.MeasureString(t, f);
        g.DrawString(t, f, b, x + Math.Max(0f,(w-s.Width)/2f), y + Math.Max(0f,(h-s.Height)/2f));
    }

    private static string Trunc(System.Drawing.Graphics g, string t, System.Drawing.Font f, float mw)
    {
        if (string.IsNullOrEmpty(t) || g.MeasureString(t, f).Width <= mw) return t;
        float ew = g.MeasureString("…", f).Width;
        while (t.Length > 0 && g.MeasureString(t, f).Width + ew > mw) t = t[..^1];
        return t + "…";
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
