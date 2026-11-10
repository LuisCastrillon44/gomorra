-- El login pasa a ser por nombre de usuario; el correo queda opcional.

ALTER TABLE gomorra.usuarios ADD COLUMN usuario text NULL;

-- Usuarios existentes: se toma la parte del correo antes de la @.
UPDATE gomorra.usuarios SET usuario = lower(split_part(email, '@', 1)) WHERE usuario IS NULL;

ALTER TABLE gomorra.usuarios
    ALTER COLUMN usuario SET NOT NULL,
    ADD CONSTRAINT usuarios_usuario_check CHECK (length(trim(usuario)) > 0),
    ALTER COLUMN email DROP NOT NULL;

CREATE UNIQUE INDEX usuarios_usuario_unico ON gomorra.usuarios (lower(usuario));

-- usuarios_email_unico (lower(email)) se mantiene: en Postgres varios NULL no chocan entre sí.
