using CrediSoft.Core.Models;

namespace CrediSoft.UI.Views.Maestros;

public class ProveedorFichaData
{
    public string Codigo      { get; init; } = "";
    public string Nombre      { get; init; } = "";
    public string Ruc         { get; init; } = "";
    public string Direccion   { get; init; } = "";
    public string Telefono    { get; init; } = "";
    public string Ciudad      { get; init; } = "";
    public string Email       { get; init; } = "";
    public string Web         { get; init; } = "";
    public string ConNombre   { get; init; } = "";
    public string ConCargo    { get; init; } = "";
    public string ConCelular  { get; init; } = "";
    public string ConCorreo   { get; init; } = "";
    public string LogoPath    { get; init; } = "";
    public string Usuario     { get; init; } = "";
    public string FechaImp    { get; init; } = "";
}

public static class ProveedorFichaDibujador
{
    private const int PgW  = 827;
    private const int PgH  = 1169;
    private const int Lx   = 30;
    private const int Rx   = PgW - 30;
    private const int Pw   = Rx - Lx;   // 767

    private static readonly System.Drawing.Font FntTit    = new("Arial", 13f, System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntSub    = new("Arial",  7f, System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntSecHdr = new("Arial",  9f, System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntLbl    = new("Arial",  8f, System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntVal    = new("Arial",  9f, System.Drawing.FontStyle.Bold);
    private static readonly System.Drawing.Font FntFoot   = new("Arial",  7f, System.Drawing.FontStyle.Regular);
    private static readonly System.Drawing.Font FntCodigo = new("Arial", 22f, System.Drawing.FontStyle.Bold);

    private static readonly System.Drawing.SolidBrush BNaranja  = new(System.Drawing.Color.FromArgb(230, 81,   0));
    private static readonly System.Drawing.SolidBrush BRojo     = new(System.Drawing.Color.FromArgb(210,  0,   0));
    private static readonly System.Drawing.SolidBrush BAzul     = new(System.Drawing.Color.FromArgb( 13, 71, 161));
    private static readonly System.Drawing.SolidBrush BVerde    = new(System.Drawing.Color.FromArgb( 27,134,  68));
    private static readonly System.Drawing.SolidBrush BSubTxt   = new(System.Drawing.Color.FromArgb( 80, 80, 100));
    private static readonly System.Drawing.SolidBrush BSubBg    = new(System.Drawing.Color.FromArgb(245,245,248));
    private static readonly System.Drawing.SolidBrush BSecHdrBg = new(System.Drawing.Color.FromArgb(230, 81,   0));
    private static readonly System.Drawing.SolidBrush BCardBg   = new(System.Drawing.Color.FromArgb(250,250,252));
    private static readonly System.Drawing.SolidBrush BLbl      = new(System.Drawing.Color.FromArgb(100,100,120));
    private static readonly System.Drawing.SolidBrush BVal      = new(System.Drawing.Color.FromArgb( 20, 20, 40));
    private static readonly System.Drawing.SolidBrush BPagNum   = new(System.Drawing.Color.FromArgb(140,140,160));
    private static readonly System.Drawing.SolidBrush BCodigoFg = new(System.Drawing.Color.FromArgb(230, 81,   0));

    private static readonly System.Drawing.Pen PenRojo   = new(System.Drawing.Color.FromArgb(210,  0,  0), 3.5f);
    private static readonly System.Drawing.Pen PenRojo1  = new(System.Drawing.Color.FromArgb(210,  0,  0), 1.0f);
    private static readonly System.Drawing.Pen PenGris19 = new(System.Drawing.Color.FromArgb(190,190,190), 1.0f);
    private static readonly System.Drawing.Pen PenGris05 = new(System.Drawing.Color.FromArgb(190,190,190), 0.5f);
    private static readonly System.Drawing.Pen PenAzul   = new(System.Drawing.Color.FromArgb( 13, 71,161), 1.5f);
    private static readonly System.Drawing.Pen PenSubBd  = new(System.Drawing.Color.FromArgb(220,220,230), 0.8f);
    private static readonly System.Drawing.Pen PenCard   = new(System.Drawing.Color.FromArgb(220,220,230), 0.8f);
    private static readonly System.Drawing.Pen PenSep    = new(System.Drawing.Color.FromArgb(210,210,220), 0.7f);

    public static void Dibujar(System.Drawing.Graphics g, ProveedorFichaData d, System.Drawing.Image? logo)
    {
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.None;

        // Marca de agua
        if (logo != null)
        {
            float wmW = 480f, wmH = logo.Height * (wmW / logo.Width);
            DrawImg(g, logo, (int)(PgW/2f-wmW/2f), (int)(PgH/2f-wmH/2f+40f), (int)wmW, (int)wmH, 0.05f);
        }

        // ── Encabezado ──────────────────────────────────────────────────────
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
            path.AddArc(tx+tpw-r*2,  8+36-r*2, r*2, r*2,   0, 90);
            path.AddArc(tx,          8+36-r*2, r*2, r*2,  90, 90);
            path.CloseFigure();
            g.FillPath(BNaranja, path);
        }
        g.FillRectangle(BRojo, tx, 8, 4, 36);
        const string titulo = "FICHA DE PROVEEDOR";
        var szT = g.MeasureString(titulo, FntTit);
        g.DrawString(titulo, FntTit, System.Drawing.Brushes.White,
            tx + 8 + (tpw - 12 - szT.Width) / 2f, 8 + (36 - szT.Height) / 2f);

        g.DrawLine(PenRojo1, tx, 50, tx+tpw, 50);
        g.FillRectangle(BSubBg, tx, 53, tpw, 24f);
        g.DrawRectangle(PenSubBd, tx, 53, tpw, 24f);
        DrawC(g, $"Fecha: {d.FechaImp}   ●   Usuario: {d.Usuario}", FntSub, BSubTxt, tx+4, 53, tpw-8, 12);

        g.DrawLine(PenRojo,   0, 80, PgW, 80);
        g.DrawLine(PenGris05, 0, 82, PgW, 82);

        int y = 95;

        // ── Chip de código ──────────────────────────────────────────────────
        {
            var cod = string.IsNullOrEmpty(d.Codigo) ? "—" : d.Codigo;
            var szC = g.MeasureString(cod, FntCodigo);
            float chipW = szC.Width + 28f, chipH = szC.Height + 10f;
            float chipX = Rx - chipW;
            DrawRoundRect(g, BCardBg, PenCard, chipX, y, chipW, chipH, 6f);
            g.DrawString(cod, FntCodigo, BCodigoFg, chipX + 14f, y + 5f);

            // Nombre grande a la izquierda del chip
            var szN = g.MeasureString(d.Nombre, FntTit);
            float nameY = y + (chipH - szN.Height) / 2f;
            g.DrawString(d.Nombre, FntTit, BVal, Lx, nameY);

            y += (int)chipH + 14;
        }

        g.DrawLine(PenAzul, Lx, y, Rx, y); y += 8;

        // ── DATOS DE LA EMPRESA ─────────────────────────────────────────────
        y = SecHdr(g, "DATOS DE LA EMPRESA", y);

        // Card empresa
        int cardY = y;
        DrawRoundRect(g, BCardBg, PenCard, Lx, cardY, Pw, 148, 6f);
        y = cardY + 10;

        // Fila: RUC | Ciudad
        FilaDoble(g, "RUC", d.Ruc, "Ciudad", d.Ciudad, Lx+10, Rx-10, ref y);
        // Fila: Dirección (ancho completo)
        FilaSola(g, "Dirección", d.Direccion, Lx+10, Rx-10, ref y);
        // Fila: Teléfono | E-mail
        FilaDoble(g, "Teléfono", d.Telefono, "E-mail", d.Email, Lx+10, Rx-10, ref y);
        // Fila: Web (ancho completo)
        FilaSola(g, "Web", d.Web, Lx+10, Rx-10, ref y);

        y = cardY + 148 + 12;

        // ── DATOS DEL CONTACTO ──────────────────────────────────────────────
        y = SecHdr(g, "DATOS DEL CONTACTO", y);

        bool hayContacto = !string.IsNullOrEmpty(d.ConNombre) || !string.IsNullOrEmpty(d.ConCargo)
                        || !string.IsNullOrEmpty(d.ConCelular) || !string.IsNullOrEmpty(d.ConCorreo);

        int cardCY = y;
        DrawRoundRect(g, BCardBg, PenCard, Lx, cardCY, Pw, hayContacto ? 100 : 42, 6f);
        y = cardCY + 10;

        if (hayContacto)
        {
            FilaDoble(g, "Nombre",  d.ConNombre,  "Cargo",  d.ConCargo,  Lx+10, Rx-10, ref y);
            FilaDoble(g, "Celular", d.ConCelular, "Correo", d.ConCorreo, Lx+10, Rx-10, ref y);
        }
        else
        {
            DrawC(g, "Sin datos de contacto registrados", FntSub, BLbl, Lx+10, y, Pw-20, 20);
            y += 22;
        }

        y = cardCY + (hayContacto ? 100 : 42) + 16;

        // ── Pie ─────────────────────────────────────────────────────────────
        int pieY = PgH - 42;
        g.DrawLine(PenAzul, Lx, pieY, Rx, pieY);
        DrawC(g, "Documento generado por ElectroMar", FntFoot, BPagNum, Lx, pieY+6, Pw/2f, 16);
        var pag = $"Página 1 de 1";
        var szP = g.MeasureString(pag, FntFoot);
        g.DrawString(pag, FntFoot, BPagNum, Rx - szP.Width, pieY + 6);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static int SecHdr(System.Drawing.Graphics g, string txt, int y)
    {
        DrawRoundRect(g, BSecHdrBg, null, Lx, y, Pw, 24, 4f);
        g.FillRectangle(BRojo, Lx, y, 4, 24);
        float fh = FntSecHdr.GetHeight(g);
        g.DrawString(txt, FntSecHdr, System.Drawing.Brushes.White, Lx + 10f, y + (24 - fh) / 2f);
        return y + 24 + 6;
    }

    private static void FilaDoble(System.Drawing.Graphics g,
        string l1, string v1, string l2, string v2,
        float xL, float xR, ref int y)
    {
        float midX = xL + (xR - xL) / 2f - 4f;
        float cellW = midX - xL;
        float fhL = FntLbl.GetHeight(g), fhV = FntVal.GetHeight(g);
        g.DrawString(l1, FntLbl, BLbl, xL, y);
        g.DrawString(l2, FntLbl, BLbl, midX + 8f, y);
        y += (int)fhL + 2;
        var t1 = TruncStr(g, v1, FntVal, cellW); g.DrawString(t1, FntVal, BVal, xL, y);
        var t2 = TruncStr(g, v2, FntVal, cellW); g.DrawString(t2, FntVal, BVal, midX + 8f, y);
        y += (int)fhV + 10;
        g.DrawLine(PenSep, xL, y - 4, xR, y - 4);
    }

    private static void FilaSola(System.Drawing.Graphics g,
        string lbl, string val, float xL, float xR, ref int y)
    {
        float fhL = FntLbl.GetHeight(g), fhV = FntVal.GetHeight(g);
        g.DrawString(lbl, FntLbl, BLbl, xL, y);
        y += (int)fhL + 2;
        var t = TruncStr(g, val, FntVal, xR - xL); g.DrawString(t, FntVal, BVal, xL, y);
        y += (int)fhV + 10;
        g.DrawLine(PenSep, xL, y - 4, xR, y - 4);
    }

    private static void DrawRoundRect(System.Drawing.Graphics g,
        System.Drawing.Brush? fill, System.Drawing.Pen? border,
        float x, float y, float w, float h, float r)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(x,       y,       r*2, r*2, 180,  90);
        path.AddArc(x+w-r*2, y,       r*2, r*2, 270,  90);
        path.AddArc(x+w-r*2, y+h-r*2, r*2, r*2,   0,  90);
        path.AddArc(x,       y+h-r*2, r*2, r*2,  90,  90);
        path.CloseFigure();
        if (fill   != null) g.FillPath(fill,   path);
        if (border != null) g.DrawPath(border, path);
        path.Dispose();
    }

    private static void DrawC(System.Drawing.Graphics g, string t, System.Drawing.Font f,
        System.Drawing.Brush b, float x, float y, float w, float h)
    {
        var s = g.MeasureString(t, f);
        g.DrawString(t, f, b, x + Math.Max(0f, (w - s.Width) / 2f), y + Math.Max(0f, (h - s.Height) / 2f));
    }

    private static string TruncStr(System.Drawing.Graphics g, string t, System.Drawing.Font f, float mw)
    {
        if (string.IsNullOrEmpty(t)) return "—";
        if (g.MeasureString(t, f).Width <= mw) return t;
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
