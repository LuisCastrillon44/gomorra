-- Caja: turnos de caja (apertura con base, cierre con arqueo), ventas con su detalle
-- y entradas/salidas de efectivo. Las ventas descuentan inventario.
--
-- Efectivo esperado = base_inicial + ventas en efectivo no anuladas + entradas - salidas.

CREATE TABLE gomorra.cajas (
    id                 bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    estado             text          NOT NULL DEFAULT 'abierta' CHECK (estado IN ('abierta', 'cerrada')),
    base_inicial       numeric(12,2) NOT NULL CHECK (base_inicial >= 0),
    abierta_en         timestamptz   NOT NULL DEFAULT now(),
    cerrada_en         timestamptz   NULL,
    -- Foto del efectivo esperado en el momento del cierre.
    efectivo_esperado  numeric(12,2) NULL,
    efectivo_contado   numeric(12,2) NULL CHECK (efectivo_contado >= 0),
    nota_cierre        text          NULL,

    CONSTRAINT cajas_cierre_coherente CHECK (
        (estado = 'abierta' AND cerrada_en IS NULL AND efectivo_esperado IS NULL AND efectivo_contado IS NULL) OR
        (estado = 'cerrada' AND cerrada_en IS NOT NULL AND efectivo_esperado IS NOT NULL AND efectivo_contado IS NOT NULL)
    )
);

-- Solo puede haber una caja abierta a la vez.
CREATE UNIQUE INDEX cajas_una_abierta ON gomorra.cajas ((true)) WHERE estado = 'abierta';
CREATE INDEX cajas_abierta_en_idx ON gomorra.cajas (abierta_en DESC);

CREATE TABLE gomorra.ventas (
    id           bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    caja_id      bigint        NOT NULL REFERENCES gomorra.cajas (id),
    metodo_pago  text          NOT NULL CHECK (metodo_pago IN ('efectivo', 'transferencia')),
    total        numeric(12,2) NOT NULL CHECK (total >= 0),
    anulada      boolean       NOT NULL DEFAULT false,
    anulada_en   timestamptz   NULL,
    creado_en    timestamptz   NOT NULL DEFAULT now(),

    CONSTRAINT ventas_anulacion_coherente CHECK (anulada = (anulada_en IS NOT NULL))
);

CREATE INDEX ventas_caja_id_idx ON gomorra.ventas (caja_id, creado_en DESC);

CREATE TABLE gomorra.ventas_detalle (
    id               bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    venta_id         bigint        NOT NULL REFERENCES gomorra.ventas (id),
    producto_id      bigint        NOT NULL REFERENCES gomorra.productos (id),
    cantidad         integer       NOT NULL CHECK (cantidad > 0),
    -- Foto de productos.precio_venta al momento de vender.
    precio_unitario  numeric(12,2) NOT NULL CHECK (precio_unitario >= 0),
    subtotal         numeric(12,2) NOT NULL GENERATED ALWAYS AS (cantidad * precio_unitario) STORED,

    CONSTRAINT ventas_detalle_producto_unico UNIQUE (venta_id, producto_id)
);

CREATE TABLE gomorra.movimientos_caja (
    id         bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    caja_id    bigint        NOT NULL REFERENCES gomorra.cajas (id),
    tipo       text          NOT NULL CHECK (tipo IN ('entrada', 'salida')),
    monto      numeric(12,2) NOT NULL CHECK (monto > 0),
    motivo     text          NOT NULL CHECK (length(trim(motivo)) > 0),
    creado_en  timestamptz   NOT NULL DEFAULT now()
);

CREATE INDEX movimientos_caja_caja_id_idx ON gomorra.movimientos_caja (caja_id, creado_en DESC);

-- Inventario: las ventas y sus anulaciones quedan como movimientos enlazados a la venta.
ALTER TABLE gomorra.movimientos_inventario
    ADD COLUMN venta_id bigint NULL REFERENCES gomorra.ventas (id),
    DROP CONSTRAINT movimientos_inventario_tipo_check,
    DROP CONSTRAINT movimientos_cantidad_segun_tipo,
    ADD CONSTRAINT movimientos_inventario_tipo_check
        CHECK (tipo IN ('entrada', 'salida', 'ajuste', 'venta', 'anulacion_venta')),
    ADD CONSTRAINT movimientos_cantidad_segun_tipo CHECK (
        (tipo = 'entrada'         AND cantidad > 0) OR
        (tipo = 'salida'          AND cantidad < 0) OR
        (tipo = 'ajuste'          AND cantidad <> 0) OR
        (tipo = 'venta'           AND cantidad < 0) OR
        (tipo = 'anulacion_venta' AND cantidad > 0)
    ),
    ADD CONSTRAINT movimientos_venta_coherente
        CHECK ((venta_id IS NOT NULL) = (tipo IN ('venta', 'anulacion_venta')));

CREATE INDEX movimientos_inventario_venta_id_idx
    ON gomorra.movimientos_inventario (venta_id) WHERE venta_id IS NOT NULL;
