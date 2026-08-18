namespace CrediSoft.UI.Views.Ventas;

// ── Modelo de datos ─────────────────────────────────────────────────────────
public class SolicitudFichaData
{
    // Cabecera
    public string Numero    { get; init; } = "";
    public string Estado    { get; init; } = "";
    public string Fecha     { get; init; } = "";
    public string Local     { get; init; } = "";
    public string Vendedor  { get; init; } = "";
    public string Nota      { get; init; } = "";
    // Cliente
    public string CliNombre   { get; init; } = "";
    public string CliCi       { get; init; } = "";
    public string CliRuc      { get; init; } = "";
    public string CliDir      { get; init; } = "";
    public string CliCel      { get; init; } = "";
    public string CliCiudad   { get; init; } = "";
    public string CliEcv      { get; init; } = "";
    public string CliCredMax  { get; init; } = "";
    public string CliSaldo    { get; init; } = "";
    public string CliCondicion{ get; init; } = "";
    public string CliTipo     { get; init; } = "";
    public string CliVencCi   { get; init; } = "";
    public string CliConyuge  { get; init; } = "";
    // Garante
    public string GarNombre  { get; init; } = "";
    public string GarCi      { get; init; } = "";
    public string GarDir     { get; init; } = "";
    public string GarCel     { get; init; } = "";
    public string GarEmpresa { get; init; } = "";
    public string GarTelLab  { get; init; } = "";
    public string GarEcv     { get; init; } = "";
    // Referencias
    public string Ref1Nom  { get; init; } = ""; public string Ref1Tel  { get; init; } = ""; public string Ref1Trab  { get; init; } = "";
    public string Ref2Nom  { get; init; } = ""; public string Ref2Tel  { get; init; } = ""; public string Ref2Trab  { get; init; } = "";
    public string RefC1Nom { get; init; } = ""; public string RefC1Tel { get; init; } = "";
    public string RefC2Nom { get; init; } = ""; public string RefC2Tel { get; init; } = "";
    // Ingresos / Egresos
    public string ISal    { get; init; } = "0"; public string IHon  { get; init; } = "0";
    public string ICon    { get; init; } = "0"; public string IOtros{ get; init; } = "0";
    public string ITotal  { get; init; } = "0";
    public string EGas    { get; init; } = "0"; public string ECuo  { get; init; } = "0";
    public string EAlq    { get; init; } = "0"; public string EOtros{ get; init; } = "0";
    public string ETotal  { get; init; } = "0"; public string Saldo { get; init; } = "0";
    // Mercaderías
    public List<DetalleSolRow> Productos { get; init; } = new();
    public decimal TotalVenta  { get; init; }
    public decimal TotalEntrega{ get; init; }
    public int     Cuotas      { get; init; }
    public decimal CostoCuota  { get; init; }
    // Foto cedula
    public System.Drawing.Image? FotoDoc { get; init; }
    // Meta
    public string LogoPath { get; init; } = "";
    public string Usuario  { get; init; } = "";
    public string FechaImp { get; init; } = "";
}

// ── Dibujador ───────────────────────────────────────────────────────────────
public static class SolicitudFichaDibujador
{
    private const int PgW = 827;
    private const int PgH = 1169;
    private const int Lx  = 22;
    private const int Pw  = PgW - 44;   // 783
    private const int RxMax = PgW - Lx; // right edge

    // Fuentes
    private static readonly System.Drawing.Font FntTit   = new("Arial", 12f,  System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntSub   = new("Arial",  7f,  System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntSecH  = new("Arial",  9f,  System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntLbl   = new("Arial",  8f,  System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntVal   = new("Arial",  8f,  System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntValB  = new("Arial",  8f,  System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntTblH  = new("Arial",  7.5f,System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntTblR  = new("Arial",  7.5f,System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntFoot  = new("Arial",  7f,  System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntEstado= new("Arial", 10f,  System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntResum = new("Arial",  9f,  System.Drawing.FontStyle.Bold);

    // Pinceles
    private static readonly System.Drawing.SolidBrush BNaranja = new(System.Drawing.Color.FromArgb(255, 140,   0));
    private static readonly System.Drawing.SolidBrush BRojo    = new(System.Drawing.Color.FromArgb(210,   0,   0));
    private static readonly System.Drawing.SolidBrush BBlanco  = new(System.Drawing.Color.White);
    private static readonly System.Drawing.SolidBrush BGris    = new(System.Drawing.Color.FromArgb(107, 114, 128));
    private static readonly System.Drawing.SolidBrush BGrisCl  = new(System.Drawing.Color.FromArgb(200, 200, 200));
    private static readonly System.Drawing.SolidBrush BAlt     = new(System.Drawing.Color.FromArgb(249, 250, 251));
    private static readonly System.Drawing.SolidBrush BHdrTbl  = new(System.Drawing.Color.FromArgb( 13,  71, 161));
    private static readonly System.Drawing.SolidBrush BSubTxt  = new(System.Drawing.Color.FromArgb( 80,  80, 100));
    private static readonly System.Drawing.SolidBrush BSubBg   = new(System.Drawing.Color.FromArgb(245, 245, 248));
    private static readonly System.Drawing.SolidBrush BSecHdr  = new(System.Drawing.Color.FromArgb(255, 140,   0));
    private static readonly System.Drawing.SolidBrush BNaranjaOs= new(System.Drawing.Color.FromArgb(224, 112,   0));
    private static readonly System.Drawing.SolidBrush BVerde   = new(System.Drawing.Color.FromArgb( 22, 163,  74));
    private static readonly System.Drawing.SolidBrush BAzul    = new(System.Drawing.Color.FromArgb( 37,  99, 235));
    private static readonly System.Drawing.SolidBrush BPend    = new(System.Drawing.Color.FromArgb(180,  83,   9));
    private static readonly System.Drawing.SolidBrush BResumBg = new(System.Drawing.Color.FromArgb(255, 247, 237));

    // Plumas
    private static readonly System.Drawing.Pen PenRojo   = new(System.Drawing.Color.FromArgb(210, 0,  0), 3.5f);
    private static readonly System.Drawing.Pen PenRojo1  = new(System.Drawing.Color.FromArgb(210, 0,  0), 1.0f);
    private static readonly System.Drawing.Pen PenGris19 = new(System.Drawing.Color.FromArgb(190,190,190), 1.0f);
    private static readonly System.Drawing.Pen PenGris05 = new(System.Drawing.Color.FromArgb(190,190,190), 0.5f);
    private static readonly System.Drawing.Pen PenAzul   = new(System.Drawing.Color.FromArgb( 13, 71,161), 1.5f);
    private static readonly System.Drawing.Pen PenSep    = new(System.Drawing.Color.FromArgb(200,210,225), 0.5f);
    private static readonly System.Drawing.Pen PenSubBd  = new(System.Drawing.Color.FromArgb(220,220,230), 0.8f);
    private static readonly System.Drawing.Pen PenHdrTop = new(System.Drawing.Color.FromArgb(160,160,160), 0.8f);
    private static readonly System.Drawing.Pen PenBrd    = new(System.Drawing.Color.FromArgb(229,231,235), 1.0f);
    private static readonly System.Drawing.Pen PenNar15  = new(System.Drawing.Color.FromArgb(255,140,  0), 1.5f);

    private static readonly System.Globalization.CultureInfo Cult =
        System.Globalization.CultureInfo.GetCultureInfo("es-PY");

    // Los Brush/Pen/Font de arriba son estáticos compartidos entre TODAS las llamadas a
    // Dibujar() — GDI+ (System.Drawing) no es thread-safe, y cada vista previa renderiza en
    // su propio hilo de threadpool (ver SolicitudFichaImpresora.RenderAsync, Task.Run). Si el
    // usuario abre dos fichas casi al mismo tiempo (doble clic, o abre otra mientras la
    // anterior seguía renderizando), dos hilos ejecutan Dibujar() en paralelo compartiendo el
    // mismo Brush/Pen — bug real reportado: "Object is currently in use elsewhere" al llamar
    // FillRectangle. Cada Bitmap/Graphics de destino ya es propio de cada llamada (no
    // compartido), así que alcanza con serializar el uso de estos recursos estáticos.
    private static readonly object _dibujoLock = new();

    // ────────────────────────────────────────────────────────────────────────
    public static void Dibujar(System.Drawing.Graphics g, SolicitudFichaData d, System.Drawing.Image? logo)
    {
        lock (_dibujoLock) { DibujarInterno(g, d, logo); }
    }

    private static void DibujarInterno(System.Drawing.Graphics g, SolicitudFichaData d, System.Drawing.Image? logo)
    {
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.None;

        // Marca de agua
        if (logo != null)
        {
            float wmW = 460f, wmH = logo.Height * (wmW / logo.Width);
            DrawImg(g, logo, (int)(PgW/2f-wmW/2f), (int)(PgH/2f-wmH/2f+40f), (int)wmW, (int)wmH, 0.055f);
        }

        // ── Encabezado ──────────────────────────────────────────────────────
        g.FillRectangle(BBlanco, 0, 0, PgW, 88);
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

        // Banner título
        using (var path = new System.Drawing.Drawing2D.GraphicsPath())
        {
            const float r = 5f;
            path.AddArc(tx,         8,       r*2, r*2, 180, 90);
            path.AddArc(tx+tpw-r*2, 8,       r*2, r*2, 270, 90);
            path.AddArc(tx+tpw-r*2, 8+36-r*2,r*2, r*2, 0,   90);
            path.AddArc(tx,         8+36-r*2,r*2, r*2, 90,  90);
            path.CloseFigure();
            g.FillPath(BNaranja, path);
        }
        g.FillRectangle(BRojo, tx, 8, 4, 36);
        string titTxt = $"SOLICITUD N° {d.Numero}  ·  {d.CliNombre}";
        var szT = g.MeasureString(titTxt, FntTit);
        // Si es muy largo, recortar
        if (szT.Width > tpw - 100)
            titTxt = $"SOLICITUD N° {d.Numero}";
        szT = g.MeasureString(titTxt, FntTit);
        g.DrawString(titTxt, FntTit, BBlanco, tx + 8 + (tpw - 16 - szT.Width) / 2f, 8 + (36 - szT.Height) / 2f);

        g.DrawLine(PenRojo1, tx, 50, tx+tpw, 50);
        g.FillRectangle(BSubBg, tx, 53, tpw, 24f);
        g.DrawRectangle(PenSubBd, tx, 53, tpw, 24f);
        DrawC(g, $"Estado: {d.Estado}   ●   Local: {d.Local}   ●   Vendedor: {d.Vendedor}", FntSub, BSubTxt, tx+4, 53, tpw-8, 12);
        DrawC(g, $"Fecha solicitud: {d.Fecha}   ●   Impreso: {d.FechaImp}   ●   Usuario: {d.Usuario}", FntSub, BSubTxt, tx+4, 65, tpw-8, 12);

        g.DrawLine(PenRojo,   0, 80, PgW, 80);
        g.DrawLine(PenGris05, 0, 82, PgW, 82);

        int y = 90;

        // ── Chip estado ─────────────────────────────────────────────────────
        var (estBrush, estBg) = d.Estado.ToUpperInvariant() switch {
            var s when s.Contains("APROB") => (BVerde,  System.Drawing.Color.FromArgb(220,252,231)),
            var s when s.Contains("RECH")  => (BRojo,   System.Drawing.Color.FromArgb(254,226,226)),
            var s when s.Contains("VERIF") => (BAzul,   System.Drawing.Color.FromArgb(219,234,254)),
            _                              => (BPend,   System.Drawing.Color.FromArgb(254,243,199)),
        };
        var szE = g.MeasureString(d.Estado.ToUpperInvariant(), FntEstado);
        float chipW = szE.Width + 28, chipH = 26;
        float chipX = RxMax - chipW - 2;
        g.FillEllipse(new System.Drawing.SolidBrush(estBg),   chipX, y, chipW, chipH);
        g.DrawEllipse(new System.Drawing.Pen(estBrush.Color), chipX, y, chipW, chipH);
        g.DrawString(d.Estado.ToUpperInvariant(), FntEstado, estBrush, chipX + 14f, y + (chipH-szE.Height)/2f);
        y += (int)chipH + 6;

        // ── SECCIÓN CLIENTE ──────────────────────────────────────────────────
        y = SeccionHeader(g, "CLIENTE", y);
        y = FilaDoble(g, y, "Nombre:",      d.CliNombre,   "C.I.:",        d.CliCi);
        y = FilaDoble(g, y, "Dirección:",   d.CliDir,      "Ciudad:",       d.CliCiudad);
        y = FilaDoble(g, y, "Celular:",     d.CliCel,      "Estado Civil:", d.CliEcv);
        y = FilaDoble(g, y, "RUC:",         d.CliRuc,      "Cónyuge:",      d.CliConyuge);
        y = FilaDoble(g, y, "Crédito máx:", d.CliCredMax,  "Tipo:",         d.CliTipo);
        y = FilaDoble(g, y, "Condición:",   d.CliCondicion,"Venc. C.I.:",   d.CliVencCi);
        y += 4;

        // ── SECCIÓN GARANTE ──────────────────────────────────────────────────
        y = SeccionHeader(g, "GARANTE", y);
        y = FilaDoble(g, y, "Nombre:",      d.GarNombre,  "C.I.:",         d.GarCi);
        y = FilaDoble(g, y, "Dirección:",   d.GarDir,     "Teléfono:",      d.GarCel);
        y = FilaDoble(g, y, "Lugar trabajo:",d.GarEmpresa,"Tel. laboral:",   d.GarTelLab);
        y = FilaDoble(g, y, "Estado Civil:", d.GarEcv,    "",               "");
        y += 4;

        // ── SECCIÓN REFERENCIAS ──────────────────────────────────────────────
        y = SeccionHeader(g, "REFERENCIAS", y);
        y = FilaDoble(g, y, "Ref. Pers. 1:", d.Ref1Nom,  "Celular:",     d.Ref1Tel);
        y = FilaDoble(g, y, "Lugar trabajo:", d.Ref1Trab, "Ref. Pers. 2:", d.Ref2Nom);
        y = FilaDoble(g, y, "Celular:",       d.Ref2Tel,  "Lugar trabajo:", d.Ref2Trab);
        y = FilaDoble(g, y, "Ref. Com. 1:",   d.RefC1Nom, "Tel.:",         d.RefC1Tel);
        y = FilaDoble(g, y, "Ref. Com. 2:",   d.RefC2Nom, "Tel.:",         d.RefC2Tel);
        y += 4;

        // ── INGRESOS / EGRESOS + MERCADERÍAS en fila ─────────────────────────
        int ieX = Lx, ieW = 240;
        int mercX = Lx + ieW + 8, mercW = Pw - ieW - 8;
        int yIE = y;

        // IE Header
        DrawSecHdrRect(g, "INGRESOS / EGRESOS", ieX, yIE, ieW);
        yIE += 22;

        // Cabecera IE
        g.FillRectangle(BHdrTbl, ieX, yIE, ieW, 18);
        float hh = FntTblH.GetHeight(g);
        DrawS(g, "INGRESOS",       FntTblH, BBlanco, ieX+4,        yIE+(18-hh)/2f);
        DrawS(g, "EGRESOS",        FntTblH, BBlanco, ieX+ieW/2+4,  yIE+(18-hh)/2f);
        yIE += 18;

        yIE = FilaIE(g, yIE, ieX, ieW, "Salario",          d.ISal,  "Gastos familiares", d.EGas);
        yIE = FilaIE(g, yIE, ieX, ieW, "Honorario Prof.",  d.IHon,  "Cuotas",            d.ECuo);
        yIE = FilaIE(g, yIE, ieX, ieW, "Salario Cónyuge",  d.ICon,  "Alquiler",          d.EAlq);
        yIE = FilaIE(g, yIE, ieX, ieW, "Otros",            d.IOtros,"Otros",              d.EOtros);
        // totales IE
        g.DrawLine(PenAzul, ieX, yIE, ieX+ieW, yIE); yIE += 1;
        g.FillRectangle(BAlt, ieX, yIE, ieW, 18);
        DrawS(g, "TOTAL", FntValB, BGris, ieX+4, yIE+(18-FntValB.GetHeight(g))/2f);
        DrawS(g, d.ITotal, FntValB, BNaranjaOs, ieX+ieW/2-g.MeasureString(d.ITotal, FntValB).Width-4, yIE+(18-FntValB.GetHeight(g))/2f);
        DrawS(g, "TOTAL", FntValB, BGris, ieX+ieW/2+4, yIE+(18-FntValB.GetHeight(g))/2f);
        DrawS(g, d.ETotal, FntValB, BRojo, ieX+ieW-g.MeasureString(d.ETotal, FntValB).Width-4, yIE+(18-FntValB.GetHeight(g))/2f);
        yIE += 18;
        // Saldo
        g.FillRectangle(new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255,247,237)), ieX, yIE, ieW, 20);
        g.DrawRectangle(PenNar15, ieX, yIE, ieW, 20);
        DrawS(g, "SALDO:", FntValB, BGris, ieX+6, yIE+(20-FntValB.GetHeight(g))/2f);
        var saldoStr = d.Saldo;
        DrawS(g, saldoStr, FntValB, BNaranja, ieX+ieW-g.MeasureString(saldoStr, FntValB).Width-6, yIE+(20-FntValB.GetHeight(g))/2f);
        yIE += 20;

        // Nota
        if (!string.IsNullOrWhiteSpace(d.Nota))
        {
            yIE += 4;
            DrawSecHdrRect(g, "NOTA / OBSERVACIÓN", ieX, yIE, ieW);
            yIE += 22;
            g.DrawRectangle(PenBrd, ieX, yIE, ieW, 40);
            g.DrawString(d.Nota, FntVal, BGris, new System.Drawing.RectangleF(ieX+4, yIE+4, ieW-8, 32));
            yIE += 44;
        }

        // ── Mercaderías ──────────────────────────────────────────────────────
        int yMerc = y;
        DrawSecHdrRect(g, "MERCADERÍAS", mercX, yMerc, mercW);
        yMerc += 22;

        // Cabecera tabla productos
        // Código | Descripción | Precio | Entrega | Cuotas | Costo mens. | Total final | Cant. | Subtotal
        int[] tw = { 48, 0, 60, 55, 42, 68, 68, 40, 68 }; // 0 = star
        int starW = mercW - tw.Sum() + 0;
        tw[1] = starW;
        float[] tx2 = new float[tw.Length];
        tx2[0] = mercX;
        for (int i = 1; i < tw.Length; i++) tx2[i] = tx2[i-1] + tw[i-1];
        string[] th = { "Cód.", "Descripción", "Precio", "Entrega", "Ctas.", "Costo mens.", "Total final", "Cant.", "Subtotal" };

        g.FillRectangle(BHdrTbl, mercX, yMerc, mercW, 18);
        float fhTH = FntTblH.GetHeight(g);
        for (int i = 0; i < th.Length; i++)
            DrawS(g, th[i], FntTblH, BBlanco, tx2[i]+2f, yMerc+(18-fhTH)/2f);
        yMerc += 18;
        g.DrawLine(PenAzul, mercX, yMerc, mercX+mercW, yMerc);

        float fhTR = FntTblR.GetHeight(g);
        int rowi = 0;
        foreach (var prod in d.Productos)
        {
            int rh = 18;
            if (rowi % 2 == 1) g.FillRectangle(BAlt, mercX, yMerc, mercW, rh);
            float ty1 = yMerc + (rh-fhTR)/2f;
            DCT(g, prod.Codigo,                          0, tx2, tw, ty1);
            DCT(g, prod.Descripcion,                     1, tx2, tw, ty1);
            DCT(g, prod.Precio.ToString("N0",Cult),      2, tx2, tw, ty1);
            DCT(g, prod.Entrega.ToString("N0",Cult),     3, tx2, tw, ty1);
            DCT(g, prod.Cuotas.ToString(),               4, tx2, tw, ty1);
            DCT(g, prod.CostoMens.ToString("N0",Cult),   5, tx2, tw, ty1);
            DCT(g, prod.ValorFinal.ToString("N0",Cult),  6, tx2, tw, ty1);
            DCT(g, prod.Cantidad.ToString("N0",Cult),    7, tx2, tw, ty1);
            DCT(g, prod.TotalGral.ToString("N0",Cult),   8, tx2, tw, ty1);
            yMerc += rh;
            g.DrawLine(PenSep, mercX, yMerc, mercX+mercW, yMerc);
            rowi++;
        }
        g.DrawLine(PenAzul, mercX, yMerc, mercX+mercW, yMerc);
        yMerc += 2;

        // Resumen financiero
        yMerc += 4;
        DrawSecHdrRect(g, "RESUMEN FINANCIERO", mercX, yMerc, mercW);
        yMerc += 22;
        g.FillRectangle(BResumBg, mercX, yMerc, mercW, 36);
        g.DrawRectangle(PenBrd, mercX, yMerc, mercW, 36);
        float resumY = yMerc + 4;
        float resumItemW = mercW / 5f;
        string[] resumLbl = { "Total venta",  "Entrega",               "Cuotas", "Costo cuota",               "Fecha sol." };
        string[] resumVal = {
            d.TotalVenta.ToString("N0",Cult) + " Gs.",
            d.TotalEntrega.ToString("N0",Cult) + " Gs.",
            d.Cuotas.ToString(),
            d.CostoCuota.ToString("N0",Cult) + " Gs.",
            d.Fecha
        };
        float fhRL = FntFoot.GetHeight(g), fhRV = FntResum.GetHeight(g);
        for (int i = 0; i < resumLbl.Length; i++)
        {
            float rx = mercX + i * resumItemW;
            DrawS(g, resumLbl[i], FntFoot, BGris,    rx+4, resumY);
            DrawS(g, resumVal[i], FntResum, BNaranja, rx+4, resumY + fhRL + 1);
        }
        yMerc += 38;

        int yAfterSections = Math.Max(yIE, yMerc) + 6;

        // ── Foto cédula ───────────────────────────────────────────────────────
        if (d.FotoDoc != null)
        {
            int yFoto = yAfterSections;
            DrawSecHdrRect(g, "FOTO DOCUMENTO (C.I.)", Lx, yFoto, Pw);
            yFoto += 22;
            int fotoH = Math.Min(160, PgH - yFoto - 40);
            int fotoW = (int)(d.FotoDoc.Width * ((float)fotoH / d.FotoDoc.Height));
            fotoW = Math.Min(fotoW, Pw);
            DrawImg(g, d.FotoDoc, Lx, yFoto, fotoW, fotoH, 1f);
            yAfterSections = yFoto + fotoH + 8;
        }

        // ── Pie de página ─────────────────────────────────────────────────────
        g.DrawLine(PenAzul, Lx, PgH-32, Lx+Pw, PgH-32);
        var pie = $"Fecha impresión: {d.FechaImp}   ●   Usuario: {d.Usuario}";
        DrawS(g, pie, FntFoot, BGrisCl, Lx+2f, PgH-26);
    }

    // ── Helpers de dibujo ────────────────────────────────────────────────────

    private static int SeccionHeader(System.Drawing.Graphics g, string titulo, int y)
    {
        DrawSecHdrRect(g, titulo, Lx, y, Pw);
        return y + 22;
    }

    private static void DrawSecHdrRect(System.Drawing.Graphics g, string titulo, int x, int y, int w)
    {
        g.FillRectangle(BSecHdr, x, y, w, 20);
        float fh = FntSecH.GetHeight(g);
        g.DrawString(titulo, FntSecH, BBlanco, x + 8f, y + (20 - fh) / 2f);
    }

    private static int FilaDoble(System.Drawing.Graphics g, int y,
        string lbl1, string val1, string lbl2, string val2)
    {
        const int Rh = 18;
        int half = Pw / 2;
        float fhR = FntVal.GetHeight(g), ty = y + (Rh - fhR) / 2f;

        // Fondo alterno
        g.DrawLine(PenSep, Lx, y, Lx+Pw, y);

        if (!string.IsNullOrEmpty(lbl1))
        {
            var szL = g.MeasureString(lbl1, FntLbl);
            g.DrawString(lbl1, FntLbl, BGris, Lx+4f, ty);
            float vx = Lx + 4 + szL.Width + 4;
            var v1Trunc = Trunc(g, val1 ?? "—", FntVal, half - (vx - Lx) - 6);
            g.DrawString(v1Trunc, FntVal, new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(17,24,39)), vx, ty);
        }
        if (!string.IsNullOrEmpty(lbl2))
        {
            float x2 = Lx + half;
            var szL2 = g.MeasureString(lbl2, FntLbl);
            g.DrawString(lbl2, FntLbl, BGris, x2+4f, ty);
            float vx2 = x2 + 4 + szL2.Width + 4;
            var v2Trunc = Trunc(g, val2 ?? "—", FntVal, (Lx+Pw) - vx2 - 4);
            g.DrawString(v2Trunc, FntVal, new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(17,24,39)), vx2, ty);
        }
        return y + Rh;
    }

    private static int FilaIE(System.Drawing.Graphics g, int y, int x, int w,
        string lbl1, string val1, string lbl2, string val2)
    {
        const int Rh = 16;
        int half = w / 2;
        float fhR = FntVal.GetHeight(g), ty = y + (Rh - fhR) / 2f;
        float fhRB= FntValB.GetHeight(g), tyB = y + (Rh - fhRB) / 2f;

        g.DrawLine(PenSep, x, y, x+w, y);
        DrawS(g, lbl1, FntVal, BGris, x+4f, ty);
        var v1 = val1 ?? "0";
        DrawS(g, v1, FntValB, BNaranjaOs, x+half-g.MeasureString(v1, FntValB).Width-4, tyB);
        DrawS(g, lbl2, FntVal, BGris, x+half+4f, ty);
        var v2 = val2 ?? "0";
        DrawS(g, v2, FntValB, BRojo, x+w-g.MeasureString(v2, FntValB).Width-4, tyB);
        return y + Rh;
    }

    private static void DCT(System.Drawing.Graphics g, string txt, int col, float[] cx, int[] cw, float ty)
    {
        var t = Trunc(g, txt, FntTblR, cw[col] - 4f);
        g.DrawString(t, FntTblR, new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(17,24,39)), cx[col]+2f, ty);
    }

    private static string Trunc(System.Drawing.Graphics g, string t, System.Drawing.Font f, float mw)
    {
        if (string.IsNullOrEmpty(t) || g.MeasureString(t, f).Width <= mw) return t;
        float ew = g.MeasureString("…", f).Width;
        while (t.Length > 0 && g.MeasureString(t, f).Width + ew > mw) t = t[..^1];
        return t + "…";
    }

    private static void DrawS(System.Drawing.Graphics g, string t, System.Drawing.Font f,
        System.Drawing.Brush b, float x, float y)
        => g.DrawString(t, f, b, x, y);

    private static void DrawC(System.Drawing.Graphics g, string t, System.Drawing.Font f,
        System.Drawing.Brush b, float x, float y, float w, float h)
    {
        var s = g.MeasureString(t, f);
        g.DrawString(t, f, b, x + Math.Max(0f,(w-s.Width)/2f), y + Math.Max(0f,(h-s.Height)/2f));
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
