-- Cuentas abiertas por mesa o cliente: los pedidos se acumulan y se pagan al final.
-- El inventario se descuenta al cobrar (la cuenta pagada genera una venta normal).
-- Mientras tanto, lo pedido en cuentas abiertas no está "disponible" para otras ventas.
--
-- Además, pago mixto: cada venta guarda cuánto se pagó en efectivo y cuánto por transferencia.

-- ---------- Pago mixto en ventas ----------

ALTER TABLE gomorra.ventas
    ADD COLUMN pago_efectivo      numeric(12,2) NOT NULL DEFAULT 0 CHECK (pago_efectivo >= 0),
    ADD COLUMN pago_transferencia numeric(12,2) NOT NULL DEFAULT 0 CHECK (pago_transferencia >= 0);

UPDATE gomorra.ventas
SET pago_efectivo      = CASE WHEN metodo_pago = 'efectivo' THEN total ELSE 0 END,
    pago_transferencia = CASE WHEN metodo_pago = 'transferencia' THEN total ELSE 0 END;

ALTER TABLE gomorra.ventas
    DROP CONSTRAINT ventas_metodo_pago_check,
    ADD CONSTRAINT ventas_metodo_pago_check CHECK (metodo_pago IN ('efectivo', 'transferencia', 'mixto')),
    ADD CONSTRAINT ventas_pago_cuadra CHECK (pago_efectivo + pago_transferencia = total),
    ADD CONSTRAINT ventas_pago_coherente CHECK (
        (metodo_pago = 'efectivo'      AND pago_transferencia = 0) OR
        (metodo_pago = 'transferencia' AND pago_efectivo = 0) OR
        (metodo_pago = 'mixto'         AND pago_efectivo > 0 AND pago_transferencia > 0)
    );

-- ---------- Cuentas abiertas ----------

CREATE TABLE gomorra.cuentas (
    id          bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    caja_id     bigint       NOT NULL REFERENCES gomorra.cajas (id),
    nombre      text         NOT NULL CHECK (length(trim(nombre)) > 0),
    estado      text         NOT NULL DEFAULT 'abierta' CHECK (estado IN ('abierta', 'pagada', 'anulada')),
    venta_id    bigint       NULL REFERENCES gomorra.ventas (id),
    abierta_en  timestamptz  NOT NULL DEFAULT now(),
    cerrada_en  timestamptz  NULL,

    CONSTRAINT cuentas_estado_coherente CHECK (
        (estado = 'abierta' AND venta_id IS NULL     AND cerrada_en IS NULL) OR
        (estado = 'pagada'  AND venta_id IS NOT NULL AND cerrada_en IS NOT NULL) OR
        (estado = 'anulada' AND venta_id IS NULL     AND cerrada_en IS NOT NULL)
    )
);

-- No puede haber dos cuentas abiertas con el mismo nombre en la misma caja.
CREATE UNIQUE INDEX cuentas_nombre_abierta_unico
    ON gomorra.cuentas (caja_id, lower(nombre)) WHERE estado = 'abierta';
CREATE INDEX cuentas_caja_estado_idx ON gomorra.cuentas (caja_id, estado);

-- Una fila por producto; cada ronda suma a la cantidad. El precio se toma al cobrar.
CREATE TABLE gomorra.cuentas_detalle (
    id              bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    cuenta_id       bigint       NOT NULL REFERENCES gomorra.cuentas (id),
    producto_id     bigint       NOT NULL REFERENCES gomorra.productos (id),
    cantidad        integer      NOT NULL CHECK (cantidad > 0),
    actualizado_en  timestamptz  NOT NULL DEFAULT now(),

    CONSTRAINT cuentas_detalle_producto_unico UNIQUE (cuenta_id, producto_id)
);

CREATE INDEX cuentas_detalle_producto_idx ON gomorra.cuentas_detalle (producto_id);
