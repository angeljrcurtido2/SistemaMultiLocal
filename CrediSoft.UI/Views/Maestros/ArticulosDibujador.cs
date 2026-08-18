namespace CrediSoft.UI.Views.Maestros;

// Campos por artículo en el listado PDF
public record FilaArticuloImp(
    string  Codigo,
    string  Descripcion,
    string  Gravada,        // "Sí" / "No"
    string  Iva,            // "10%" / "5%"
    string  MaxCuota,
    string  StockMinimo,
    string  SoloContado,    // "Sí" / "No"
    string  Seccion,
    string  Proveedor,
    string  Subcategoria,
    string  Categoria,
    string  Pais,
    string  Marca,
    string  UnidadMedida,
    string  Estado
);

public class ArticulosPagina
{
    public List<FilaArticuloImp> Filas { get; init; } = new();
    public string FechaImp  { get; init; } = "";
    public string Usuario   { get; init; } = "";
    public string Filtro    { get; init; } = "";
    public string Impresora { get; init; } = "";
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
        return candidatos.FirstOrDefault(System.IO.File.Exists)
            ?? System.IO.Path.Combine(baseDir, nombre);
    }
}

public static class ArticulosDibujador
{
    private const int PgW      = 827;
    private const int PgH      = 1169;
    private const int Lx       = 20;
    private const int Pw       = PgW - 40;   // 787
    private const int PieAreaY = PgH - 42;
    private const int PieH     = 18;

    // Cada artículo ocupa 2 filas de 16px cada una (32px total) + 1px separador
    private const int FilaH    = 16;
    private const int ArtH     = FilaH * 2 + 3;  // 35px por artículo
    private const int HdrH     = 22;
    private const int BodyStartY = 90 + 2 + HdrH + 3;

    // Cabeceras fila 1: Código | Nombre/Descripción | Gravada | IVA | Máx.C | S.Min | S.Cont
    // Anchos fila 1: 80 + 252 + 52 + 42 + 42 + 52 + 52 = 572  → resto para estado = 215
    // Realmente usamos todo el ancho Pw=787 con dos bloques apilados:
    // Fila 1: Código(80) | Descripción(272) | Gravada(52) | IVA(42) | Máx.C(45) | S.Min(50) | S.Cont(50) | Estado(70) | blank(126)
    // Pero es mejor separar en dos líneas horizontales completas:
    // Línea 1: [Código 85] [Descripción 330] [Gravada 60] [IVA 48] [Máx.C 50] [S.Min 55] [S.Cont 60] = 688 → + estado 99 = 787
    // Línea 2: [Sección 110] [Proveedor 145] [Subcategoría 130] [Categoría 115] [País 80] [Marca 115] [U.Medida 92] = 787

    // Anchos línea 1
    private static readonly int[] Cw1 = { 85, 295, 60, 48, 55, 60, 60, 124 };
    //                                   Cod  Desc  Grav IVA MaxC SMin SCnt Estado
    // Suma: 85+295+60+48+55+60+60+124 = 787 ✓

    // Anchos línea 2
    private static readonly int[] Cw2 = { 110, 145, 130, 115, 80, 115, 92 };
    //                                   Secc  Prov  Sub   Cat  País Marc UMed
    // Suma: 110+145+130+115+80+115+92 = 787 ✓

    private static float[] ColX(int[] cws)
    {
        var x = new float[cws.Length];
        float acc = Lx;
        for (int i = 0; i < cws.Length; i++) { x[i] = acc; acc += cws[i]; }
        return x;
    }

    private static readonly float[] Cx1 = ColX(Cw1);
    private static readonly float[] Cx2 = ColX(Cw2);

    // Fuentes
    private static readonly System.Drawing.Font FntHdr  = new("Arial", 6.5f, System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntRow1 = new("Arial", 7f,   System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntRow2 = new("Arial", 6.5f, System.Drawing.FontStyle.Italic);
    private static readonly System.Drawing.Font FntFoot = new("Arial", 7.5f, System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntPag  = new("Arial", 6.5f, System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntTit  = new("Arial", 12f,  System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntSub  = new("Arial", 7f,   System.Drawing.FontStyle.Regular);

    // Pinceles
    private static readonly System.Drawing.SolidBrush BRojo    = new(System.Drawing.Color.FromArgb(210,  0,   0));
    private static readonly System.Drawing.SolidBrush BNaranja = new(System.Drawing.Color.FromArgb(230, 81,   0));
    private static readonly System.Drawing.SolidBrush BSubTxt  = new(System.Drawing.Color.FromArgb( 80, 80, 100));
    private static readonly System.Drawing.SolidBrush BSubBg   = new(System.Drawing.Color.FromArgb(245,245, 248));
    private static readonly System.Drawing.SolidBrush BPagNum  = new(System.Drawing.Color.FromArgb(120,120, 140));
    private static readonly System.Drawing.SolidBrush BLbl2    = new(System.Drawing.Color.FromArgb( 90, 90, 110));
    private static readonly System.Drawing.SolidBrush BAlt     = new(System.Drawing.Color.FromArgb(248,249, 252));

    // Plumas
    private static readonly System.Drawing.Pen PenRojo    = new(System.Drawing.Color.FromArgb(210,  0,  0), 3.5f);
    private static readonly System.Drawing.Pen PenRojo1   = new(System.Drawing.Color.FromArgb(210,  0,  0), 1.0f);
    private static readonly System.Drawing.Pen PenAzul15  = new(System.Drawing.Color.FromArgb( 13, 71,161), 1.5f);
    private static readonly System.Drawing.Pen PenGris190 = new(System.Drawing.Color.FromArgb(190,190,190), 1.0f);
    private static readonly System.Drawing.Pen PenGris05  = new(System.Drawing.Color.FromArgb(190,190,190), 0.5f);
    private static readonly System.Drawing.Pen PenHdrTop  = new(System.Drawing.Color.FromArgb(160,160,160), 0.8f);
    private static readonly System.Drawing.Pen PenSep     = new(System.Drawing.Color.FromArgb(180,200,220), 0.8f);
    private static readonly System.Drawing.Pen PenPieLn   = new(System.Drawing.Color.FromArgb(160,160,160), 0.8f);
    private static readonly System.Drawing.Pen PenSubBd   = new(System.Drawing.Color.FromArgb(220,220,230), 0.8f);
    private static readonly System.Drawing.Pen PenHdrCol  = new(System.Drawing.Color.FromArgb(180,200,220), 0.5f);

    // Colores fondo cabecera de las dos líneas
    private static readonly System.Drawing.SolidBrush BHdr1Bg = new(System.Drawing.Color.FromArgb(13, 71, 161));   // azul oscuro
    private static readonly System.Drawing.SolidBrush BHdr2Bg = new(System.Drawing.Color.FromArgb(40, 95, 180));   // azul medio

    public static int ContarPaginas(ArticulosPagina p)
    {
        if (p.Filas.Count == 0) return 1;
        int area = PieAreaY - BodyStartY;
        int porPag = area / ArtH;
        return (p.Filas.Count + porPag - 1) / porPag;
    }

    public static bool DibujarPagina(
        System.Drawing.Graphics g,
        ArticulosPagina p,
        System.Drawing.Image? logo,
        ref int filaOffset,
        int pageNum,
        int totalPages)
    {
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.None;

        // ── Marca de agua ────────────────────────────────────────────────────
        if (logo != null)
        {
            float wmW = 500f, wmH = logo.Height * (wmW / logo.Width);
            DrawConOpacidad(g, logo,
                (int)(PgW / 2f - wmW / 2f),
                (int)(PgH / 2f - wmH / 2f + 40f),
                (int)wmW, (int)wmH, 0.055f);
        }

        // ── Encabezado ───────────────────────────────────────────────────────
        g.FillRectangle(System.Drawing.Brushes.White, 0, 0, PgW, 88);
        g.DrawLine(PenRojo, 0, 2, PgW, 2);

        float tx = Lx, tpw = Pw;
        if (logo != null)
        {
            float logoH2 = 62f, logoW2 = logo.Width * (logoH2 / logo.Height);
            DrawConOpacidad(g, logo, Lx, 8, (int)logoW2, (int)logoH2, 1f);
            float sepX = Lx + logoW2 + 12f;
            g.DrawLine(PenGris190, sepX, 6, sepX, 78);
            tx = sepX + 10f; tpw = Pw - (sepX - Lx) - 10f;
        }
        DibujarBandaTitulo(g, tx, tpw);
        DibujarSubtitulos(g, p, tx, tpw);

        g.DrawLine(PenRojo,  0, 80, PgW, 80);
        g.DrawLine(PenGris05, 0, 82, PgW, 82);

        int y = 90;

        // ── Cabecera doble ────────────────────────────────────────────────────
        g.DrawLine(PenHdrTop, Lx, y, Lx + Pw, y);
        y += 2;

        // Fila cabecera 1 (azul oscuro)
        g.FillRectangle(BHdr1Bg, Lx, y, Pw, FilaH);
        string[] hdrs1 = { "Código", "Nombre / Descripción", "Gravada", "IVA", "Máx.C", "S.Min", "S.Cont", "Estado" };
        DibujarCeldas(g, hdrs1, Cw1, Cx1, y, FntHdr, System.Drawing.Brushes.White, FilaH, new bool[hdrs1.Length]);
        y += FilaH;

        // Fila cabecera 2 (azul medio)
        g.FillRectangle(BHdr2Bg, Lx, y, Pw, FilaH);
        string[] hdrs2 = { "Sección", "Proveedor", "Subcategoría", "Categoría", "País", "Marca", "Unidad de Medida" };
        DibujarCeldas(g, hdrs2, Cw2, Cx2, y, FntHdr, System.Drawing.Brushes.White, FilaH, new bool[hdrs2.Length]);
        y += FilaH;

        g.DrawLine(PenAzul15, Lx, y, Lx + Pw, y);
        y += 3;

        // ── Artículos ─────────────────────────────────────────────────────────
        bool  hayMas = false;
        int   limY   = PieAreaY - 4;
        int   artIdx = 0;

        while (filaOffset < p.Filas.Count)
        {
            if (y + ArtH > limY) { hayMas = true; break; }

            var f = p.Filas[filaOffset++];

            // Fondo alternado
            if (artIdx % 2 == 0)
                g.FillRectangle(BAlt, Lx, y, Pw, ArtH - 1);

            var bEst = f.Estado == "Inactivo" ? BRojo : System.Drawing.Brushes.Black;

            // Línea 1: datos de identificación
            string[] vals1 = { f.Codigo, f.Descripcion, f.Gravada, f.Iva, f.MaxCuota, f.StockMinimo, f.SoloContado, f.Estado };
            System.Drawing.Brush[] brs1 = {
                System.Drawing.Brushes.Black, System.Drawing.Brushes.Black,
                System.Drawing.Brushes.Black, System.Drawing.Brushes.Black,
                System.Drawing.Brushes.Black, System.Drawing.Brushes.Black,
                System.Drawing.Brushes.Black, bEst
            };
            DibujarCeldasColor(g, vals1, Cw1, Cx1, y, FntRow1, brs1, FilaH);
            y += FilaH;

            // Línea 2: datos de clasificación (texto secundario)
            string[] vals2 = { f.Seccion, f.Proveedor, f.Subcategoria, f.Categoria, f.Pais, f.Marca, f.UnidadMedida };
            DibujarCeldas(g, vals2, Cw2, Cx2, y, FntRow2, BLbl2, FilaH, new bool[vals2.Length]);
            y += FilaH;

            // Separador entre artículos
            g.DrawLine(PenSep, Lx, y + 1, Lx + Pw, y + 1);
            y += 3;
            artIdx++;
        }

        // ── Pie ───────────────────────────────────────────────────────────────
        g.DrawLine(hayMas ? PenPieLn : PenAzul15, Lx, PieAreaY, Lx + Pw, PieAreaY);

        float fhFoot = FntFoot.GetHeight(g);
        float footY  = PieAreaY + 4 + Math.Max(0f, (PieH - fhFoot) / 2f);
        float partW  = Pw / 3f;

        if (!hayMas)
        {
            g.DrawString($"Artículos: {p.Filas.Count}", FntFoot, System.Drawing.Brushes.Black, Lx + 2f, footY);
            int activos = p.Filas.Count(f => f.Estado == "Activo");
            DrawCentrado(g, $"Activos: {activos}   Inactivos: {p.Filas.Count - activos}",
                FntFoot, System.Drawing.Brushes.Black, Lx + partW, footY, partW, PieH);
            for (int i = 1; i < 3; i++)
                g.DrawLine(PenPieLn, Lx + i * partW, PieAreaY + 2, Lx + i * partW, PieAreaY + PieH + 2);
        }

        string pagTxt = $"Página {pageNum} de {totalPages}";
        var szPag = g.MeasureString(pagTxt, FntPag);
        g.DrawString(pagTxt, FntPag, BPagNum, Lx + Pw - szPag.Width, PgH - 18);

        return hayMas;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void DibujarCeldas(
        System.Drawing.Graphics g, string[] txts,
        int[] cws, float[] cx, int y,
        System.Drawing.Font fnt, System.Drawing.Brush br,
        int rowH, bool[] _)
    {
        float fh = fnt.GetHeight(g), ty = y + Math.Max(0f, (rowH - fh) / 2f);
        for (int i = 0; i < txts.Length; i++)
        {
            string t = txts[i];
            float cW = cws[i], maxW = cW - 4f;
            Trunc(g, ref t, fnt, maxW);
            g.DrawString(t, fnt, br, cx[i] + 2f, ty);
        }
    }

    private static void DibujarCeldasColor(
        System.Drawing.Graphics g, string[] txts,
        int[] cws, float[] cx, int y,
        System.Drawing.Font fnt, System.Drawing.Brush[] brs,
        int rowH)
    {
        float fh = fnt.GetHeight(g), ty = y + Math.Max(0f, (rowH - fh) / 2f);
        for (int i = 0; i < txts.Length; i++)
        {
            string t = txts[i];
            Trunc(g, ref t, fnt, cws[i] - 4f);
            g.DrawString(t, fnt, brs[i], cx[i] + 2f, ty);
        }
    }

    private static void Trunc(System.Drawing.Graphics g, ref string t, System.Drawing.Font f, float maxW)
    {
        if (string.IsNullOrEmpty(t) || g.MeasureString(t, f).Width <= maxW) return;
        float elW = g.MeasureString("…", f).Width;
        while (t.Length > 0 && g.MeasureString(t, f).Width + elW > maxW)
            t = t[..^1];
        t += "…";
    }

    private static void DibujarBandaTitulo(System.Drawing.Graphics g, float tx, float tpw)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        const float r5 = 5f;
        path.AddArc(tx,              8,              r5*2, r5*2, 180, 90);
        path.AddArc(tx+tpw-r5*2,    8,              r5*2, r5*2, 270, 90);
        path.AddArc(tx+tpw-r5*2,    8+36-r5*2,      r5*2, r5*2,   0, 90);
        path.AddArc(tx,              8+36-r5*2,      r5*2, r5*2,  90, 90);
        path.CloseFigure();
        g.FillPath(BNaranja, path);
        g.FillRectangle(BRojo, tx, 8, 4, 36);
        DrawCentrado(g, "LISTADO DE ARTÍCULOS", FntTit, System.Drawing.Brushes.White, tx+8, 8, tpw-12, 36);
    }

    private static void DibujarSubtitulos(System.Drawing.Graphics g, ArticulosPagina p, float tx, float tpw)
    {
        g.DrawLine(PenRojo1, tx, 50, tx+tpw, 50);
        g.FillRectangle(BSubBg,  tx, 53, tpw, 24f);
        g.DrawRectangle(PenSubBd, tx, 53, tpw, 24f);
        var sub1 = string.IsNullOrEmpty(p.Filtro) ? "Todos los artículos" : $"Filtro: {p.Filtro}";
        DrawCentrado(g, sub1,                                      FntSub, BSubTxt, tx+4, 53, tpw-8, 12);
        DrawCentrado(g, $"Fecha: {p.FechaImp}   ●   Usuario: {p.Usuario}", FntSub, BSubTxt, tx+4, 65, tpw-8, 12);
    }

    private static void DrawCentrado(
        System.Drawing.Graphics g, string txt, System.Drawing.Font f,
        System.Drawing.Brush br, float x, float y, float w, float h)
    {
        var sz = g.MeasureString(txt, f);
        g.DrawString(txt, f, br,
            x + Math.Max(0f, (w - sz.Width) / 2f),
            y + Math.Max(0f, (h - sz.Height) / 2f));
    }

    private static void DrawConOpacidad(
        System.Drawing.Graphics g, System.Drawing.Image img,
        int x, int y, int w, int h, float opacity)
    {
        var cm = new System.Drawing.Imaging.ColorMatrix { Matrix33 = opacity };
        using var ia = new System.Drawing.Imaging.ImageAttributes();
        ia.SetColorMatrix(cm, System.Drawing.Imaging.ColorMatrixFlag.Default, System.Drawing.Imaging.ColorAdjustType.Bitmap);
        g.DrawImage(img, new System.Drawing.Rectangle(x, y, w, h),
            0, 0, img.Width, img.Height, System.Drawing.GraphicsUnit.Pixel, ia);
    }
}
