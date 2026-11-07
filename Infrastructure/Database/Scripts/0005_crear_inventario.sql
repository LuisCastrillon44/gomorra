-- Inventario: stock actual por producto + historial de movimientos (entrada, salida, ajuste).
-- Un producto sin fila en inventario se considera con stock 0 y mínimo 0.

CREATE TABLE gomorra.inventario (
    producto_id     bigint      PRIMARY KEY REFERENCES gomorra.productos (id),
    stock           integer     NOT NULL DEFAULT 0 CHECK (stock >= 0),
    stock_minimo    integer     NOT NULL DEFAULT 0 CHECK (stock_minimo >= 0),
    actualizado_en  timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE gomorra.movimientos_inventario (
    id                bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    producto_id       bigint        NOT NULL REFERENCES gomorra.productos (id),
    tipo              text          NOT NULL CHECK (tipo IN ('entrada', 'salida', 'ajuste')),
    -- Cambio con signo: entradas suman, salidas restan, ajustes pueden ir en cualquier sentido.
    cantidad          integer       NOT NULL,
    stock_resultante  integer       NOT NULL CHECK (stock_resultante >= 0),
    -- Costo de esa compra (dato histórico). Solo aplica a entradas.
    costo_unitario    numeric(12,2) NULL CHECK (costo_unitario >= 0),
    creado_en         timestamptz   NOT NULL DEFAULT now(),

    CONSTRAINT movimientos_cantidad_segun_tipo CHECK (
        (tipo = 'entrada' AND cantidad > 0) OR
        (tipo = 'salida'  AND cantidad < 0) OR
        (tipo = 'ajuste'  AND cantidad <> 0)
    ),
    CONSTRAINT movimientos_costo_solo_entradas CHECK (costo_unitario IS NULL OR tipo = 'entrada')
);

CREATE INDEX movimientos_inventario_producto_fecha_idx
    ON gomorra.movimientos_inventario (producto_id, creado_en DESC);
