namespace CrediSoft.UI.Views.Ventas;

public class SolicitudPagina
{
    public List<SolicitudItem> Filas    { get; init; } = new();
    public string              Filtro   { get; init; } = "";
    public string              FechaImp { get; init; } = "";
    public string              Usuario  { get; init; } = "";
    public string              LogoPath { get; init; } = "";
}

public static class SolicitudDibujador
{
    private const int PgW      = 827;
    private const int PgH      = 1169;
    private const int Lx       = 20;
    private const int Pw       = PgW - 40;   // 787
    private const int PieAreaY = PgH - 42;
    private const int PieH     = 18;
    private const int FilaH    = 26;
    private const int HdrH     = 24;
    private const int BodyY    = 92 + HdrH + 3;

    // N° Solicitud | Local | Cliente | Vendedor | Estado | Fecha | Total
    // 105 + 110 + 195 + 140 + 72 + 75 + 90 = 787
    private static readonly int[]    Cw  = { 105, 110, 195, 140, 72, 75, 90 };
    private static readonly float[]  Cx  = { 20f, 125f, 235f, 430f, 570f, 642f, 717f };
    private static readonly string[] Hdr = { "N° Solicitud", "Local", "Cliente", "Vendedor", "Estado", "Fecha", "Total (Gs.)" };

    private static readonly System.Drawing.Font FntTit  = new("Arial", 12f,  System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntSub  = new("Arial",  7f,  System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntHdr  = new("Arial",  8f,  System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntRow  = new("Arial",  8f,  System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntBold = new("Arial",  8f,  System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntFoot = new("Arial",  7.5f,System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntPag  = new("Arial",  6.5f,System.Drawing.FontStyle.Regular);

    private static readonly System.Drawing.SolidBrush BNaranja  = new(System.Drawing.Color.FromArgb(230, 81,   0));
    private static readonly System.Drawing.SolidBrush BRojo     = new(System.Drawing.Color.FromArgb(210,  0,   0));
    private static readonly System.Drawing.SolidBrush BSubTxt   = new(System.Drawing.Color.FromArgb( 80, 80, 100));
    private static readonly System.Drawing.SolidBrush BSubBg    = new(System.Drawing.Color.FromArgb(245,245, 248));
    private static readonly System.Drawing.SolidBrush BPagNum   = new(System.Drawing.Color.FromArgb(120,120, 140));
    private static readonly System.Drawing.SolidBrush BAlt      = new(System.Drawing.Color.FromArgb(248,249, 252));
    private static readonly System.Drawing.SolidBrush BHdr      = new(System.Drawing.Color.FromArgb( 13, 71, 161));
    private static readonly System.Drawing.SolidBrush BVerific  = new(System.Drawing.Color.FromArgb(124, 58, 237));
    private static readonly System.Drawing.SolidBrush BAcept    = new(System.Drawing.Color.FromArgb( 22,163,  74));
    private static readonly System.Drawing.SolidBrush BRechaz   = new(System.Drawing.Color.FromArgb(220, 38,  38));
    private static readonly System.Drawing.SolidBrush BGrisEst  = new(System.Drawing.Color.FromArgb( 80, 80,  80));

    private static readonly System.Drawing.Pen PenRojo   = new(System.Drawing.Color.FromArgb(210, 0,  0), 3.5f);
    private static readonly System.Drawing.Pen PenRojo1  = new(System.Drawing.Color.FromArgb(210, 0,  0), 1.0f);
    private static readonly System.Drawing.Pen PenGris19 = new(System.Drawing.Color.FromArgb(190,190,190), 1.0f);
    private static readonly System.Drawing.Pen PenGris05 = new(System.Drawing.Color.FromArgb(190,190,190), 0.5f);
    private static readonly System.Drawing.Pen PenAzul15 = new(System.Drawing.Color.FromArgb( 13, 71,161), 1.5f);
    private static readonly System.Drawing.Pen PenSep    = new(System.Drawing.Color.FromArgb(200,210,225), 0.5f);
    private static readonly System.Drawing.Pen PenHdrTop = new(System.Drawing.Color.FromArgb(160,160,160), 0.8f);
    private static readonly System.Drawing.Pen PenSubBd  = new(System.Drawing.Color.FromArgb(220,220,230), 0.8f);
    private static readonly System.Drawing.Pen PenPie    = new(System.Drawing.Color.FromArgb(160,160,160), 0.8f);

    private static readonly System.Globalization.CultureInfo Cult =
        System.Globalization.CultureInfo.GetCultureInfo("es-PY");

    public static int ContarPaginas(SolicitudPagina p)
    {
        if (p.Filas.Count == 0) return 1;
        int area      = PieAreaY - BodyY;
        int porPagina = area / FilaH;
        return (p.Filas.Count + porPagina - 1) / porPagina;
    }

    public static bool DibujarPagina(
        System.Drawing.Graphics g,
        SolicitudPagina p,
        System.Drawing.Image? logo,
        ref int offset,
        int pageNum,
        int totalPages)
    {
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.None;

        // Marca de agua
        if (logo != null)
        {
            float wmW = 500f, wmH = logo.Height * (wmW / logo.Width);
            DrawImg(g, logo, (int)(PgW/2f-wmW/2f), (int)(PgH/2f-wmH/2f+40f), (int)wmW, (int)wmH, 0.055f);
        }

        // Encabezado
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
            path.AddArc(tx,          8,        r*2, r*2, 180, 90);
            path.AddArc(tx+tpw-r*2,  8,        r*2, r*2, 270, 90);
            path.AddArc(tx+tpw-r*2,  8+36-r*2, r*2, r*2, 0,   90);
            path.AddArc(tx,          8+36-r*2, r*2, r*2, 90,  90);
            path.CloseFigure();
            g.FillPath(BNaranja, path);
        }
        g.FillRectangle(BRojo, tx, 8, 4, 36);
        const string titulo = "SOLICITUDES DE VENTA A CRÉDITO";
        var szT = g.MeasureString(titulo, FntTit);
        g.DrawString(titulo, FntTit, System.Drawing.Brushes.White,
            tx + 8 + (tpw - 12 - szT.Width) / 2f, 8 + (36 - szT.Height) / 2f);

        g.DrawLine(PenRojo1, tx, 50, tx+tpw, 50);
        g.FillRectangle(BSubBg, tx, 53, tpw, 24f);
        g.DrawRectangle(PenSubBd, tx, 53, tpw, 24f);
        var sub = string.IsNullOrEmpty(p.Filtro) ? "Todas las solicitudes" : p.Filtro;
        DrawC(g, sub,                                               FntSub, BSubTxt, tx+4, 53, tpw-8, 12);
        DrawC(g, $"Fecha: {p.FechaImp}   ●   Usuario: {p.Usuario}", FntSub, BSubTxt, tx+4, 65, tpw-8, 12);

        g.DrawLine(PenRojo,   0, 80, PgW, 80);
        g.DrawLine(PenGris05, 0, 82, PgW, 82);

        int y = 90;

        // Cabecera columnas
        g.DrawLine(PenHdrTop, Lx, y, Lx+Pw, y); y += 2;
        g.FillRectangle(BHdr, Lx, y, Pw, HdrH);
        float fhH = FntHdr.GetHeight(g), tyH = HdrH/2f - fhH/2f;
        for (int i = 0; i < Hdr.Length; i++)
            g.DrawString(Hdr[i], FntHdr, System.Drawing.Brushes.White, Cx[i]+4f, y+tyH);
        y += HdrH;
        g.DrawLine(PenAzul15, Lx, y, Lx+Pw, y); y += 3;

        // Filas
        bool  hayMas = false;
        float fhR = FntRow.GetHeight(g), tyR = Math.Max(0f, (FilaH - fhR) / 2f);

        while (offset < p.Filas.Count)
        {
            if (y + FilaH > PieAreaY - 4) { hayMas = true; break; }
            var f = p.Filas[offset++];

            if (offset % 2 == 0)
                g.FillRectangle(BAlt, Lx, y, Pw, FilaH);

            float ty1 = y + tyR;
            DC(g, f.Numero,                              0, ty1, System.Drawing.Brushes.Black);
            DC(g, f.LocalNombre,                         1, ty1, System.Drawing.Brushes.Black);
            DC(g, f.ClienteNombre,                       2, ty1, System.Drawing.Brushes.Black);
            DC(g, f.VendedorNombre,                      3, ty1, System.Drawing.Brushes.Black);
            // Estado con color
            var bEst = f.Estado switch {
                "Aceptado"  => BAcept,
                "Verificar" => BVerific,
                "Rechazado" => BRechaz,
                _           => BGrisEst,
            };
            DC(g, f.Estado,                              4, ty1, bEst);
            DC(g, f.FechaSolicitud.ToString("dd/MM/yyyy"), 5, ty1, System.Drawing.Brushes.Black);
            DC(g, f.TotalVenta.ToString("N0", Cult),     6, ty1, System.Drawing.Brushes.Black);

            y += FilaH;
            g.DrawLine(PenSep, Lx, y, Lx+Pw, y);
        }

        // Pie
        g.DrawLine(hayMas ? PenPie : PenAzul15, Lx, PieAreaY, Lx+Pw, PieAreaY);
        if (!hayMas)
        {
            float fhF = FntFoot.GetHeight(g), fy = PieAreaY+4+Math.Max(0f,(PieH-fhF)/2f);
            var totGral = p.Filas.Sum(f => f.TotalVenta).ToString("N0", Cult);
            g.DrawString($"Total registros: {p.Filas.Count}   |   Total Gs.: {totGral}",
                FntFoot, System.Drawing.Brushes.Black, Lx+2f, fy);
        }
        var pag = $"Página {pageNum} de {totalPages}";
        var szP = g.MeasureString(pag, FntPag);
        g.DrawString(pag, FntPag, BPagNum, Lx+Pw-szP.Width, PgH-18);

        return hayMas;
    }

    private static void DC(System.Drawing.Graphics g, string txt, int col, float ty, System.Drawing.Brush br)
    {
        var t = Trunc(g, txt, FntRow, Cw[col] - 8f);
        g.DrawString(t, FntRow, br, Cx[col]+4f, ty);
    }

    private static string Trunc(System.Drawing.Graphics g, string t, System.Drawing.Font f, float mw)
    {
        if (string.IsNullOrEmpty(t) || g.MeasureString(t, f).Width <= mw) return t;
        float ew = g.MeasureString("…", f).Width;
        while (t.Length > 0 && g.MeasureString(t, f).Width + ew > mw) t = t[..^1];
        return t + "…";
    }

    private static void DrawC(System.Drawing.Graphics g, string t, System.Drawing.Font f,
        System.Drawing.Brush b, float x, float y, float w, float h)
    {
        var s = g.MeasureString(t, f);
        g.DrawString(t, f, b, x+Math.Max(0f,(w-s.Width)/2f), y+Math.Max(0f,(h-s.Height)/2f));
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
