namespace CrediSoft.UI.Views.Informes;

public record FilaCreditoImp(
    string  Fecha,
    string  Cliente,
    string  Telefono,
    string  Solicitud,
    string  Local,
    int     Cuotas,
    decimal Total,
    decimal Debe,
    decimal Haber,
    string  Usuario
);

public class CreditosPagina
{
    public List<FilaCreditoImp> Filas { get; init; } = new();
    public string Desde     { get; init; } = "";
    public string Hasta     { get; init; } = "";
    public string FechaImp  { get; init; } = "";
    public string Usuario   { get; init; } = "";
    public string LogoPath  { get; init; } = "";

    public static string ResolverLogoPath()
    {
        const string nombre = "logotipocredimar2.png";
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var candidatos = new[]
        {
            System.IO.Path.Combine(baseDir, nombre),
            System.IO.Path.Combine(baseDir, "..", nombre),
            System.IO.Path.Combine(baseDir, "..", "..", "..", "..", "..", nombre),
        };
        return candidatos.FirstOrDefault(System.IO.File.Exists) ?? System.IO.Path.Combine(baseDir, nombre);
    }
}

public static class CreditosDibujador
{
    // Landscape A4: 1169 × 827
    // Fecha | Cliente | Teléfono | Solicitud | Local | Cuotas | Total Gs. | Debe Gs. | Haber Gs. | Usuario
    private static readonly int[] Cws         = { 80, 210, 95, 75, 105, 52, 100, 100, 100, 120 };
    // Sum = 1037  (Pw=1129)
    private static readonly bool[] RightAlign = { false, false, false, false, false, true, true, true, true, false };

    private const int PgW = 1169;
    private const int PgH = 827;
    private const int Lx  = 20;
    private const int Pw  = PgW - 40; // 1129

    private static readonly System.Drawing.Color Naranja = System.Drawing.Color.FromArgb(230, 81, 0);   // #E65100
    private static readonly System.Drawing.Color Azul    = System.Drawing.Color.FromArgb(13, 71, 161);  // #0D47A1
    private static readonly System.Drawing.Color Verde   = System.Drawing.Color.FromArgb(27, 94, 32);   // #1B5E20

    public static int ContarPaginas(CreditosPagina p)
    {
        const int hdrH = 20, rowH = 26;
        int y = 88 + 2 + hdrH + 3;
        int paginas = 1;
        foreach (var _ in p.Filas)
        {
            if (y > PgH - 52) { paginas++; y = 88 + 2 + hdrH + 3; }
            y += rowH;
        }
        return paginas;
    }

    public static bool DibujarPagina(
        System.Drawing.Graphics g,
        CreditosPagina p,
        System.Drawing.Image? logo,
        ref int filaOffset,
        int pageNum,
        int totalPages)
    {
        using var fntHdr  = new System.Drawing.Font("Arial", 7.5f, System.Drawing.FontStyle.Bold);
        using var fntRow  = new System.Drawing.Font("Arial", 7f,   System.Drawing.FontStyle.Regular);
        using var fntFoot = new System.Drawing.Font("Arial", 7.5f, System.Drawing.FontStyle.Bold);
        using var fntPag  = new System.Drawing.Font("Arial", 6.5f, System.Drawing.FontStyle.Regular);

        // ── Marca de agua ────────────────────────────────────────────────────
        if (logo != null)
        {
            float wmW = 500f, wmH = logo.Height * (wmW / logo.Width);
            DrawConOpacidad(g, logo,
                (int)(PgW / 2f - wmW / 2f),
                (int)(PgH / 2f - wmH / 2f),
                (int)wmW, (int)wmH, 0.055f);
        }

        // ── Encabezado ───────────────────────────────────────────────────────
        g.FillRectangle(System.Drawing.Brushes.White, 0, 0, PgW, 86);
        using var penNar = new System.Drawing.Pen(Naranja, 3.5f);
        g.DrawLine(penNar, 0, 2, PgW, 2);

        if (logo != null)
        {
            float logoH2 = 60f, logoW2 = logo.Width * (logoH2 / logo.Height);
            DrawConOpacidad(g, logo, Lx, 8, (int)logoW2, (int)logoH2, 1f);
            float sepX = Lx + logoW2 + 12f;
            using var penGris = new System.Drawing.Pen(System.Drawing.Color.FromArgb(190, 190, 190), 1f);
            g.DrawLine(penGris, sepX, 6, sepX, 76);
            float tx = sepX + 10f, tpw = Pw - (sepX - Lx) - 10f;
            DibujarBandaTitulo(g, tx, tpw);
            DibujarSubtitulos(g, p, tx, tpw);
        }
        else
        {
            DibujarBandaTitulo(g, Lx, Pw);
            DibujarSubtitulos(g, p, Lx, Pw);
        }

        g.DrawLine(penNar, 0, 78, PgW, 78);
        using var penGris2 = new System.Drawing.Pen(System.Drawing.Color.FromArgb(190, 190, 190), 0.5f);
        g.DrawLine(penGris2, 0, 80, PgW, 80);

        int y = 88;

        // ── Cabecera columnas ────────────────────────────────────────────────
        const int hdrH = 20;
        using var penHdrTop = new System.Drawing.Pen(System.Drawing.Color.FromArgb(160, 160, 160), 0.8f);
        g.DrawLine(penHdrTop, Lx, y, Lx + Pw, y);
        y += 2;

        string[] hdrs = { "Fecha", "Cliente", "Teléfono", "Solicitud", "Local", "Cuo.", "Total Gs.", "Debe Gs.", "Haber Gs.", "Usuario" };
        for (int i = 0; i < hdrs.Length; i++)
            Celda(g, hdrs[i], fntHdr, i, y, hdrH, System.Drawing.Brushes.Black, RightAlign[i]);

        y += hdrH;
        using var penHdrBot = new System.Drawing.Pen(Naranja, 1.5f);
        g.DrawLine(penHdrBot, Lx, y, Lx + Pw, y);
        y += 3;

        // ── Filas ────────────────────────────────────────────────────────────
        const int rowH = 26;
        bool hayMas = false;

        while (filaOffset < p.Filas.Count)
        {
            if (y > PgH - 52) { hayMas = true; break; }

            var f = p.Filas[filaOffset++];
            string[] vals =
            {
                f.Fecha, f.Cliente, f.Telefono, f.Solicitud, f.Local,
                f.Cuotas.ToString(),
                f.Total.ToString("N0"),
                f.Debe.ToString("N0"),
                f.Haber.ToString("N0"),
                f.Usuario
            };

            for (int i = 0; i < vals.Length; i++)
            {
                System.Drawing.Brush bTxt = System.Drawing.Brushes.Black;
                bool disp = false;
                if (i == 6 && f.Total > 0)
                    { bTxt = new System.Drawing.SolidBrush(Azul); disp = true; }
                else if (i == 7 && f.Debe > 0)
                    { bTxt = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(183, 28, 28)); disp = true; }
                else if (i == 8 && f.Haber > 0)
                    { bTxt = new System.Drawing.SolidBrush(Verde); disp = true; }
                Celda(g, vals[i], fntRow, i, y, rowH, bTxt, RightAlign[i]);
                if (disp) bTxt.Dispose();
            }

            y += rowH;
            using var penSep = new System.Drawing.Pen(System.Drawing.Color.FromArgb(200, 215, 230), 0.5f);
            g.DrawLine(penSep, Lx, y, Lx + Pw, y);
        }

        // ── Pie ──────────────────────────────────────────────────────────────
        string pagTxt = $"Página {pageNum} de {totalPages}";
        var szPag = g.MeasureString(pagTxt, fntPag);
        using var bPag = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(120, 120, 140));
        g.DrawString(pagTxt, fntPag, bPag, Lx + Pw - szPag.Width, PgH - 26);

        if (!hayMas)
        {
            y += 4;
            using (var p1 = new System.Drawing.Pen(Naranja, 1.5f))
                g.DrawLine(p1, Lx, y, Lx + Pw, y);
            y += 6;

            const int pieH = 18;
            float partW = Pw / 3f;
            float fhFoot = fntFoot.GetHeight(g);
            float footY  = y + Math.Max(0f, (pieH - fhFoot) / 2f);

            using var bRojo = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(183, 28, 28));
            using var bVerde = new System.Drawing.SolidBrush(Verde);
            g.DrawString($"Solicitudes: {p.Filas.Count}", fntFoot, System.Drawing.Brushes.Black, Lx + 2f, footY);
            DrawDerecha(g, $"Total otorgado: Gs. {p.Filas.Sum(f => f.Total):N0}", fntFoot, System.Drawing.Brushes.Black, Lx + partW,     partW, footY);
            DrawDerecha(g, $"Pendiente (Debe): Gs. {p.Filas.Sum(f => f.Debe):N0}", fntFoot, bRojo,                       Lx + 2 * partW, partW, footY);

            using var penPD = new System.Drawing.Pen(System.Drawing.Color.FromArgb(160, 160, 160), 0.8f);
            for (int i = 1; i < 3; i++)
                g.DrawLine(penPD, Lx + i * partW, y + 2, Lx + i * partW, y + pieH - 2);
        }

        return hayMas;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void DibujarBandaTitulo(System.Drawing.Graphics g, float tx, float tpw)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        const float r5 = 5f;
        path.AddArc(tx,                     8,              r5*2, r5*2, 180, 90);
        path.AddArc(tx + tpw - r5*2,        8,              r5*2, r5*2, 270, 90);
        path.AddArc(tx + tpw - r5*2,        8 + 34 - r5*2, r5*2, r5*2, 0,   90);
        path.AddArc(tx,                     8 + 34 - r5*2, r5*2, r5*2, 90,  90);
        path.CloseFigure();
        using var bBanda = new System.Drawing.SolidBrush(Naranja);
        g.FillPath(bBanda, path);
        using var fntTit = new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Bold);
        DrawCentrado(g, "HISTORIAL DE CRÉDITOS", fntTit, System.Drawing.Brushes.White, tx + 8, 8, tpw - 12, 34);
    }

    private static void DibujarSubtitulos(System.Drawing.Graphics g, CreditosPagina p, float tx, float tpw)
    {
        using var penAT = new System.Drawing.Pen(Naranja, 1f);
        g.DrawLine(penAT, tx, 47, tx + tpw, 47);

        using var bSubBg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 243, 224));
        g.FillRectangle(bSubBg, tx, 50, tpw, 24f);
        using var penSubBd = new System.Drawing.Pen(System.Drawing.Color.FromArgb(255, 204, 128), 0.8f);
        g.DrawRectangle(penSubBd, tx, 50, tpw, 24f);

        using var fntSub  = new System.Drawing.Font("Arial", 7f, System.Drawing.FontStyle.Regular);
        using var bSubTxt = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(130, 50, 0));
        string linea1 = string.IsNullOrEmpty(p.Desde)
            ? "Sin filtro de fecha"
            : $"Período: {p.Desde} al {p.Hasta}";
        DrawCentrado(g, linea1,                                             fntSub, bSubTxt, tx + 4, 50, tpw - 8, 12);
        DrawCentrado(g, $"Fecha: {p.FechaImp}   ●   Usuario: {p.Usuario}", fntSub, bSubTxt, tx + 4, 62, tpw - 8, 12);
    }

    private static void Celda(System.Drawing.Graphics g, string txt, System.Drawing.Font f,
        int col, int ry, int rh, System.Drawing.Brush br, bool right)
    {
        if (string.IsNullOrEmpty(txt)) return;
        float colX = Lx + Cws.Take(col).Sum();
        float colW = Cws[col];
        float maxW = colW - 4f;
        float fh   = f.GetHeight(g);
        float ty   = ry + Math.Max(0f, (rh - fh) / 2f);
        txt = Truncar(g, txt, f, maxW);
        float tx2 = right ? colX + colW - g.MeasureString(txt, f).Width - 2f : colX + 2f;
        g.DrawString(txt, f, br, tx2, ty);
    }

    private static string Truncar(System.Drawing.Graphics g, string txt, System.Drawing.Font f, float maxW)
    {
        if (g.MeasureString(txt, f).Width <= maxW) return txt;
        const string e = "…";
        float ew = g.MeasureString(e, f).Width;
        while (txt.Length > 0 && g.MeasureString(txt, f).Width + ew > maxW) txt = txt[..^1];
        return txt + e;
    }

    private static void DrawCentrado(System.Drawing.Graphics g, string txt, System.Drawing.Font f,
        System.Drawing.Brush br, float x, float y, float w, float h)
    {
        var sz = g.MeasureString(txt, f);
        g.DrawString(txt, f, br, x + Math.Max(0f, (w - sz.Width) / 2f), y + Math.Max(0f, (h - sz.Height) / 2f));
    }

    private static void DrawDerecha(System.Drawing.Graphics g, string txt, System.Drawing.Font f,
        System.Drawing.Brush br, float x, float w, float y)
    {
        var sz = g.MeasureString(txt, f);
        g.DrawString(txt, f, br, x + w - sz.Width - 2f, y);
    }

    private static void DrawConOpacidad(System.Drawing.Graphics g, System.Drawing.Image img,
        int x, int y, int w, int h, float opacity)
    {
        var cm = new System.Drawing.Imaging.ColorMatrix { Matrix33 = opacity };
        using var ia = new System.Drawing.Imaging.ImageAttributes();
        ia.SetColorMatrix(cm, System.Drawing.Imaging.ColorMatrixFlag.Default, System.Drawing.Imaging.ColorAdjustType.Bitmap);
        g.DrawImage(img, new System.Drawing.Rectangle(x, y, w, h),
            0, 0, img.Width, img.Height, System.Drawing.GraphicsUnit.Pixel, ia);
    }
}
