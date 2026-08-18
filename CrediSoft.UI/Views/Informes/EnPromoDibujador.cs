namespace CrediSoft.UI.Views.Informes;

public record FilaEnPromoImp(
    string  Local,
    string  Codigo,
    string  Articulo,
    string  Inicio,
    string  Fin,
    decimal PPromo,
    string  Estado
);

public class EnPromoPagina
{
    public List<FilaEnPromoImp> Filas    { get; init; } = new();
    public string FechaImp  { get; init; } = "";
    public string Usuario   { get; init; } = "";
    public string LogoPath  { get; init; } = "";
    public string LocalFiltro { get; init; } = "Todos los locales";
    public int    Vigentes  { get; init; }
    public int    Vencidas  { get; init; }
    public int    Futuras   { get; init; }

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

public static class EnPromoDibujador
{
    // Portrait A4: 827 × 1169
    // Local | Código | Artículo | Inicio | Fin | P.Promo Gs. | Estado
    private static readonly int[] Cws         = { 120, 70, 0, 88, 88, 110, 80 };
    // Col 2 (Artículo) se calcula para llenar  →  787 - (120+70+88+88+110+80) = 231
    private static readonly bool[] RightAlign = { false, false, false, false, false, true, false };

    private const int PgW = 827;
    private const int PgH = 1169;
    private const int Lx  = 20;
    private const int Pw  = PgW - 40; // 787

    private static int ArticuloW => Pw - Cws.Where((_, i) => i != 2).Sum(); // ~231

    private static readonly System.Drawing.Color Violeta  = System.Drawing.Color.FromArgb(106, 27, 154);  // #6A1B9A
    private static readonly System.Drawing.Color VioletaD = System.Drawing.Color.FromArgb(74, 20, 140);   // #4A148C
    private static readonly System.Drawing.Color Verde    = System.Drawing.Color.FromArgb(27, 94, 32);    // #1B5E20
    private static readonly System.Drawing.Color Gris     = System.Drawing.Color.FromArgb(158, 158, 158); // #9E9E9E
    private static readonly System.Drawing.Color Azul     = System.Drawing.Color.FromArgb(21, 101, 192);  // #1565C0

    public static int ContarPaginas(EnPromoPagina p)
    {
        const int hdrH = 20, rowH = 28;
        int y = 88 + 2 + hdrH + 3;
        int paginas = 1;
        foreach (var _ in p.Filas)
        {
            if (y > PgH - 60) { paginas++; y = 88 + 2 + hdrH + 3; }
            y += rowH;
        }
        return paginas;
    }

    public static bool DibujarPagina(
        System.Drawing.Graphics g,
        EnPromoPagina p,
        System.Drawing.Image? logo,
        ref int filaOffset,
        int pageNum,
        int totalPages)
    {
        using var fntHdr  = new System.Drawing.Font("Arial", 7.5f, System.Drawing.FontStyle.Bold);
        using var fntRow  = new System.Drawing.Font("Arial", 7.5f, System.Drawing.FontStyle.Regular);
        using var fntFoot = new System.Drawing.Font("Arial", 7.5f, System.Drawing.FontStyle.Bold);
        using var fntPag  = new System.Drawing.Font("Arial", 6.5f, System.Drawing.FontStyle.Regular);

        int artW = ArticuloW;

        // ── Marca de agua ────────────────────────────────────────────────────
        if (logo != null)
            DrawConOpacidad(g, logo,
                (int)(PgW / 2f - 250f), (int)(PgH / 2f - logo.Height * 500f / logo.Width / 2f),
                500, (int)(logo.Height * 500f / logo.Width), 0.055f);

        // ── Encabezado ───────────────────────────────────────────────────────
        g.FillRectangle(System.Drawing.Brushes.White, 0, 0, PgW, 86);
        using var penViol = new System.Drawing.Pen(Violeta, 3.5f);
        g.DrawLine(penViol, 0, 2, PgW, 2);

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

        g.DrawLine(penViol, 0, 78, PgW, 78);
        int y = 88;

        // ── Cabecera columnas ────────────────────────────────────────────────
        const int hdrH = 20;
        using var penHdrTop = new System.Drawing.Pen(System.Drawing.Color.FromArgb(160, 160, 160), 0.8f);
        g.DrawLine(penHdrTop, Lx, y, Lx + Pw, y);
        y += 2;

        string[] hdrs = { "Local", "Código", "Artículo", "Inicio", "Fin", "P. Promo Gs.", "Estado" };
        for (int i = 0; i < hdrs.Length; i++)
            Celda(g, hdrs[i], fntHdr, i, y, hdrH, System.Drawing.Brushes.Black, RightAlign[i], artW);

        y += hdrH;
        using var penHdrBot = new System.Drawing.Pen(Violeta, 1.5f);
        g.DrawLine(penHdrBot, Lx, y, Lx + Pw, y);
        y += 3;

        // ── Filas ────────────────────────────────────────────────────────────
        const int rowH = 28;
        bool hayMas = false;

        while (filaOffset < p.Filas.Count)
        {
            if (y > PgH - 60) { hayMas = true; break; }

            var f = p.Filas[filaOffset++];
            bool vigente = f.Estado == "Vigente";
            bool futura  = f.Estado == "Futura";

            if (vigente)
            {
                using var bBg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(243, 229, 245));
                g.FillRectangle(bBg, Lx, y, Pw, rowH);
            }

            System.Drawing.Color fgColor = vigente ? Verde : futura ? Azul : Gris;
            using var bFg = new System.Drawing.SolidBrush(fgColor);

            string[] vals =
            {
                f.Local, f.Codigo, f.Articulo,
                f.Inicio, f.Fin,
                f.PPromo.ToString("N0"),
                f.Estado
            };

            for (int i = 0; i < vals.Length; i++)
                Celda(g, vals[i], fntRow, i, y, rowH, bFg, RightAlign[i], artW);

            y += rowH;
            using var penSep = new System.Drawing.Pen(System.Drawing.Color.FromArgb(225, 210, 235), 0.4f);
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
            using (var pViol = new System.Drawing.Pen(Violeta, 1.5f))
                g.DrawLine(pViol, Lx, y, Lx + Pw, y);
            y += 6;

            const int pieH = 18;
            float partW  = Pw / 4f;
            float fhFoot = fntFoot.GetHeight(g);
            float footY  = y + Math.Max(0f, (pieH - fhFoot) / 2f);

            using var bVerde = new System.Drawing.SolidBrush(Verde);
            using var bAzul  = new System.Drawing.SolidBrush(Azul);

            g.DrawString($"Total: {p.Filas.Count}", fntFoot, System.Drawing.Brushes.Black, Lx + 2f, footY);
            DrawCentradoH(g, $"Vigentes: {p.Vigentes}",          fntFoot, bVerde,                       Lx + partW,      partW, footY);
            DrawCentradoH(g, $"Futuras: {p.Futuras}",             fntFoot, bAzul,                        Lx + partW * 2,  partW, footY);
            DrawCentradoH(g, $"Vencidas: {p.Vencidas}",           fntFoot, new System.Drawing.SolidBrush(Gris), Lx + partW * 3, partW, footY);

            using var penPD = new System.Drawing.Pen(System.Drawing.Color.FromArgb(160, 160, 160), 0.8f);
            for (int i = 1; i < 4; i++)
                g.DrawLine(penPD, Lx + i * partW, y + 2, Lx + i * partW, y + pieH - 2);
        }

        return hayMas;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void DibujarBandaTitulo(System.Drawing.Graphics g, float tx, float tpw)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        const float r5 = 5f;
        path.AddArc(tx,            8,           r5*2, r5*2, 180, 90);
        path.AddArc(tx+tpw-r5*2,  8,           r5*2, r5*2, 270, 90);
        path.AddArc(tx+tpw-r5*2,  8+34-r5*2,  r5*2, r5*2, 0,   90);
        path.AddArc(tx,            8+34-r5*2,  r5*2, r5*2, 90,  90);
        path.CloseFigure();
        using var bBanda = new System.Drawing.SolidBrush(Violeta);
        g.FillPath(bBanda, path);
        using var fntTit = new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Bold);
        DrawCentradoBox(g, "ARTÍCULOS EN PROMOCIÓN", fntTit, System.Drawing.Brushes.White, tx+8, 8, tpw-12, 34);
    }

    private static void DibujarSubtitulos(System.Drawing.Graphics g, EnPromoPagina p, float tx, float tpw)
    {
        using var penAT = new System.Drawing.Pen(Violeta, 1f);
        g.DrawLine(penAT, tx, 47, tx + tpw, 47);

        using var bSubBg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(237, 231, 246));
        g.FillRectangle(bSubBg, tx, 50, tpw, 24f);
        using var penSubBd = new System.Drawing.Pen(System.Drawing.Color.FromArgb(209, 196, 233), 0.8f);
        g.DrawRectangle(penSubBd, tx, 50, tpw, 24f);

        using var fntSub  = new System.Drawing.Font("Arial", 7f, System.Drawing.FontStyle.Regular);
        using var bSubTxt = new System.Drawing.SolidBrush(VioletaD);
        DrawCentradoBox(g, $"Local: {p.LocalFiltro}   ●   Fecha: {p.FechaImp}   ●   Usuario: {p.Usuario}", fntSub, bSubTxt, tx+4, 50, tpw-8, 24);
    }

    private static void Celda(System.Drawing.Graphics g, string txt, System.Drawing.Font f,
        int col, int ry, int rh, System.Drawing.Brush br, bool right, int artW)
    {
        if (string.IsNullOrEmpty(txt)) return;
        float colX, colW;
        if (col == 0)      { colX = Lx;                                                  colW = Cws[0]; }
        else if (col == 1) { colX = Lx + Cws[0];                                         colW = Cws[1]; }
        else if (col == 2) { colX = Lx + Cws[0] + Cws[1];                                colW = artW; }
        else               { colX = Lx + Cws[0] + Cws[1] + artW + Cws.Skip(3).Take(col-3).Sum(); colW = Cws[col]; }

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

    private static void DrawCentradoBox(System.Drawing.Graphics g, string txt, System.Drawing.Font f,
        System.Drawing.Brush br, float x, float y, float w, float h)
    {
        var sz = g.MeasureString(txt, f);
        g.DrawString(txt, f, br, x + Math.Max(0f, (w - sz.Width) / 2f), y + Math.Max(0f, (h - sz.Height) / 2f));
    }

    private static void DrawCentradoH(System.Drawing.Graphics g, string txt, System.Drawing.Font f,
        System.Drawing.Brush br, float x, float w, float y)
    {
        var sz = g.MeasureString(txt, f);
        g.DrawString(txt, f, br, x + Math.Max(0f, (w - sz.Width) / 2f), y);
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
