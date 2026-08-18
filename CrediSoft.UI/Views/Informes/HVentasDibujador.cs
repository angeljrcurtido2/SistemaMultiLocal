namespace CrediSoft.UI.Views.Informes;

// ── Datos ──────────────────────────────────────────────────────────────────
public class HVentasFilaDetalle
{
    public string  Local     { get; init; } = "";
    public string  Vendedor  { get; init; } = "";
    public string  Solicitud { get; init; } = "";
    public string  Tipo      { get; init; } = "";
    public string  Cliente   { get; init; } = "";
    public decimal Total     { get; init; }
    public decimal Entrega   { get; init; }
    public decimal Saldo     { get; init; }
    public string  Estado    { get; init; } = "";
    public string  Fecha     { get; init; } = "";
}

public class HVentasFilaResumen
{
    public string  Local     { get; init; } = "";
    public string  Vendedor  { get; init; } = "";
    public string  Solicitud { get; init; } = "";
    public decimal Total     { get; init; }
    public decimal Entrega   { get; init; }
    public decimal Debe      { get; init; }
    public decimal Haber     { get; init; }
    public decimal Saldo     { get; init; }
}

public class HVentasPagina
{
    public List<HVentasFilaDetalle>  Detalle   { get; init; } = new();
    public List<HVentasFilaResumen>  Resumen   { get; init; } = new();
    public decimal SumTotal   { get; init; }
    public decimal SumEntrega { get; init; }
    public decimal SumSaldo   { get; init; }
    public int     Cantidad   { get; init; }
    public string  Filtro     { get; init; } = "";
    public string  FechaImp   { get; init; } = "";
    public string  Usuario    { get; init; } = "";
    public string  LogoPath   { get; init; } = "";
}

// ── Elemento de renderizado (filas lógicas del dibujador) ──────────────────
public enum HVKind { GrpHeader, DataRow, Subtotal, GenTotal }

public class HVItem
{
    public HVKind   Kind      { get; init; }
    public string   Local     { get; init; } = "";
    public int      GrpCount  { get; init; }  // solo GrpHeader
    // campos datos
    public string   Vendedor  { get; init; } = "";
    public string   Solicitud { get; init; } = "";
    public string   Tipo      { get; init; } = "";
    public string   Cliente   { get; init; } = "";
    public decimal  Total     { get; init; }
    public decimal  Entrega   { get; init; }
    public decimal  Debe      { get; init; }
    public decimal  Haber     { get; init; }
    public decimal  Saldo     { get; init; }
    public string   Estado    { get; init; } = "";
    public string   Fecha     { get; init; } = "";
    public int      ZebraIdx  { get; init; }  // para zebra solo en DataRow
}

// ── Dibujador Detalle (LANDSCAPE 1169×827) ─────────────────────────────────
public static class HVentasDetalleDibujador
{
    private const int PgW      = 1169;
    private const int PgH      = 827;
    private const int Lx       = 20;
    private const int Pw       = PgW - 40;   // 1129
    private const int PieAreaY = PgH - 38;
    private const int FilaH    = 21;
    private const int GrpH     = 19;  // altura encabezado de grupo
    private const int SubH     = 19;  // altura fila subtotal
    private const int HdrH     = 22;
    private const int BodyY    = 85 + HdrH + 3;

    // Sin columna Local (se muestra en la banda de grupo)
    // Vendedor|Solicitud|Tipo|Cliente|Total|Entrega|Saldo|Fecha
    // 185+130+80+255+120+115+115+129 = 1129
    private static readonly int[]    Cw  = { 185, 130, 80, 255, 120, 115, 115, 129 };
    private static readonly float[]  Cx  = ComputeCx();
    private static readonly string[] Hdr = { "Vendedor", "Nº Sol.", "Tipo", "Cliente",
                                              "Total Gs.", "Entrega Gs.", "Saldo Gs.", "Fecha" };

    private static float[] ComputeCx()
    {
        var cx = new float[Cw.Length];
        cx[0] = Lx;
        for (int i = 1; i < Cw.Length; i++) cx[i] = cx[i-1] + Cw[i-1];
        return cx;
    }

    private static readonly System.Drawing.Font FntTit   = new("Arial", 11f,  System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntSub   = new("Arial",  6.5f,System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntHdr   = new("Arial",  7.5f,System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntGrp   = new("Arial",  7.5f,System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntRow   = new("Arial",  7.5f,System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntRowB  = new("Arial",  7.5f,System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntSbtB  = new("Arial",  7f,  System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntFoot  = new("Arial",  7f,  System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntPag   = new("Arial",  6f,  System.Drawing.FontStyle.Regular);

    private static readonly System.Drawing.SolidBrush BAzul    = new(System.Drawing.Color.FromArgb( 26, 82,118));
    private static readonly System.Drawing.SolidBrush BAzulCl  = new(System.Drawing.Color.FromArgb( 52,152,219));
    private static readonly System.Drawing.SolidBrush BRojo    = new(System.Drawing.Color.FromArgb(192, 57, 43));
    private static readonly System.Drawing.SolidBrush BSubTxt  = new(System.Drawing.Color.FromArgb( 80, 80,100));
    private static readonly System.Drawing.SolidBrush BSubBg   = new(System.Drawing.Color.FromArgb(245,245,248));
    private static readonly System.Drawing.SolidBrush BPagNum  = new(System.Drawing.Color.FromArgb(120,120,140));
    private static readonly System.Drawing.SolidBrush BAlt     = new(System.Drawing.Color.FromArgb(235,245,255));
    private static readonly System.Drawing.SolidBrush BGrpBg   = new(System.Drawing.Color.FromArgb(214,234,248));
    private static readonly System.Drawing.SolidBrush BGrpFg   = new(System.Drawing.Color.FromArgb( 26, 82,118));
    private static readonly System.Drawing.SolidBrush BSbtBg   = new(System.Drawing.Color.FromArgb(232,245,253));
    private static readonly System.Drawing.SolidBrush BSbtFg   = new(System.Drawing.Color.FromArgb( 21, 67, 96));
    private static readonly System.Drawing.SolidBrush BPend    = new(System.Drawing.Color.FromArgb(192, 57, 43));
    private static readonly System.Drawing.SolidBrush BTotBg   = new(System.Drawing.Color.FromArgb( 26, 82,118));

    private static readonly System.Drawing.Pen PenAzul3  = new(System.Drawing.Color.FromArgb( 26, 82,118), 3f);
    private static readonly System.Drawing.Pen PenAzul1  = new(System.Drawing.Color.FromArgb( 26, 82,118), 1f);
    private static readonly System.Drawing.Pen PenGris19 = new(System.Drawing.Color.FromArgb(190,190,190), 1f);
    private static readonly System.Drawing.Pen PenGris05 = new(System.Drawing.Color.FromArgb(190,190,190), 0.5f);
    private static readonly System.Drawing.Pen PenAzul15 = new(System.Drawing.Color.FromArgb( 26, 82,118), 1.5f);
    private static readonly System.Drawing.Pen PenGrpBot = new(System.Drawing.Color.FromArgb(174,214,241), 1f);
    private static readonly System.Drawing.Pen PenSep    = new(System.Drawing.Color.FromArgb(200,220,240), 0.5f);
    private static readonly System.Drawing.Pen PenHdrTop = new(System.Drawing.Color.FromArgb(160,160,160), 0.8f);
    private static readonly System.Drawing.Pen PenSubBd  = new(System.Drawing.Color.FromArgb(220,220,230), 0.8f);

    // Construye la lista plana de ítems a renderizar (grupos + subtotales + total)
    public static List<HVItem> BuildItems(HVentasPagina p)
    {
        var items = new List<HVItem>();
        var grupos = p.Detalle.GroupBy(f => f.Local, StringComparer.OrdinalIgnoreCase);
        foreach (var grp in grupos)
        {
            var filas = grp.ToList();
            items.Add(new HVItem { Kind = HVKind.GrpHeader, Local = grp.Key, GrpCount = filas.Count });
            int z = 0;
            foreach (var f in filas)
            {
                items.Add(new HVItem
                {
                    Kind = HVKind.DataRow, Local = grp.Key,
                    Vendedor = f.Vendedor, Solicitud = f.Solicitud, Tipo = f.Tipo,
                    Cliente = f.Cliente, Total = f.Total, Entrega = f.Entrega,
                    Saldo = f.Saldo, Estado = f.Estado, Fecha = f.Fecha,
                    ZebraIdx = z++,
                });
            }
            items.Add(new HVItem
            {
                Kind = HVKind.Subtotal, Local = grp.Key,
                Total   = filas.Sum(f => f.Total),
                Entrega = filas.Sum(f => f.Entrega),
                Saldo   = filas.Sum(f => f.Saldo),
                GrpCount= filas.Count,
            });
        }
        // fila total general
        items.Add(new HVItem
        {
            Kind    = HVKind.GenTotal,
            Total   = p.SumTotal,
            Entrega = p.SumEntrega,
            Saldo   = p.SumSaldo,
            GrpCount= p.Cantidad,
        });
        return items;
    }

    private static int ItemH(HVItem it) => it.Kind switch
    {
        HVKind.GrpHeader => GrpH,
        HVKind.Subtotal  => SubH,
        HVKind.GenTotal  => SubH + 2,
        _                => FilaH,
    };

    public static int ContarPaginas(List<HVItem> items)
    {
        int area = PieAreaY - BodyY, used = 0, pages = 1;
        foreach (var it in items)
        {
            int h = ItemH(it);
            if (used + h > area) { pages++; used = 0; }
            used += h;
        }
        return pages;
    }

    public static void DibujarPagina(
        System.Drawing.Graphics g,
        HVentasPagina p,
        List<HVItem> items,
        System.Drawing.Image? logo,
        int pageNum, int totalPages,
        ref int itemOffset)
    {
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.None;

        if (logo != null)
        {
            float wmW = 560f, wmH = logo.Height * (wmW / logo.Width);
            DrawImg(g, logo, (int)(PgW/2f-wmW/2f), (int)(PgH/2f-wmH/2f), (int)wmW, (int)wmH, 0.05f);
        }

        // ── Encabezado ────────────────────────────────────────────────────────
        g.FillRectangle(System.Drawing.Brushes.White, 0, 0, PgW, 80);
        g.DrawLine(PenAzul3, 0, 2, PgW, 2);

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
            path.AddArc(tx,         6,       r*2,r*2,180,90);
            path.AddArc(tx+tpw-r*2, 6,       r*2,r*2,270,90);
            path.AddArc(tx+tpw-r*2, 6+30-r*2,r*2,r*2,0,  90);
            path.AddArc(tx,         6+30-r*2,r*2,r*2,90, 90);
            path.CloseFigure();
            g.FillPath(BAzul, path);
        }
        g.FillRectangle(BRojo, tx, 6, 4, 30);
        const string titulo = "HISTORIAL DE VENTAS — DETALLE POR LOCAL";
        var szT = g.MeasureString(titulo, FntTit);
        g.DrawString(titulo, FntTit, System.Drawing.Brushes.White,
            tx + 8f + (tpw - 12f - szT.Width) / 2f, 6f + (30f - szT.Height) / 2f);

        g.DrawLine(PenAzul1, tx, 40f, tx+tpw, 40f);
        g.FillRectangle(BSubBg, tx, 43, tpw, 22f);
        g.DrawRectangle(PenSubBd, tx, 43, tpw, 22f);
        var sub = string.IsNullOrEmpty(p.Filtro) ? "Todas las ventas" : p.Filtro;
        DrawCL(g, sub, FntSub, BSubTxt, tx+4f, 43f, tpw-8f, 11f);
        DrawCL(g, $"Fecha: {p.FechaImp}   ●   Usuario: {p.Usuario}   ●   {p.Cantidad} ventas   ●   Total: Gs. {p.SumTotal:N0}",
            FntSub, BSubTxt, tx+4f, 54f, tpw-8f, 11f);

        g.DrawLine(PenAzul3, 0, 70, PgW, 70);
        g.DrawLine(PenGris05, 0, 72, PgW, 72);

        // ── Cabecera columnas ─────────────────────────────────────────────────
        int y = 80;
        g.DrawLine(PenHdrTop, Lx, y, Lx+Pw, y); y += 2;
        g.FillRectangle(BAzul, Lx, y, Pw, HdrH);
        float fhH = FntHdr.GetHeight(g), tyH = HdrH/2f - fhH/2f;
        for (int i = 0; i < Hdr.Length; i++)
        {
            bool numCol = i >= 4;
            if (numCol)
            {
                var szH = g.MeasureString(Hdr[i], FntHdr);
                g.DrawString(Hdr[i], FntHdr, System.Drawing.Brushes.White,
                    Cx[i] + Cw[i] - szH.Width - 5f, y+tyH);
            }
            else
                g.DrawString(Hdr[i], FntHdr, System.Drawing.Brushes.White, Cx[i]+3f, y+tyH);
        }
        y += HdrH;
        g.DrawLine(PenAzul15, Lx, y, Lx+Pw, y); y += 3;

        // ── Filas ─────────────────────────────────────────────────────────────
        int area = PieAreaY - BodyY;
        float fhR = FntRow.GetHeight(g), tyR = Math.Max(0f, (FilaH - fhR)/2f);

        while (itemOffset < items.Count)
        {
            var it = items[itemOffset];
            int h  = ItemH(it);
            if (y + h > PieAreaY) break;

            switch (it.Kind)
            {
                case HVKind.GrpHeader:
                    g.FillRectangle(BGrpBg, Lx, y, Pw, GrpH);
                    g.DrawLine(PenGrpBot, Lx, y + GrpH, Lx + Pw, y + GrpH);
                    float fhG = FntGrp.GetHeight(g), tyG = GrpH/2f - fhG/2f;
                    // icono cuadrado de color
                    g.FillRectangle(BAzul, Lx+4, y+4, 8, GrpH-8);
                    g.DrawString($"  LOCAL:  {it.Local.ToUpper()}",
                        FntGrp, BGrpFg, Lx + 16f, y + tyG);
                    var cntStr = $"{it.GrpCount} venta(s)";
                    var szCnt  = g.MeasureString(cntStr, FntGrp);
                    g.DrawString(cntStr, FntGrp, BAzulCl, Lx + Pw - szCnt.Width - 8f, y + tyG);
                    y += GrpH;
                    break;

                case HVKind.DataRow:
                    bool pend = it.Estado == "Pendiente";
                    if (it.ZebraIdx % 2 == 1) g.FillRectangle(BAlt, Lx, y, Pw, FilaH);
                    var brush = pend ? (System.Drawing.Brush)BPend : System.Drawing.Brushes.Black;
                    var font  = pend ? FntRowB : FntRow;
                    float ty1 = y + tyR;
                    DC(g,  it.Vendedor,              font, brush, 0, ty1);
                    DC(g,  ShortSol(it.Solicitud),   font, brush, 1, ty1);
                    DC(g,  it.Tipo,                  font, brush, 2, ty1);
                    DC(g,  it.Cliente,               font, brush, 3, ty1);
                    DCR(g, $"{it.Total:N0}",          font, brush, 4, ty1);
                    DCR(g, $"{it.Entrega:N0}",        font, brush, 5, ty1);
                    DCR(g, it.Saldo == 0 ? "—" : $"{it.Saldo:N0}",
                        font, pend ? BPend : System.Drawing.Brushes.Black, 6, ty1);
                    DC(g,  it.Fecha,                 font, brush, 7, ty1);
                    y += FilaH;
                    g.DrawLine(PenSep, Lx, y, Lx+Pw, y);
                    break;

                case HVKind.Subtotal:
                    g.FillRectangle(BSbtBg, Lx, y, Pw, SubH);
                    g.DrawLine(PenAzul1, Lx, y, Lx+Pw, y);
                    float fhS = FntSbtB.GetHeight(g), tyS = SubH/2f - fhS/2f;
                    g.DrawString($"  Subtotal {it.Local}  —  {it.GrpCount} venta(s)",
                        FntSbtB, BSbtFg, Lx + 6f, y + tyS);
                    DCR(g, $"Gs. {it.Total:N0}",   FntSbtB, BSbtFg, 4, y + tyS);
                    DCR(g, $"Gs. {it.Entrega:N0}", FntSbtB, BSbtFg, 5, y + tyS);
                    DCR(g, it.Saldo == 0 ? "—" : $"Gs. {it.Saldo:N0}", FntSbtB, BSbtFg, 6, y + tyS);
                    y += SubH;
                    g.DrawLine(PenGrpBot, Lx, y, Lx+Pw, y);
                    break;

                case HVKind.GenTotal:
                    int totH = SubH + 2;
                    g.FillRectangle(BTotBg, Lx, y, Pw, totH);
                    float fhT = FntSbtB.GetHeight(g), tyT = totH/2f - fhT/2f;
                    g.DrawString($"  TOTAL GENERAL  —  {it.GrpCount} venta(s)",
                        FntSbtB, System.Drawing.Brushes.White, Lx + 6f, y + tyT);
                    DCR(g, $"Gs. {it.Total:N0}",   FntSbtB, System.Drawing.Brushes.White, 4, y + tyT);
                    DCR(g, $"Gs. {it.Entrega:N0}", FntSbtB, System.Drawing.Brushes.White, 5, y + tyT);
                    DCR(g, it.Saldo == 0 ? "—" : $"Gs. {it.Saldo:N0}",
                        FntSbtB, System.Drawing.Brushes.White, 6, y + tyT);
                    y += totH;
                    break;
            }
            itemOffset++;
        }

        // ── Pie ───────────────────────────────────────────────────────────────
        bool esUltima = itemOffset >= items.Count;
        g.DrawLine(esUltima ? PenAzul15 : new System.Drawing.Pen(System.Drawing.Color.FromArgb(160,160,160), 0.8f),
            Lx, PieAreaY, Lx+Pw, PieAreaY);

        var pag = $"Página {pageNum} de {totalPages}";
        var szP = g.MeasureString(pag, FntPag);
        g.DrawString(pag, FntPag, BPagNum, Lx+Pw-szP.Width, PgH-16);
        if (!esUltima)
            g.DrawString("Continúa en la página siguiente →", FntPag, BSubTxt, Lx+2f, PgH-16);
    }

    private static void DC(System.Drawing.Graphics g, string txt, System.Drawing.Font f,
        System.Drawing.Brush b, int col, float ty)
        => g.DrawString(Trunc(g, txt, f, Cw[col]-6f), f, b, Cx[col]+3f, ty);

    private static void DCR(System.Drawing.Graphics g, string txt, System.Drawing.Font f,
        System.Drawing.Brush b, int col, float ty)
    {
        var szT = g.MeasureString(txt, f);
        float rx = Cx[col] + Cw[col] - 5f - szT.Width;
        g.DrawString(txt, f, b, Math.Max(Cx[col]+2f, rx), ty);
    }

    private static void DrawCL(System.Drawing.Graphics g, string t, System.Drawing.Font f,
        System.Drawing.Brush b, float x, float y, float w, float h)
        => g.DrawString(t, f, b, x, y + Math.Max(0f, (h - g.MeasureString(t, f).Height)/2f));

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

// ── Dibujador Resumen (PORTRAIT 827×1169) ──────────────────────────────────
public static class HVentasResumenDibujador
{
    private const int PgW      = 827;
    private const int PgH      = 1169;
    private const int Lx       = 20;
    private const int Pw       = PgW - 40;   // 787
    private const int PieAreaY = PgH - 42;
    private const int FilaH    = 24;
    private const int GrpH     = 20;
    private const int SubH     = 20;
    private const int HdrH     = 24;
    private const int BodyY    = 92 + HdrH + 3;

    // Sin columna Local (se muestra en banda de grupo)
    // Vendedor|Solicitud|Total|Entrega|Debe|Haber|Saldo
    // 185+120+120+100+90+90+82 = 787
    private static readonly int[]    Cw  = { 185, 120, 120, 100, 90, 90, 82 };
    private static readonly float[]  Cx  = ComputeCx();
    private static readonly string[] Hdr = { "Vendedor", "Nº Sol.", "Total Gs.",
                                              "Entrega Gs.", "Debe Gs.", "Haber Gs.", "Saldo Gs." };

    private static float[] ComputeCx()
    {
        var cx = new float[Cw.Length];
        cx[0] = Lx;
        for (int i = 1; i < Cw.Length; i++) cx[i] = cx[i-1] + Cw[i-1];
        return cx;
    }

    private static readonly System.Drawing.Font FntTit  = new("Arial", 12f,  System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntSub  = new("Arial",  7f,  System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntHdr  = new("Arial",  8f,  System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntGrp  = new("Arial",  8f,  System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntRow  = new("Arial",  7.5f,System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntSbtB = new("Arial",  7.5f,System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntFoot = new("Arial",  7.5f,System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntPag  = new("Arial",  6.5f,System.Drawing.FontStyle.Regular);

    private static readonly System.Drawing.SolidBrush BAzul   = new(System.Drawing.Color.FromArgb( 26, 82,118));
    private static readonly System.Drawing.SolidBrush BAzulCl = new(System.Drawing.Color.FromArgb( 52,152,219));
    private static readonly System.Drawing.SolidBrush BRojo   = new(System.Drawing.Color.FromArgb(192, 57, 43));
    private static readonly System.Drawing.SolidBrush BSubTxt = new(System.Drawing.Color.FromArgb( 80, 80,100));
    private static readonly System.Drawing.SolidBrush BSubBg  = new(System.Drawing.Color.FromArgb(245,245,248));
    private static readonly System.Drawing.SolidBrush BPagNum = new(System.Drawing.Color.FromArgb(120,120,140));
    private static readonly System.Drawing.SolidBrush BAlt    = new(System.Drawing.Color.FromArgb(235,245,255));
    private static readonly System.Drawing.SolidBrush BGrpBg  = new(System.Drawing.Color.FromArgb(214,234,248));
    private static readonly System.Drawing.SolidBrush BGrpFg  = new(System.Drawing.Color.FromArgb( 26, 82,118));
    private static readonly System.Drawing.SolidBrush BSbtBg  = new(System.Drawing.Color.FromArgb(232,245,253));
    private static readonly System.Drawing.SolidBrush BSbtFg  = new(System.Drawing.Color.FromArgb( 21, 67, 96));
    private static readonly System.Drawing.SolidBrush BSaldoR = new(System.Drawing.Color.FromArgb(192, 57, 43));
    private static readonly System.Drawing.SolidBrush BTotBg  = new(System.Drawing.Color.FromArgb( 26, 82,118));

    private static readonly System.Drawing.Pen PenAzul3  = new(System.Drawing.Color.FromArgb( 26, 82,118), 3.5f);
    private static readonly System.Drawing.Pen PenAzul1  = new(System.Drawing.Color.FromArgb( 26, 82,118), 1f);
    private static readonly System.Drawing.Pen PenGris19 = new(System.Drawing.Color.FromArgb(190,190,190), 1f);
    private static readonly System.Drawing.Pen PenGris05 = new(System.Drawing.Color.FromArgb(190,190,190), 0.5f);
    private static readonly System.Drawing.Pen PenAzul15 = new(System.Drawing.Color.FromArgb( 26, 82,118), 1.5f);
    private static readonly System.Drawing.Pen PenGrpBot = new(System.Drawing.Color.FromArgb(174,214,241), 1f);
    private static readonly System.Drawing.Pen PenSep    = new(System.Drawing.Color.FromArgb(200,220,240), 0.5f);
    private static readonly System.Drawing.Pen PenSubBd  = new(System.Drawing.Color.FromArgb(220,220,230), 0.8f);
    private static readonly System.Drawing.Pen PenHdrTop = new(System.Drawing.Color.FromArgb(160,160,160), 0.8f);

    public static List<HVItem> BuildItems(HVentasPagina p)
    {
        var items = new List<HVItem>();
        var grupos = p.Resumen.GroupBy(f => f.Local, StringComparer.OrdinalIgnoreCase);
        foreach (var grp in grupos)
        {
            var filas = grp.ToList();
            items.Add(new HVItem { Kind = HVKind.GrpHeader, Local = grp.Key, GrpCount = filas.Count });
            int z = 0;
            foreach (var f in filas)
            {
                items.Add(new HVItem
                {
                    Kind = HVKind.DataRow, Local = grp.Key,
                    Vendedor = f.Vendedor, Solicitud = f.Solicitud,
                    Total = f.Total, Entrega = f.Entrega,
                    Debe = f.Debe, Haber = f.Haber, Saldo = f.Saldo,
                    ZebraIdx = z++,
                });
            }
            items.Add(new HVItem
            {
                Kind    = HVKind.Subtotal, Local = grp.Key,
                Total   = filas.Sum(f => f.Total),
                Entrega = filas.Sum(f => f.Entrega),
                Debe    = filas.Sum(f => f.Debe),
                Haber   = filas.Sum(f => f.Haber),
                Saldo   = filas.Sum(f => f.Saldo),
                GrpCount= filas.Count,
            });
        }
        items.Add(new HVItem
        {
            Kind    = HVKind.GenTotal,
            Total   = p.SumTotal,
            Entrega = p.SumEntrega,
            Saldo   = p.SumSaldo,
            GrpCount= p.Cantidad,
        });
        return items;
    }

    private static int ItemH(HVItem it) => it.Kind switch
    {
        HVKind.GrpHeader => GrpH,
        HVKind.Subtotal  => SubH,
        HVKind.GenTotal  => SubH + 2,
        _                => FilaH,
    };

    public static int ContarPaginas(List<HVItem> items)
    {
        int area = PieAreaY - BodyY, used = 0, pages = 1;
        foreach (var it in items)
        {
            int h = ItemH(it);
            if (used + h > area) { pages++; used = 0; }
            used += h;
        }
        return pages;
    }

    public static void DibujarPagina(
        System.Drawing.Graphics g,
        HVentasPagina p,
        List<HVItem> items,
        System.Drawing.Image? logo,
        int pageNum, int totalPages,
        ref int itemOffset)
    {
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.None;

        if (logo != null)
        {
            float wmW = 500f, wmH = logo.Height * (wmW / logo.Width);
            DrawImg(g, logo, (int)(PgW/2f-wmW/2f), (int)(PgH/2f-wmH/2f+40f), (int)wmW, (int)wmH, 0.055f);
        }

        g.FillRectangle(System.Drawing.Brushes.White, 0, 0, PgW, 88);
        g.DrawLine(PenAzul3, 0, 2, PgW, 2);

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
            path.AddArc(tx,         8,        r*2,r*2,180,90);
            path.AddArc(tx+tpw-r*2, 8,        r*2,r*2,270,90);
            path.AddArc(tx+tpw-r*2, 8+36-r*2, r*2,r*2,  0,90);
            path.AddArc(tx,         8+36-r*2, r*2,r*2, 90,90);
            path.CloseFigure();
            g.FillPath(BAzul, path);
        }
        g.FillRectangle(BRojo, tx, 8, 4, 36);
        const string titulo = "HISTORIAL DE VENTAS — RESUMEN POR LOCAL";
        var szT = g.MeasureString(titulo, FntTit);
        g.DrawString(titulo, FntTit, System.Drawing.Brushes.White,
            tx + 8f + (tpw - 12f - szT.Width)/2f, 8f + (36f - szT.Height)/2f);

        g.DrawLine(PenAzul1, tx, 50, tx+tpw, 50);
        g.FillRectangle(BSubBg, tx, 53, tpw, 24f);
        g.DrawRectangle(PenSubBd, tx, 53, tpw, 24f);
        var sub = string.IsNullOrEmpty(p.Filtro) ? "Todas las ventas" : p.Filtro;
        DrawC(g, sub, FntSub, BSubTxt, tx+4f, 53f, tpw-8f, 12f);
        DrawC(g, $"Fecha: {p.FechaImp}   ●   Usuario: {p.Usuario}   ●   {p.Cantidad} ventas",
            FntSub, BSubTxt, tx+4f, 65f, tpw-8f, 12f);

        g.DrawLine(PenAzul3, 0, 80, PgW, 80);
        g.DrawLine(PenGris05, 0, 82, PgW, 82);

        // Cabecera columnas
        int y = 90;
        g.DrawLine(PenHdrTop, Lx, y, Lx+Pw, y); y += 2;
        g.FillRectangle(BAzul, Lx, y, Pw, HdrH);
        float fhH = FntHdr.GetHeight(g), tyH = HdrH/2f - fhH/2f;
        g.DrawString(Hdr[0], FntHdr, System.Drawing.Brushes.White, Cx[0]+4f, y+tyH);
        g.DrawString(Hdr[1], FntHdr, System.Drawing.Brushes.White, Cx[1]+3f, y+tyH);
        for (int i = 2; i < Hdr.Length; i++)
        {
            var szH = g.MeasureString(Hdr[i], FntHdr);
            g.DrawString(Hdr[i], FntHdr, System.Drawing.Brushes.White,
                Cx[i] + Cw[i] - szH.Width - 5f, y+tyH);
        }
        y += HdrH;
        g.DrawLine(PenAzul15, Lx, y, Lx+Pw, y); y += 3;

        // ── Filas ─────────────────────────────────────────────────────────────
        float fhR = FntRow.GetHeight(g), tyR = Math.Max(0f, (FilaH - fhR)/2f);

        while (itemOffset < items.Count)
        {
            var it = items[itemOffset];
            int h  = ItemH(it);
            if (y + h > PieAreaY) break;

            switch (it.Kind)
            {
                case HVKind.GrpHeader:
                    g.FillRectangle(BGrpBg, Lx, y, Pw, GrpH);
                    g.DrawLine(PenGrpBot, Lx, y + GrpH, Lx + Pw, y + GrpH);
                    float fhG = FntGrp.GetHeight(g), tyG = GrpH/2f - fhG/2f;
                    g.FillRectangle(BAzul, Lx+4, y+4, 8, GrpH-8);
                    g.DrawString($"  LOCAL:  {it.Local.ToUpper()}",
                        FntGrp, BGrpFg, Lx + 16f, y + tyG);
                    var cntStr = $"{it.GrpCount} venta(s)";
                    var szCnt  = g.MeasureString(cntStr, FntGrp);
                    g.DrawString(cntStr, FntGrp, BAzulCl, Lx + Pw - szCnt.Width - 8f, y + tyG);
                    y += GrpH;
                    break;

                case HVKind.DataRow:
                    if (it.ZebraIdx % 2 == 1) g.FillRectangle(BAlt, Lx, y, Pw, FilaH);
                    float ty1 = y + tyR;
                    g.DrawString(Trunc(g, it.Vendedor, FntRow, Cw[0]-8f), FntRow,
                        System.Drawing.Brushes.Black, Cx[0]+4f, ty1);
                    g.DrawString(ShortSol(it.Solicitud), FntRow,
                        System.Drawing.Brushes.Black, Cx[1]+3f, ty1);
                    DCR(g, $"{it.Total:N0}",   FntRow, System.Drawing.Brushes.Black, 2, ty1);
                    DCR(g, $"{it.Entrega:N0}", FntRow, System.Drawing.Brushes.Black, 3, ty1);
                    DCR(g, $"{it.Debe:N0}",   FntRow, System.Drawing.Brushes.Black, 4, ty1);
                    DCR(g, $"{it.Haber:N0}",  FntRow, System.Drawing.Brushes.Black, 5, ty1);
                    DCR(g, it.Saldo == 0 ? "—" : $"{it.Saldo:N0}", FntRow,
                        it.Saldo > 0 ? (System.Drawing.Brush)BSaldoR : System.Drawing.Brushes.Black, 6, ty1);
                    y += FilaH;
                    g.DrawLine(PenSep, Lx, y, Lx+Pw, y);
                    break;

                case HVKind.Subtotal:
                    g.FillRectangle(BSbtBg, Lx, y, Pw, SubH);
                    g.DrawLine(PenAzul1, Lx, y, Lx+Pw, y);
                    float fhS = FntSbtB.GetHeight(g), tyS = SubH/2f - fhS/2f;
                    g.DrawString($"  Subtotal  {it.Local}  ({it.GrpCount} ventas)",
                        FntSbtB, BSbtFg, Lx + 6f, y + tyS);
                    DCR(g, $"Gs. {it.Total:N0}",   FntSbtB, BSbtFg, 2, y + tyS);
                    DCR(g, $"Gs. {it.Entrega:N0}", FntSbtB, BSbtFg, 3, y + tyS);
                    DCR(g, $"Gs. {it.Debe:N0}",   FntSbtB, BSbtFg, 4, y + tyS);
                    DCR(g, $"Gs. {it.Haber:N0}",  FntSbtB, BSbtFg, 5, y + tyS);
                    DCR(g, it.Saldo == 0 ? "—" : $"Gs. {it.Saldo:N0}", FntSbtB, BSaldoR, 6, y + tyS);
                    y += SubH;
                    g.DrawLine(PenGrpBot, Lx, y, Lx+Pw, y);
                    break;

                case HVKind.GenTotal:
                    int totH = SubH + 2;
                    g.FillRectangle(BTotBg, Lx, y, Pw, totH);
                    float fhT = FntSbtB.GetHeight(g), tyT = totH/2f - fhT/2f;
                    g.DrawString($"  TOTAL GENERAL  ({it.GrpCount} ventas)",
                        FntSbtB, System.Drawing.Brushes.White, Lx + 6f, y + tyT);
                    DCR(g, $"Gs. {it.Total:N0}",   FntSbtB, System.Drawing.Brushes.White, 2, y + tyT);
                    DCR(g, $"Gs. {it.Entrega:N0}", FntSbtB, System.Drawing.Brushes.White, 3, y + tyT);
                    // Debe y Haber no están en SumTotal de pagina, pero sí en los items
                    DCR(g, $"Gs. {it.Saldo:N0}",  FntSbtB, System.Drawing.Brushes.White, 6, y + tyT);
                    y += totH;
                    break;
            }
            itemOffset++;
        }

        bool esUltima = itemOffset >= items.Count;
        g.DrawLine(esUltima ? PenAzul15
            : new System.Drawing.Pen(System.Drawing.Color.FromArgb(160,160,160), 0.8f),
            Lx, PieAreaY, Lx+Pw, PieAreaY);

        var pag = $"Página {pageNum} de {totalPages}";
        var szP = g.MeasureString(pag, FntPag);
        g.DrawString(pag, FntPag, BPagNum, Lx+Pw-szP.Width, PgH-18);
        if (!esUltima)
            g.DrawString("Continúa en la siguiente página →", FntPag, BSubTxt, Lx+2f, PgH-18);
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
        => g.DrawString(t, f, b, x, y + Math.Max(0f,(h-g.MeasureString(t,f).Height)/2f));

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
