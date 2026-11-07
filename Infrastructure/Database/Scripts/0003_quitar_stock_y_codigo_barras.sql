-- El stock y el código de barras salen del producto: serán parte de otro módulo (inventario).
-- Al quitar las columnas también caen su CHECK (stock >= 0) y el índice productos_codigo_barras_unico.

ALTER TABLE gomorra.productos
    DROP COLUMN stock,
    DROP COLUMN codigo_barras;
