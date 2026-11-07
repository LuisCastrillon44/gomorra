-- TEMPORAL: URL de la imagen del producto, mientras no exista la subida de imágenes.
-- Cuando se reemplace, quitar esta columna con un script nuevo.

ALTER TABLE gomorra.productos
    ADD COLUMN imagen_url text NULL;
