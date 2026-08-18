namespace CrediSoft.UI.Views.Informes;

// ── RESUMEN — agrupado por Local, con el total del tab activo (Entrada/Salida/Neto) ──

public record FilaArqueoResumen(string Local, decimal Total, int Operaciones, decimal Ingresos = 0, decimal Egresos = 0);

public class CajaArqueoResumenPagina
{
    public List<FilaArqueoResumen> Filas   { get; init; } = new();
    public string TituloTipo { get; init; } = "ENTRADA"; // ENTRADA / SALIDA / ENTRADA - SALIDA
    public string Desde      { get; init; } = "";
    public string Hasta      { get; init; } = "";
    public string FechaImp   { get; init; } = "";
    public string Usuario    { get; init; } = "";
    public string LogoPath   { get; init; } = "";

    public static string ResolverLogoPath() => CrediSoft.UI.Views.Maestros.ArticulosPagina.ResolverLogoPath();
}

public static class CajaArqueoResumenDibujador
{
    // Portrait A4: 827 × 1169 — igual al reporte viejo (bandas verticales por local)
    private const int PgW = 827;
    private const int PgH = 1169;
    private const int Lx  = 30;
    private const int Pw  = PgW - 60;

    private const int BandaH  = 26;
    private const int ValorH  = 32;
    // Con Ingresos y Egresos desglosados (reporte "ENTRADA - SALIDA") el bloque necesita 2
    // filas de valor + el total, en vez de 1 sola fila — ValorHDoble cubre ese caso.
    private const int ValorHDoble = ValorH * 2 + 8;
    private const int GapH    = 12;
    private const int TotalH  = 52; // banda final de total general (solo en la última página)

    private static int AltoValor(CajaArqueoResumenPagina p) =>
        p.TituloTipo == "ENTRADA - SALIDA" ? ValorHDoble : ValorH;

    public static int ContarPaginas(CajaArqueoResumenPagina p)
    {
        int y = 118; // altura del header con logo real en la primera página
        int paginas = 1;
        int valorH = AltoValor(p);
        for (int i = 0; i < p.Filas.Count; i++)
        {
            int bloqueH = BandaH + valorH + GapH;
            bool esUltimaFila = i == p.Filas.Count - 1;
            int margenNecesario = esUltimaFila ? 60 + TotalH : 60;
            if (y + bloqueH > PgH - margenNecesario) { paginas++; y = 40; } // páginas siguientes: header simplificado
            y += bloqueH;
        }
        return paginas;
    }

    public static bool DibujarPagina(
        System.Drawing.Graphics g,
        CajaArqueoResumenPagina p,
        System.Drawing.Image? logo,
        ref int filaOffset,
        int pageNum,
        int totalPages)
    {
        var azul    = System.Drawing.Color.FromArgb(21, 79, 110);

        using var fntHdr   = new System.Drawing.Font("Arial", 8f,   System.Drawing.FontStyle.Regular);
        using var fntTit   = new System.Drawing.Font("Arial", 12f,  System.Drawing.FontStyle.Bold);
        using var fntSub   = new System.Drawing.Font("Arial", 7.5f, System.Drawing.FontStyle.Regular);
        using var fntLocal = new System.Drawing.Font("Arial", 10f,  System.Drawing.FontStyle.Bold);
        using var fntEtiq  = new System.Drawing.Font("Arial", 9f,   System.Drawing.FontStyle.Bold);
        using var fntMonto = new System.Drawing.Font("Arial", 11f,  System.Drawing.FontStyle.Bold);
        using var fntPag   = new System.Drawing.Font("Arial", 6.5f, System.Drawing.FontStyle.Regular);

        if (logo != null)
        {
            float wmW = 380f, wmH = logo.Height * (wmW / logo.Width);
            DrawConOpacidad(g, logo, (int)(PgW / 2f - wmW / 2f), (int)(PgH / 2f - wmH / 2f), (int)wmW, (int)wmH, 0.05f);
        }

        int y;
        if (pageNum == 1)
        {
            // Encabezado con logo real (no solo marca de agua) + banda de título con
            // color sólido — mismo lenguaje visual que Egresos/Gastos de Caja, en vez
            // de texto plano centrado.
            y = 12;
            if (logo != null)
            {
                float logoH = 44f, logoW = logo.Width * (logoH / logo.Height);
                DrawConOpacidad(g, logo, Lx, y, (int)logoW, (int)logoH, 1f);

                float sepX = Lx + logoW + 10f;
                using var penGrisLogo = new System.Drawing.Pen(System.Drawing.Color.FromArgb(190, 190, 190), 1f);
                g.DrawLine(penGrisLogo, sepX, y, sepX, y + 44);

                float tx = sepX + 10f, tpw = Lx + Pw - tx;
                using var fntEmp2 = new System.Drawing.Font("Arial", 11f, System.Drawing.FontStyle.Bold);
                g.DrawString("CREDIMAR S.A.", fntEmp2, System.Drawing.Brushes.Black, tx, y + 2f);
                using var fntSub2 = new System.Drawing.Font("Arial", 7.5f, System.Drawing.FontStyle.Regular);
                g.DrawString("Electrodomésticos", fntSub2, System.Drawing.Brushes.Gray, tx, y + 20f);
            }
            else
            {
                using var fntEmp = new System.Drawing.Font("Arial", 13f, System.Drawing.FontStyle.Bold);
                DrawCentrado(g, "C R E D I M A R   S . A .", fntEmp, System.Drawing.Brushes.Black, Lx, y, Pw, 22);
            }
            y += 52;

            // Banda de título — contorno azul, fondo blanco, texto negro
            using var penTituloBanda = new System.Drawing.Pen(azul, 1.5f);
            g.DrawRectangle(penTituloBanda, Lx, y, Pw, 28);
            using var fntTitBanda = new System.Drawing.Font("Arial", 10.5f, System.Drawing.FontStyle.Bold);
            DrawCentrado(g, $"ARQUEO DE CAJA DE TIPO {p.TituloTipo}", fntTitBanda, System.Drawing.Brushes.Black, Lx, y, Pw, 28);
            y += 28 + 6;

            using var fntSub3 = new System.Drawing.Font("Arial", 9.5f, System.Drawing.FontStyle.Bold);
            DrawCentrado(g, $"Período: {p.Desde} al {p.Hasta}", fntSub3, System.Drawing.Brushes.Black, Lx, y, Pw, 16);
            y += 18;

            using var penSep = new System.Drawing.Pen(System.Drawing.Color.FromArgb(190, 190, 190), 0.8f);
            g.DrawLine(penSep, Lx, y, Lx + Pw, y);
            y += 14;
        }
        else
        {
            y = 40;
        }

        var brVerde = System.Drawing.Color.FromArgb(46, 125, 50);
        var brRojo  = System.Drawing.Color.FromArgb(198, 40, 40);
        int valorH = AltoValor(p);

        // Dibuja una fila etiqueta+monto dentro de una caja gris — reutilizado tanto para el
        // caso de una sola fila (ENTRADA o SALIDA sola) como para las 2+1 filas del caso
        // "ENTRADA - SALIDA" (Ingresos, Egresos, Total).
        void FilaValor(string etiqueta, decimal monto, float fy, float altoFila, System.Drawing.Brush brMonto)
        {
            string montoTxt = monto.ToString("N0");
            var szEtiq  = g.MeasureString(etiqueta, fntEtiq);
            var szMonto = g.MeasureString(montoTxt, fntMonto);
            float yEtiq  = fy + (altoFila - szEtiq.Height) / 2f;
            float yMonto = fy + (altoFila - szMonto.Height) / 2f;
            g.DrawString(etiqueta, fntEtiq, System.Drawing.Brushes.Black, Lx + 10f, yEtiq);
            g.DrawString(montoTxt, fntMonto, brMonto, Lx + Pw / 2f - szMonto.Width / 2f, yMonto);
        }

        bool hayMas = false;
        while (filaOffset < p.Filas.Count)
        {
            int bloqueH = BandaH + valorH + GapH;
            bool esUltimaFila = filaOffset == p.Filas.Count - 1;
            int margenNecesario = esUltimaFila ? 60 + TotalH : 60;
            if (y + bloqueH > PgH - margenNecesario) { hayMas = true; break; }

            var f = p.Filas[filaOffset++];

            // Banda del local — contorno azul, fondo blanco, texto negro
            using var penBandaLocal = new System.Drawing.Pen(azul, 1.2f);
            g.DrawRectangle(penBandaLocal, Lx, y, Pw, BandaH);
            var szLocal = g.MeasureString(f.Local, fntLocal);
            g.DrawString(f.Local, fntLocal, System.Drawing.Brushes.Black, Lx + 10f, y + (BandaH - szLocal.Height) / 2f);
            y += BandaH;

            // Caja gris muy claro con etiqueta + monto — clara a propósito: un gris
            // más oscuro se ve casi negro al imprimir en blanco y negro (impresoras
            // térmicas/láser sin color), tapando el contraste del monto.
            using var bGris = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(248, 248, 249));
            g.FillRectangle(bGris, Lx, y, Pw, valorH);
            using var penGris = new System.Drawing.Pen(System.Drawing.Color.FromArgb(210, 210, 212), 0.8f);
            g.DrawRectangle(penGris, Lx, y, Pw, valorH);

            if (p.TituloTipo == "ENTRADA - SALIDA")
            {
                // Ingresos y Egresos por separado, más el Total (neto) — antes solo se veía
                // el neto, sin poder saber cuánto entró y cuánto salió realmente ese período.
                using var bVerde = new System.Drawing.SolidBrush(brVerde);
                using var bRojo2 = new System.Drawing.SolidBrush(brRojo);
                FilaValor("INGRESOS", f.Ingresos, y, ValorH, bVerde);
                using (var penDiv = new System.Drawing.Pen(System.Drawing.Color.FromArgb(210, 210, 212), 0.6f))
                    g.DrawLine(penDiv, Lx, y + ValorH + 4, Lx + Pw, y + ValorH + 4);
                FilaValor("EGRESOS", f.Egresos, y + ValorH + 8, ValorH, bRojo2);
            }
            else
            {
                // "ENTRADA" y el monto van en la MISMA fila horizontal, no apilados en dos
                // líneas — etiqueta a la izquierda, monto a la derecha, ambos centrados
                // respecto al alto de la caja usando su propia altura de texto.
                FilaValor(p.TituloTipo, f.Total, y, ValorH, System.Drawing.Brushes.Black);
            }

            y += valorH + GapH;
        }

        // ── Banda final: TOTAL GENERAL consolidado (todos los locales) ─────────
        // Solo en la última página, después de la última banda de local — mismo
        // estilo que las demás bandas (contorno azul, fondo blanco, texto negro),
        // pero centrada y con fuente más grande para que resalte como cierre del reporte.
        if (!hayMas)
        {
            const int totalBandaH = 32;
            decimal totalGeneral = p.Filas.Sum(f => f.Total);

            using var penTotal = new System.Drawing.Pen(azul, 1.8f);
            g.DrawRectangle(penTotal, Lx, y, Pw, totalBandaH);

            using var fntTotal = new System.Drawing.Font("Arial", 12f, System.Drawing.FontStyle.Bold);
            string totalTxt = $"{Titulo1a(p.TituloTipo)} Total: {totalGeneral:N0}";
            DrawCentrado(g, totalTxt, fntTotal, System.Drawing.Brushes.Black, Lx, y, Pw, totalBandaH);
            y += totalBandaH + 6;

            using var fntFecha = new System.Drawing.Font("Arial", 6.5f, System.Drawing.FontStyle.Regular);
            var ahora = DateTime.Now;
            var cultEs = new System.Globalization.CultureInfo("es-ES");
            string diaTxt = cultEs.TextInfo.ToTitleCase(ahora.ToString("dddd", cultEs));
            string mesTxt = cultEs.TextInfo.ToTitleCase(ahora.ToString("MMMM", cultEs));
            string fechaTxt = $"{diaTxt}, {ahora.Day} de {mesTxt} del {ahora.Year}, Hora {ahora:HH:mm}";
            DrawCentrado(g, fechaTxt, fntFecha, System.Drawing.Brushes.Gray, Lx, y, Pw, 12);
        }

        string pagTxt = $"{pageNum}";
        var szPag = g.MeasureString(pagTxt, fntPag);
        using var bPag = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(120, 120, 140));
        g.DrawString(pagTxt, fntPag, bPag, Lx + Pw - szPag.Width, PgH - 26);

        return hayMas;
    }

    // "ENTRADA" -> "Entrada", "SALIDA" -> "Salida", "ENTRADA - SALIDA" -> "Entrada - Salida"
    private static string Titulo1a(string tituloTipo)
    {
        var partes = tituloTipo.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", partes.Select(w =>
            w.Length == 0 ? w : (w == "-" ? w : char.ToUpper(w[0]) + w[1..].ToLower())));
    }

    private static void DrawCentrado(System.Drawing.Graphics g, string txt, System.Drawing.Font f,
        System.Drawing.Brush br, float x, float y, float w, float h)
    {
        var sz = g.MeasureString(txt, f);
        g.DrawString(txt, f, br, x + Math.Max(0f, (w - sz.Width) / 2f), y + Math.Max(0f, (h - sz.Height) / 2f));
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

// ── DETALLE — agrupado por Usuario (cajero), fila por fila ──────────────────────

public record FilaArqueoDetalle(
    bool    EsHeaderUsuario,
    string  Usuario,
    string  LocalEtiqueta,
    string  Concepto,
    string  Metodo,
    decimal Monto,
    string  FechaStr,
    string  Obs,
    string  Tipo = ""
);

public class CajaArqueoDetallePagina
{
    public List<FilaArqueoDetalle> Filas { get; init; } = new();
    public string TituloTipo { get; init; } = "ENTRADAS"; // ENTRADAS / SALIDAS / ENTRADA-SALIDA
    public string Desde      { get; init; } = "";
    public string Hasta      { get; init; } = "";
    public string FechaImp   { get; init; } = "";
    public string Usuario    { get; init; } = "";
    public string LogoPath   { get; init; } = "";

    public static string ResolverLogoPath() => CrediSoft.UI.Views.Maestros.ArticulosPagina.ResolverLogoPath();
}

public static class CajaArqueoDetalleDibujador
{
    // Landscape A4: 1169 × 827 — mismo formato que Gastos de Caja
    // Orden: Concepto, Método, Monto Gs., Tipo, Fecha, Local, Observación — la columna "Tipo"
    // (Ingreso/Egreso) reemplaza lo que antes era "Monto Gs." repetido dos veces por error.
    private static readonly int[] Cws         = { 150, 100, 110, 80, 150, 100, 379 };
    private static readonly bool[] RightAlign = { false, false, true, false, false, false, false };

    private const int PgW = 1169;
    private const int PgH = 827;
    private const int Lx  = 20;
    private const int Pw  = PgW - 40;

    public static int ContarPaginas(CajaArqueoDetallePagina p)
    {
        const int hdrH = 20, rowH = 22, userH = 24, subtotalH = 20;
        int y = 88 + 2 + hdrH + 3;
        int paginas = 1;
        bool hayBloqueAbierto = false;
        foreach (var f in p.Filas)
        {
            // Cada header de usuario va seguido de un header de columnas repetido (userH + hdrH+3)
            // y, si no es el primer vendedor, precedido del subtotal (subtotalH) del bloque
            // anterior — igual que DibujarPagina, para que el alto estimado acá no se quede
            // corto respecto al que realmente ocupa el dibujo.
            int alto = f.EsHeaderUsuario
                ? userH + hdrH + 3 + (hayBloqueAbierto ? subtotalH : 0)
                : rowH;
            if (y + alto > PgH - 40) { paginas++; y = 88 + 2 + hdrH + 3; }
            y += alto;
            if (f.EsHeaderUsuario) hayBloqueAbierto = true;
        }
        if (hayBloqueAbierto) y += subtotalH; // subtotal del último bloque
        return paginas;
    }

    public static bool DibujarPagina(
        System.Drawing.Graphics g,
        CajaArqueoDetallePagina p,
        System.Drawing.Image? logo,
        ref int filaOffset,
        int pageNum,
        int totalPages)
    {
        var azul = System.Drawing.Color.FromArgb(21, 79, 110);
        var rojo = System.Drawing.Color.FromArgb(198, 40, 40);

        using var fntHdr  = new System.Drawing.Font("Arial", 7.5f, System.Drawing.FontStyle.Bold);
        using var fntRow  = new System.Drawing.Font("Arial", 7f,   System.Drawing.FontStyle.Regular);
        using var fntUser = new System.Drawing.Font("Arial", 8f,   System.Drawing.FontStyle.Bold);
        using var fntFoot = new System.Drawing.Font("Arial", 7.5f, System.Drawing.FontStyle.Bold);
        using var fntPag  = new System.Drawing.Font("Arial", 6.5f, System.Drawing.FontStyle.Regular);
        using var brIngreso = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(46, 125, 50));
        using var brEgreso  = new System.Drawing.SolidBrush(rojo);

        if (logo != null)
        {
            float wmW = 500f, wmH = logo.Height * (wmW / logo.Width);
            DrawConOpacidad(g, logo, (int)(PgW / 2f - wmW / 2f), (int)(PgH / 2f - wmH / 2f), (int)wmW, (int)wmH, 0.055f);
        }

        // ── Encabezado ────────────────────────────────────────────────────────
        g.FillRectangle(System.Drawing.Brushes.White, 0, 0, PgW, 86);
        using var penAzul = new System.Drawing.Pen(azul, 3.5f);
        g.DrawLine(penAzul, 0, 2, PgW, 2);

        if (logo != null)
        {
            float logoH2 = 60f, logoW2 = logo.Width * (logoH2 / logo.Height);
            DrawConOpacidad(g, logo, Lx, 8, (int)logoW2, (int)logoH2, 1f);

            float sepX = Lx + logoW2 + 12f;
            using var penGris = new System.Drawing.Pen(System.Drawing.Color.FromArgb(190, 190, 190), 1f);
            g.DrawLine(penGris, sepX, 6, sepX, 76);

            float tx = sepX + 10f, tpw = Pw - (sepX - Lx) - 10f;
            DibujarBandaTitulo(g, azul, tx, tpw, p.TituloTipo);
            DibujarSubtitulos(g, p, tx, tpw);
        }
        else
        {
            DibujarBandaTitulo(g, azul, Lx, Pw, p.TituloTipo);
            DibujarSubtitulos(g, p, Lx, Pw);
        }

        g.DrawLine(penAzul, 0, 78, PgW, 78);
        using var penGris2 = new System.Drawing.Pen(System.Drawing.Color.FromArgb(190, 190, 190), 0.5f);
        g.DrawLine(penGris2, 0, 80, PgW, 80);

        int y = 88;

        const int hdrH = 20;
        using var penHdrTop = new System.Drawing.Pen(System.Drawing.Color.FromArgb(160, 160, 160), 0.8f);
        g.DrawLine(penHdrTop, Lx, y, Lx + Pw, y);
        y += 2;

        string[] hdrs = { "Concepto", "Método", "Monto Gs.", "Tipo", "Fecha", "Local", "Observación" };
        void DibujarHeader(int yh)
        {
            for (int i = 0; i < hdrs.Length; i++)
                Celda(g, hdrs[i], fntHdr, i, yh, hdrH, System.Drawing.Brushes.Black, RightAlign[i]);
        }
        DibujarHeader(y);

        y += hdrH;
        using var penHdrBot = new System.Drawing.Pen(azul, 1.5f);
        g.DrawLine(penHdrBot, Lx, y, Lx + Pw, y);
        y += 3;

        const int rowH = 22, userH = 24, subtotalH = 20;
        bool hayMas = false;
        int contador = 0;
        // Acumulador del bloque de vendedor en curso (Ingresos - Egresos) — se resetea al
        // entrar a un header nuevo y se dibuja justo antes de pasar al siguiente (o al cerrar
        // la página), en vez de tener que buscar hacia atrás/adelante en p.Filas.
        decimal subtotalActual = 0;
        bool hayBloqueAbierto = false;

        void DibujarSubtotal()
        {
            var bSub = subtotalActual < 0 ? brEgreso : brIngreso;
            using var bFondoSub = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(238, 238, 238));
            g.FillRectangle(bFondoSub, Lx, y, Pw, subtotalH);
            var txt = $"Subtotal: Gs. {subtotalActual:N0}";
            var szSub = g.MeasureString(txt, fntUser);
            g.DrawString(txt, fntUser, bSub, Lx + Pw - szSub.Width - 8f, y + (subtotalH - szSub.Height) / 2f);
            y += subtotalH;
        }

        while (filaOffset < p.Filas.Count)
        {
            var f = p.Filas[filaOffset];
            // El subtotal del vendedor anterior ocupa espacio también — sin sumarlo acá, un
            // header nuevo podía "caber" en la página pero el subtotal que lo precede quedaba
            // cortado o superpuesto con el borde inferior.
            int altoExtra = f.EsHeaderUsuario && hayBloqueAbierto ? subtotalH : 0;
            int alto = (f.EsHeaderUsuario ? userH : rowH) + altoExtra;
            if (y + alto > PgH - 40) { hayMas = true; break; }
            filaOffset++;

            if (f.EsHeaderUsuario)
            {
                if (hayBloqueAbierto) DibujarSubtotal();

                using var bRojo = new System.Drawing.SolidBrush(rojo);
                g.FillRectangle(bRojo, Lx, y, Pw, userH);
                var etiqueta = string.IsNullOrEmpty(f.LocalEtiqueta) ? f.Usuario : $"{f.Usuario}   {f.LocalEtiqueta}";
                var szU = g.MeasureString(etiqueta, fntUser);
                g.DrawString(etiqueta, fntUser, System.Drawing.Brushes.White, Lx + 8f, y + (userH - szU.Height) / 2f);
                y += userH;

                // Pedido explícito: repetir el encabezado de columnas debajo de cada vendedor —
                // con varias secciones seguidas, el header original (arriba del todo) quedaba
                // lejos y era fácil perder de vista qué representaba cada columna.
                if (y + hdrH > PgH - 40) { hayMas = true; filaOffset--; break; }
                DibujarHeader(y);
                y += hdrH;
                using (var penHdrBot2 = new System.Drawing.Pen(azul, 1.2f))
                    g.DrawLine(penHdrBot2, Lx, y, Lx + Pw, y);
                y += 3;

                contador = 0;
                subtotalActual = 0;
                hayBloqueAbierto = true;
                continue;
            }

            if (contador % 2 == 1)
            {
                using var bAlt = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(244, 248, 250));
                g.FillRectangle(bAlt, Lx, y, Pw, rowH);
            }
            contador++;
            subtotalActual += f.Tipo == "Egreso" ? -f.Monto : f.Monto;

            string montoTxt = f.Monto.ToString("N0");
            string[] vals = { f.Concepto, f.Metodo, montoTxt, f.Tipo, f.FechaStr, f.LocalEtiqueta, f.Obs };
            for (int i = 0; i < vals.Length; i++)
            {
                System.Drawing.Brush brCelda = System.Drawing.Brushes.Black;
                if (i == 3 && f.Tipo == "Ingreso") brCelda = brIngreso;
                else if (i == 3 && f.Tipo == "Egreso") brCelda = brEgreso;
                Celda(g, vals[i], fntRow, i, y, rowH, brCelda, RightAlign[i]);
            }

            y += rowH;
            using var penSep = new System.Drawing.Pen(System.Drawing.Color.FromArgb(200, 215, 230), 0.5f);
            g.DrawLine(penSep, Lx, y, Lx + Pw, y);
        }

        // Si la página termina y el bloque del vendedor actual sigue (hayMas=true, más filas
        // de ESE mismo usuario en la próxima página), no hay forma de saber su total real acá
        // — subtotalActual es local a esta página y no se acarrea a la siguiente. Se omite en
        // ese caso puntual (poco común: un vendedor con movimientos suficientes para no entrar
        // en una sola página); si el bloque sí terminó en esta página, se dibuja normalmente.
        var bloqueTerminaEnEstaPagina = !hayMas ||
            (filaOffset < p.Filas.Count && p.Filas[filaOffset].EsHeaderUsuario);
        if (hayBloqueAbierto && bloqueTerminaEnEstaPagina) DibujarSubtotal();

        string pagTxt = $"Página {pageNum} de {totalPages}";
        var szPag = g.MeasureString(pagTxt, fntPag);
        using var bPag = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(120, 120, 140));
        g.DrawString(pagTxt, fntPag, bPag, Lx + Pw - szPag.Width, PgH - 26);

        if (!hayMas)
        {
            y += 4;
            using (var p1 = new System.Drawing.Pen(azul, 1.5f))
                g.DrawLine(p1, Lx, y, Lx + Pw, y);
            y += 6;

            var datos = p.Filas.Where(f => !f.EsHeaderUsuario).ToList();
            float fhFoot = fntFoot.GetHeight(g);
            float footY = y + Math.Max(0f, (18 - fhFoot) / 2f);
            g.DrawString($"Registros: {datos.Count}", fntFoot, System.Drawing.Brushes.Black, Lx + 2f, footY);

            // Total centrado y más grande — pedido explícito para que resalte como cifra final
            // del reporte, en vez de compartir el mismo tamaño discreto que "Registros".
            using var fntTotal = new System.Drawing.Font("Arial", 11f, System.Drawing.FontStyle.Bold);
            using var bAzul = new System.Drawing.SolidBrush(azul);
            DrawCentrado(g, $"Total: Gs. {datos.Sum(f => f.Monto):N0}", fntTotal, bAzul, Lx, y - 2f, Pw, 22f);
        }

        return hayMas;
    }

    private static void DibujarBandaTitulo(System.Drawing.Graphics g, System.Drawing.Color azul, float tx, float tpw, string tipo)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        const float r5 = 5f;
        path.AddArc(tx, 8, r5 * 2, r5 * 2, 180, 90);
        path.AddArc(tx + tpw - r5 * 2, 8, r5 * 2, r5 * 2, 270, 90);
        path.AddArc(tx + tpw - r5 * 2, 8 + 34 - r5 * 2, r5 * 2, r5 * 2, 0, 90);
        path.AddArc(tx, 8 + 34 - r5 * 2, r5 * 2, r5 * 2, 90, 90);
        path.CloseFigure();
        using var bBanda = new System.Drawing.SolidBrush(azul);
        g.FillPath(bBanda, path);
        using var fntTit = new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Bold);
        DrawCentrado(g, $"ARQUEO DE CAJA — {tipo}", fntTit, System.Drawing.Brushes.White, tx + 8, 8, tpw - 12, 34);
    }

    private static void DibujarSubtitulos(System.Drawing.Graphics g, CajaArqueoDetallePagina p, float tx, float tpw)
    {
        var azul = System.Drawing.Color.FromArgb(21, 79, 110);
        using var penAT = new System.Drawing.Pen(azul, 1f);
        g.DrawLine(penAT, tx, 47, tx + tpw, 47);

        using var bSubBg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(232, 240, 254));
        g.FillRectangle(bSubBg, tx, 50, tpw, 24f);
        using var penSubBd = new System.Drawing.Pen(System.Drawing.Color.FromArgb(187, 222, 251), 0.8f);
        g.DrawRectangle(penSubBd, tx, 50, tpw, 24f);

        using var fntSub  = new System.Drawing.Font("Arial", 7f, System.Drawing.FontStyle.Regular);
        using var bSubTxt = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(13, 71, 161));
        string linea1 = $"Período: {p.Desde} al {p.Hasta}";
        DrawCentrado(g, linea1, fntSub, bSubTxt, tx + 4, 50, tpw - 8, 12);
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
        float tx;
        if (right) { var sz = g.MeasureString(txt, f); tx = colX + colW - sz.Width - 2f; }
        else tx = colX + 2f;
        g.DrawString(txt, f, br, tx, ty);
    }

    private static string Truncar(System.Drawing.Graphics g, string txt, System.Drawing.Font f, float maxW)
    {
        if (g.MeasureString(txt, f).Width <= maxW) return txt;
        const string e = "…";
        float ew = g.MeasureString(e, f).Width;
        while (txt.Length > 0 && g.MeasureString(txt, f).Width + ew > maxW)
            txt = txt[..^1];
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
