-- Se quita el costo del producto y se agrega el precio para consumo en el local.
-- A los productos existentes se les copia el precio de venta como punto de partida.

ALTER TABLE gomorra.productos
    DROP COLUMN costo,
    ADD COLUMN precio_local numeric(12,2) NULL CHECK (precio_local >= 0);

UPDATE gomorra.productos SET precio_local = precio_venta;

ALTER TABLE gomorra.productos
    ALTER COLUMN precio_local SET NOT NULL;
