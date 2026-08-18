namespace CrediSoft.UI.Views.Informes;

// ── Dibujador reporte morosos formato TARJETA POR CLIENTE (portrait 827×1169) ──
// Réplica del "REPORTE HISTÓRICO DE CLIENTES MOROSOS" del sistema viejo: una
// tarjeta por cuota en mora, con banda roja de nombre, datos de la venta,
// contacto/garante, y la cuota vencida con su total resaltado en rojo.
public static class AtrasosTarjetasDibujador
{
    private const int PgW      = 827;
    private const int PgH      = 1169;
    private const int Lx       = 20;
    private const int Pw       = PgW - 40;   // 787
    private const int PieAreaY = PgH - 30;

    // Alto de cada tarjeta (banda + tabla venta + contacto + tabla cuota + total + margen)
    private const int TarjetaH = 205;
    private const int BodyY    = 95;

    private static readonly System.Drawing.Font FntTit    = new("Arial", 12f,  System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntSub    = new("Arial",  7f,  System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntNombre = new("Arial",  9.5f, System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntFecha  = new("Arial",  7.5f, System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntHdr    = new("Arial",  7f,  System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntRow    = new("Arial",  7.5f, System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntLbl    = new("Arial",  6.5f, System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntTotal  = new("Arial",  9f,  System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntPag    = new("Arial",  6.5f, System.Drawing.FontStyle.Regular);

    private static readonly System.Drawing.SolidBrush BRojo     = new(System.Drawing.Color.FromArgb(210,  0,  0));
    private static readonly System.Drawing.SolidBrush BRojoTxt  = new(System.Drawing.Color.FromArgb(200, 20, 20));
    private static readonly System.Drawing.SolidBrush BSubTxt   = new(System.Drawing.Color.FromArgb( 80, 80,100));
    private static readonly System.Drawing.SolidBrush BSubBg    = new(System.Drawing.Color.FromArgb(245,245,248));
    private static readonly System.Drawing.SolidBrush BPagNum   = new(System.Drawing.Color.FromArgb(120,120,140));
    private static readonly System.Drawing.SolidBrush BHdrBg    = new(System.Drawing.Color.FromArgb(224,224,224));
    private static readonly System.Drawing.SolidBrush BLblBg    = new(System.Drawing.Color.FromArgb(238,238,238));

    private static readonly System.Drawing.Pen PenRojo   = new(System.Drawing.Color.FromArgb(210,  0,  0), 3f);
    private static readonly System.Drawing.Pen PenRojo1  = new(System.Drawing.Color.FromArgb(210,  0,  0), 0.8f);
    private static readonly System.Drawing.Pen PenGris19 = new(System.Drawing.Color.FromArgb(190,190,190), 1.0f);
    private static readonly System.Drawing.Pen PenGris05 = new(System.Drawing.Color.FromArgb(190,190,190), 0.5f);
    private static readonly System.Drawing.Pen PenGrisTb = new(System.Drawing.Color.FromArgb(160,160,160), 0.8f);
    private static readonly System.Drawing.Pen PenSubBd  = new(System.Drawing.Color.FromArgb(220,220,230), 0.8f);

    public static int ContarPaginas(AtrasosPagina p)
    {
        if (p.Morosos.Count == 0) return 1;
        int area = PieAreaY - BodyY;
        int porPag = Math.Max(1, area / TarjetaH);
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

        if (logo != null)
        {
            float wmW = 500f, wmH = logo.Height * (wmW / logo.Width);
            DrawImg(g, logo, (int)(PgW / 2f - wmW / 2f), (int)(PgH / 2f - wmH / 2f + 40f), (int)wmW, (int)wmH, 0.05f);
        }

        // ── Encabezado ────────────────────────────────────────────────────────
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
            g.FillPath(BRojo, path);
        }
        const string titulo = "REPORTE HISTÓRICO DE CLIENTES MOROSOS";
        var szT = g.MeasureString(titulo, FntTit);
        g.DrawString(titulo, FntTit, System.Drawing.Brushes.White,
            tx + 8f + (tpw - 12f - szT.Width) / 2f, 8f + (36f - szT.Height) / 2f);

        g.DrawLine(PenRojo1, tx, 50, tx+tpw, 50);
        g.FillRectangle(BSubBg, tx, 53, tpw, 24f);
        g.DrawRectangle(PenSubBd, tx, 53, tpw, 24f);
        var sub = string.IsNullOrEmpty(p.Filtro) ? "Todos los atrasos" : p.Filtro;
        DrawC(g, sub, FntSub, BSubTxt, tx+4f, 53f, tpw-8f, 12f);
        DrawC(g, $"Fecha: {p.FechaImp}   ●   Usuario: {p.Usuario}", FntSub, BSubTxt, tx+4f, 65f, tpw-8f, 12f);

        g.DrawLine(PenRojo,   0, 80, PgW, 80);
        g.DrawLine(PenGris05, 0, 82, PgW, 82);

        // ── Tarjetas ──────────────────────────────────────────────────────────
        int y = BodyY;
        int area = PieAreaY - BodyY;
        int porPag = Math.Max(1, area / TarjetaH);
        int start = pageOffset, end = Math.Min(start + porPag, p.Morosos.Count);

        for (int i = start; i < end; i++)
        {
            var f = p.Morosos[i];
            DibujarTarjeta(g, f, Lx, y, Pw);
            y += TarjetaH;
        }

        // ── Pie ───────────────────────────────────────────────────────────────
        var pag = $"Página {pageNum} de {totalPages}";
        var szP = g.MeasureString(pag, FntPag);
        g.DrawString(pag, FntPag, BPagNum, Lx+Pw-szP.Width, PgH-16);
    }

    private static void DibujarTarjeta(System.Drawing.Graphics g, AtrasosFilaMoroso f, float x, float y, float w)
    {
        // Banda roja con nombre + fecha/hora de impresión a la derecha
        const float bandaH = 20f;
        g.FillRectangle(BRojo, x, y, w, bandaH);
        var szNom = g.MeasureString(f.Cliente.ToUpperInvariant(), FntNombre);
        g.DrawString(f.Cliente.ToUpperInvariant(), FntNombre, System.Drawing.Brushes.White,
            x + 6f, y + Math.Max(0f, (bandaH - szNom.Height) / 2f));

        var fechaTxt = DateTime.Now.ToString("d/M/yyyy H:mm:ss");
        var szFec = g.MeasureString(fechaTxt, FntFecha);
        g.DrawString(fechaTxt, FntFecha, System.Drawing.Brushes.Black, x + w - szFec.Width - 2f, y + bandaH + 2f);

        float yy = y + bandaH + 18f;

        // Tabla de la venta: Solicitud | Descripción | Precio | Vendedor
        int[]   cw = { 130, 340, 130, w > 787 ? 187 : (int)(w - 130 - 340 - 130) };
        float[] cx = { x, x+cw[0], x+cw[0]+cw[1], x+cw[0]+cw[1]+cw[2] };
        string[] hdr = { "SOLICITUD", "DESCRIPCION", "PRECIO", "VENDEDOR" };
        DibujarFilaTabla(g, hdr, cw, cx, yy, esHeader: true);
        yy += 16f;
        string[] vals = { ShortSol(f.Solicitud), f.ArticuloDesc, $"{f.ArticuloPrecio:N0}", f.Vendedor };
        DibujarFilaTabla(g, vals, cw, cx, yy, esHeader: false);
        yy += 20f;

        // Tel. Cliente | Garante | Tel. — tres columnas de ancho fijo con separación entre
        // sí, sumando exactamente el ancho disponible (antes había un array con índices sin
        // usar que dejaba la columna "Tel." del garante casi sin espacio real).
        const float gap = 6f;
        float wTelCliente = w * 0.30f;
        float wTelGarante = w * 0.20f;
        float wGarante    = w - wTelCliente - wTelGarante - gap * 2f;
        float xTelCliente = x;
        float xGarante    = x + wTelCliente + gap;
        float xTelGarante = xGarante + wGarante + gap;

        DibujarEtiquetaValor(g, "Tel. Cliente", f.Telefono,       xTelCliente, yy, wTelCliente);
        DibujarEtiquetaValor(g, "Garante",      f.GaranteNombre,  xGarante,    yy, wGarante);
        DibujarEtiquetaValor(g, "Tel.",         f.GaranteTel,     xTelGarante, yy, wTelGarante);
        yy += 22f;

        // C.I Cliente
        DibujarEtiquetaValor(g, "C.I Cliente", f.ClienteCi, x, yy, 200);
        yy += 22f;

        // Tabla de la cuota: N° cuota | Monto | Vencimiento | Mora
        int[]   cw3 = { 90, 130, 130, w > 787 ? 437 : (int)(w-90-130-130) };
        float[] cx3 = { x, x+cw3[0], x+cw3[0]+cw3[1], x+cw3[0]+cw3[1]+cw3[2] };
        string[] hdr3 = { "N° CUOTA", "MONTO", "VENCIMIENTO", "MORA" };
        DibujarFilaTabla(g, hdr3, cw3, cx3, yy, esHeader: true);
        yy += 16f;
        // Se muestra el número de mora tal cual (incluye negativos, dentro del período de
        // gracia) — un guion ahí podía malinterpretarse como "sin mora" cuando en realidad
        // hay un valor real (ej. -4, vencida hace 1 día pero aún en gracia).
        string[] vals3 = { f.NCuota.ToString(), $"{f.Monto:N0}", f.Vto, f.Mora.ToString() };
        DibujarFilaTabla(g, vals3, cw3, cx3, yy, esHeader: false);
        yy += 22f;

        // Total resaltado en rojo, centrado
        var totalTxt = $"{f.Monto:N0}";
        var szTot = g.MeasureString(totalTxt, FntTotal);
        g.DrawString(totalTxt, FntTotal, BRojoTxt, x + (w - szTot.Width) / 2f, yy);

        // Separador entre tarjetas
        g.DrawLine(PenGrisTb, x, y + TarjetaH - 6f, x + w, y + TarjetaH - 6f);
    }

    private static void DibujarFilaTabla(
        System.Drawing.Graphics g, string[] vals, int[] cw, float[] cx, float y, bool esHeader)
    {
        var font  = esHeader ? FntHdr : FntRow;
        var brush = System.Drawing.Brushes.Black;
        float h = esHeader ? 16f : 18f;

        for (int i = 0; i < vals.Length; i++)
        {
            if (esHeader)
                g.FillRectangle(BHdrBg, cx[i], y, cw[i], h);
            g.DrawRectangle(PenGrisTb, cx[i], y, cw[i], h);
            var t = Trunc(g, vals[i], font, cw[i] - 6f);
            var szV = g.MeasureString(t, font);
            float ty = y + Math.Max(0f, (h - szV.Height) / 2f);
            g.DrawString(t, font, brush, cx[i] + 4f, ty);
        }
    }

    private static void DibujarEtiquetaValor(System.Drawing.Graphics g, string etiqueta, string valor, float x, float y, float w)
    {
        const float h = 18f;
        var szE = g.MeasureString(etiqueta, FntLbl);
        float etW = Math.Min(w * 0.5f, szE.Width + 10f);
        g.FillRectangle(BLblBg, x, y, etW, h);
        g.DrawRectangle(PenGrisTb, x, y, etW, h);
        g.DrawString(etiqueta, FntLbl, System.Drawing.Brushes.Black, x + 4f, y + Math.Max(0f, (h-szE.Height)/2f));

        var valW = Math.Max(0f, w - etW);
        g.DrawRectangle(PenGrisTb, x + etW, y, valW, h);
        var t = Trunc(g, valor, FntRow, valW - 6f);
        var szV = g.MeasureString(t, FntRow);
        g.DrawString(t, FntRow, System.Drawing.Brushes.Black, x + etW + 4f, y + Math.Max(0f, (h-szV.Height)/2f));
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
