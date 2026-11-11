-- La presentación pasa de mililitros a texto libre (Media, Botella, Litro, Garrafa, "350 ml"…),
-- para poder agrupar los productos por ella. Los valores en ml se convierten a las opciones nuevas.

ALTER TABLE gomorra.productos
    ADD COLUMN presentacion text NULL CHECK (length(trim(presentacion)) BETWEEN 1 AND 40);

UPDATE gomorra.productos SET presentacion = CASE
    WHEN presentacion_ml = 375   THEN 'Media'
    WHEN presentacion_ml = 750   THEN 'Botella'
    WHEN presentacion_ml = 1000  THEN 'Litro'
    WHEN presentacion_ml >= 1750 THEN 'Garrafa'
    WHEN presentacion_ml IS NOT NULL THEN presentacion_ml || ' ml'
END;

ALTER TABLE gomorra.productos DROP COLUMN presentacion_ml;
