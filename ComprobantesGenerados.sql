IF OBJECT_ID('dbo.COMPROBANTES_GENERADOS', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.COMPROBANTES_GENERADOS
    (
        IDCOMPROBANTE      INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        TIPO               VARCHAR(20)  NOT NULL,   -- 'COBRO' | 'VENTA_CONTADO' | 'VENTA_CREDITO' | 'SOLICITUD_APROBADA'
        NUMERO_TICKET      BIGINT       NOT NULL,
        ID_LOCAL           INT          NOT NULL,
        FECHA_EMISION      DATETIME     NOT NULL CONSTRAINT DF_COMPROBANTES_GENERADOS_FECHA DEFAULT(GETDATE()),
        ID_USUARIO_CAJERO  INT          NULL,
        NOMBRE_CAJERO      VARCHAR(100) NULL,
        ID_CLIENTE         INT          NULL,
        NOMBRE_CLIENTE     VARCHAR(150) NULL,
        -- Referencias a la operación de origen — permiten ubicar el comprobante desde otras
        -- pantallas (ej. "ver cobros de esta cuota") sin depender solo de NUMERO_TICKET.
        ID_CAB             INT          NULL,
        ID_GENERADAS       INT          NULL,
        NRO_SOLICITUD      VARCHAR(30)  NULL,
        NRECIBO            VARCHAR(30)  NULL,
        MONTO_TOTAL        DECIMAL(18,2) NULL,
        -- Snapshot completo del objeto DatosTicketCobro/DatosTicketVenta tal cual se usó para
        -- dibujar el ticket original — la reimpresión deserializa esto y vuelve a dibujar
        -- exactamente lo mismo, sin depender de reconstruir el dato desde otras tablas (que
        -- pueden haber cambiado desde entonces, ej. GENERADAS.PUNITORIO recalculado).
        DATOS_JSON         NVARCHAR(MAX) NOT NULL,
        ID_USUARIO_REGISTRO INT         NULL
    );

    CREATE INDEX IX_COMPROBANTES_GENERADOS_TICKET
        ON dbo.COMPROBANTES_GENERADOS (NUMERO_TICKET, ID_LOCAL, TIPO);

    CREATE INDEX IX_COMPROBANTES_GENERADOS_FECHA
        ON dbo.COMPROBANTES_GENERADOS (FECHA_EMISION DESC);

    CREATE INDEX IX_COMPROBANTES_GENERADOS_CLIENTE
        ON dbo.COMPROBANTES_GENERADOS (ID_CLIENTE, FECHA_EMISION DESC);

    CREATE INDEX IX_COMPROBANTES_GENERADOS_CAB
        ON dbo.COMPROBANTES_GENERADOS (ID_CAB, FECHA_EMISION DESC);

    CREATE INDEX IX_COMPROBANTES_GENERADOS_GENERADAS
        ON dbo.COMPROBANTES_GENERADOS (ID_GENERADAS, FECHA_EMISION DESC);
END
