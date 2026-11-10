-- Seguridad: usuarios que inician sesión (email + clave) y refresh tokens con rotación.
-- Los roles y permisos llegan en una migración posterior.

CREATE TABLE gomorra.usuarios (
    id              bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    email           text         NOT NULL CHECK (length(trim(email)) > 0),
    nombre          text         NOT NULL CHECK (length(trim(nombre)) > 0),
    -- Hash BCrypt; la clave en texto plano nunca se guarda.
    clave_hash      text         NOT NULL,
    activo          boolean      NOT NULL DEFAULT true,
    ultimo_acceso   timestamptz  NULL,
    creado_en       timestamptz  NOT NULL DEFAULT now(),
    actualizado_en  timestamptz  NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX usuarios_email_unico ON gomorra.usuarios (lower(email));

-- Solo se guarda el SHA-256 del token: si se filtra la BD, los tokens no sirven.
-- Rotación: cada uso revoca el token y enlaza el nuevo en reemplazado_por.
CREATE TABLE gomorra.refresh_tokens (
    id               bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    usuario_id       bigint       NOT NULL REFERENCES gomorra.usuarios (id),
    token_hash       text         NOT NULL,
    expira_en        timestamptz  NOT NULL,
    revocado_en      timestamptz  NULL,
    reemplazado_por  bigint       NULL REFERENCES gomorra.refresh_tokens (id),
    creado_en        timestamptz  NOT NULL DEFAULT now(),

    CONSTRAINT refresh_tokens_token_hash_unico UNIQUE (token_hash)
);

CREATE INDEX refresh_tokens_usuario_id_idx ON gomorra.refresh_tokens (usuario_id);
