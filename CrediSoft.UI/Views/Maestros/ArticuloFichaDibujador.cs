namespace CrediSoft.UI.Views.Maestros;

public record ArticuloFicha(
    // Identificación
    string Codigo,
    string Descripcion,
    string Serial,
    string Presentacion,
    string Gravada,
    string Iva,
    string MaxCuota,
    string StockMinimo,
    string SoloContado,
    // Clasificación
    string Seccion,
    string Proveedor,
    string Subcategoria,
    string Categoria,
    string Pais,
    string Marca,
    string UnidadMedida,
    // Vencimiento
    string Vencimiento,
    // Meta
    string FechaImp,
    string Usuario,
    string LogoPath,
    string Impresora
);

public static class ArticuloFichaDibujador
{
    private const int PgW = 827, PgH = 1169, Lx = 20, Pw = 787;

    private static readonly System.Drawing.Font FntTit   = new("Arial", 12f, System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntSub   = new("Arial",  7f, System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntSec   = new("Arial",  8f, System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntLbl   = new("Arial",  7.5f, System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntVal   = new("Arial",  7.5f, System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntPag   = new("Arial",  6.5f, System.Drawing.FontStyle.Regular);

    private static readonly System.Drawing.SolidBrush BNaranja = new(System.Drawing.Color.FromArgb(230, 81, 0));
    private static readonly System.Drawing.SolidBrush BRojo    = new(System.Drawing.Color.FromArgb(210,   0, 0));
    private static readonly System.Drawing.SolidBrush BSubTxt  = new(System.Drawing.Color.FromArgb( 80,  80, 100));
    private static readonly System.Drawing.SolidBrush BSubBg   = new(System.Drawing.Color.FromArgb(245, 245, 248));
    private static readonly System.Drawing.SolidBrush BSecHdr  = new(System.Drawing.Color.FromArgb( 52,  73,  94));
    private static readonly System.Drawing.SolidBrush BLbl     = new(System.Drawing.Color.FromArgb( 80,  80,  80));
    private static readonly System.Drawing.SolidBrush BVal     = new(System.Drawing.Color.FromArgb( 20,  20,  20));
    private static readonly System.Drawing.SolidBrush BPagNum  = new(System.Drawing.Color.FromArgb(120, 120, 140));
    private static readonly System.Drawing.SolidBrush BRowBg   = new(System.Drawing.Color.FromArgb(248, 249, 252));
    private static readonly System.Drawing.SolidBrush BAltBg   = new(System.Drawing.Color.FromArgb(255, 255, 255));

    private static readonly System.Drawing.Pen PenRojo    = new(System.Drawing.Color.FromArgb(210,   0,  0), 3.5f);
    private static readonly System.Drawing.Pen PenGris190 = new(System.Drawing.Color.FromArgb(190, 190, 190), 1f);
    private static readonly System.Drawing.Pen PenGris05  = new(System.Drawing.Color.FromArgb(190, 190, 190), 0.5f);
    private static readonly System.Drawing.Pen PenBorder  = new(System.Drawing.Color.FromArgb(210, 215, 220), 0.8f);
    private static readonly System.Drawing.Pen PenSubBd   = new(System.Drawing.Color.FromArgb(220, 220, 230), 0.8f);

    public static void DibujarFicha(
        System.Drawing.Graphics g,
        ArticuloFicha f,
        System.Drawing.Image? logo)
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

        if (logo != null)
        {
            float logoH2 = 62f, logoW2 = logo.Width * (logoH2 / logo.Height);
            DrawConOpacidad(g, logo, Lx, 8, (int)logoW2, (int)logoH2, 1f);
            float sepX = Lx + logoW2 + 12f;
            g.DrawLine(PenGris190, sepX, 6, sepX, 78);
            float tx = sepX + 10f, tpw = Pw - (sepX - Lx) - 10f;
            DibujarBandaTitulo(g, tx, tpw);
            DibujarSubtitulos(g, f, tx, tpw);
        }
        else
        {
            DibujarBandaTitulo(g, Lx, Pw);
            DibujarSubtitulos(g, f, Lx, Pw);
        }

        g.DrawLine(PenRojo,   0, 80, PgW, 80);
        g.DrawLine(PenGris05, 0, 82, PgW, 82);

        int y = 100;

        // ── Sección: Identificación ──────────────────────────────────────────
        y = DibujarSeccionHdr(g, "IDENTIFICACIÓN", y);

        var idFields = new (string Lbl, string Val)[]
        {
            ("Nombre / Descripción", f.Descripcion),
            ("Código",               f.Codigo),
            ("Gravada",              f.Gravada),
            ("IVA (%)",              f.Iva),
            ("Máx. Cuotas",          f.MaxCuota),
            ("Stock Mínimo",         f.StockMinimo),
            ("Solo Contado",         f.SoloContado),
        };
        y = DibujarCampos(g, idFields, y, cols: 2);

        y += 10;

        // ── Sección: Clasificación ───────────────────────────────────────────
        y = DibujarSeccionHdr(g, "CLASIFICACIÓN", y);

        var clasFields = new (string Lbl, string Val)[]
        {
            ("Sección",          f.Seccion),
            ("Proveedor",        f.Proveedor),
            ("Subcategoría",     f.Subcategoria),
            ("Categoría",        f.Categoria),
            ("País",             f.Pais),
            ("Marca",            f.Marca),
            ("Unidad de Medida", f.UnidadMedida),
        };
        y = DibujarCampos(g, clasFields, y, cols: 2);

        if (!string.IsNullOrWhiteSpace(f.Vencimiento) && f.Vencimiento != "Imperecedero")
        {
            y += 10;
            y = DibujarSeccionHdr(g, "VENCIMIENTO", y);
            y = DibujarCampos(g, new[] { ("Fecha de vencimiento", f.Vencimiento) }, y, cols: 1);
        }

        // ── Número de página ─────────────────────────────────────────────────
        string pagTxt = "Página 1 de 1";
        var szPag = g.MeasureString(pagTxt, FntPag);
        g.DrawString(pagTxt, FntPag, BPagNum, Lx + Pw - szPag.Width, PgH - 18);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static int DibujarSeccionHdr(System.Drawing.Graphics g, string titulo, int y)
    {
        g.FillRectangle(BSecHdr, Lx, y, Pw, 20);
        float fh = FntSec.GetHeight(g);
        g.DrawString(titulo, FntSec, System.Drawing.Brushes.White, Lx + 8f, y + (20 - fh) / 2f);
        return y + 22;
    }

    private static int DibujarCampos(
        System.Drawing.Graphics g,
        (string Lbl, string Val)[] fields,
        int startY,
        int cols)
    {
        const int RowH   = 22;
        const int PadX   = 8;
        float     colW   = (float)Pw / cols;
        float     lblW   = colW * 0.38f;
        float     valW   = colW * 0.58f;
        float     fhLbl  = FntLbl.GetHeight(g);
        float     fhVal  = FntVal.GetHeight(g);
        float     tyLbl  = (RowH - fhLbl) / 2f;
        float     tyVal  = (RowH - fhVal) / 2f;

        int y = startY;
        for (int i = 0; i < fields.Length; i++)
        {
            int col = i % cols;
            int row = i / cols;
            float rx = Lx + col * colW;
            float ry = y + row * RowH;

            // Fondo alternado por fila
            var bg = (row % 2 == 0) ? BRowBg : BAltBg;
            g.FillRectangle(bg, rx, ry, colW, RowH);
            g.DrawRectangle(PenBorder, rx, ry, colW, RowH);

            // Label
            string lbl = fields[i].Lbl;
            g.DrawString(lbl, FntLbl, BLbl, rx + PadX, ry + tyLbl);

            // Valor (truncado si hace falta)
            string val = fields[i].Val;
            float vx   = rx + lblW + PadX;
            float maxVW = valW - PadX;
            if (g.MeasureString(val, FntVal).Width > maxVW)
            {
                float elW = g.MeasureString("…", FntVal).Width;
                while (val.Length > 0 && g.MeasureString(val, FntVal).Width + elW > maxVW)
                    val = val[..^1];
                val += "…";
            }
            g.DrawString(val, FntVal, BVal, vx, ry + tyVal);
        }

        int rowCount = (int)Math.Ceiling((double)fields.Length / cols);
        return startY + rowCount * RowH;
    }

    private static void DibujarBandaTitulo(System.Drawing.Graphics g, float tx, float tpw)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        const float r5 = 5f;
        path.AddArc(tx,               8,              r5*2, r5*2, 180, 90);
        path.AddArc(tx + tpw - r5*2, 8,              r5*2, r5*2, 270, 90);
        path.AddArc(tx + tpw - r5*2, 8 + 36 - r5*2, r5*2, r5*2,   0, 90);
        path.AddArc(tx,               8 + 36 - r5*2, r5*2, r5*2,  90, 90);
        path.CloseFigure();
        g.FillPath(BNaranja, path);
        g.FillRectangle(BRojo, tx, 8, 4, 36);
        DrawCentrado(g, "FICHA DE ARTÍCULO", FntTit, System.Drawing.Brushes.White, tx + 8, 8, tpw - 12, 36);
    }

    private static void DibujarSubtitulos(System.Drawing.Graphics g, ArticuloFicha f, float tx, float tpw)
    {
        g.DrawLine(new System.Drawing.Pen(System.Drawing.Color.FromArgb(210, 0, 0), 1f), tx, 50, tx + tpw, 50);
        g.FillRectangle(BSubBg, tx, 53, tpw, 24f);
        g.DrawRectangle(PenSubBd, tx, 53, tpw, 24f);
        var sub2 = $"Fecha: {f.FechaImp}   ●   Usuario: {f.Usuario}";
        DrawCentrado(g, f.Descripcion, FntSub, BSubTxt, tx + 4, 53, tpw - 8, 12);
        DrawCentrado(g, sub2,          FntSub, BSubTxt, tx + 4, 65, tpw - 8, 12);
    }

    private static void DrawCentrado(
        System.Drawing.Graphics g, string txt, System.Drawing.Font fnt,
        System.Drawing.Brush br, float x, float y, float w, float h)
    {
        var sz = g.MeasureString(txt, fnt);
        g.DrawString(txt, fnt, br,
            x + Math.Max(0f, (w - sz.Width) / 2f),
            y + Math.Max(0f, (h - sz.Height) / 2f));
    }

    private static void DrawConOpacidad(
        System.Drawing.Graphics g, System.Drawing.Image img,
        int x, int y, int w, int h, float opacity)
    {
        var cm = new System.Drawing.Imaging.ColorMatrix { Matrix33 = opacity };
        using var ia = new System.Drawing.Imaging.ImageAttributes();
        ia.SetColorMatrix(cm,
            System.Drawing.Imaging.ColorMatrixFlag.Default,
            System.Drawing.Imaging.ColorAdjustType.Bitmap);
        g.DrawImage(img, new System.Drawing.Rectangle(x, y, w, h),
            0, 0, img.Width, img.Height, System.Drawing.GraphicsUnit.Pixel, ia);
    }
}
