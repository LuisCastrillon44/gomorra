-- Catálogo: categorías y productos. El borrado es lógico (activo = false).

CREATE TABLE gomorra.categorias (
    id              bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre          text        NOT NULL,
    activo          boolean     NOT NULL DEFAULT true,
    creado_en       timestamptz NOT NULL DEFAULT now(),
    actualizado_en  timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX categorias_nombre_unico ON gomorra.categorias (lower(nombre));

CREATE TABLE gomorra.productos (
    id               bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    categoria_id     bigint        NOT NULL REFERENCES gomorra.categorias (id),
    nombre           text          NOT NULL,
    presentacion_ml  integer       NULL CHECK (presentacion_ml > 0),
    precio_venta     numeric(12,2) NOT NULL CHECK (precio_venta >= 0),
    costo            numeric(12,2) NOT NULL CHECK (costo >= 0),
    stock            integer       NOT NULL DEFAULT 0 CHECK (stock >= 0),
    codigo_barras    text          NULL,
    activo           boolean       NOT NULL DEFAULT true,
    creado_en        timestamptz   NOT NULL DEFAULT now(),
    actualizado_en   timestamptz   NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX productos_codigo_barras_unico ON gomorra.productos (codigo_barras) WHERE codigo_barras IS NOT NULL;
CREATE INDEX productos_categoria_id_idx ON gomorra.productos (categoria_id);
