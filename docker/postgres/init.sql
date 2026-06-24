DO
$do$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'xeno') THEN
        CREATE ROLE xeno LOGIN PASSWORD 'xeno';
    ELSE
        ALTER ROLE xeno WITH LOGIN PASSWORD 'xeno';
    END IF;
END
$do$;

SELECT 'CREATE DATABASE xeno OWNER xeno'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'xeno')\gexec

ALTER DATABASE xeno OWNER TO xeno;
GRANT ALL PRIVILEGES ON DATABASE xeno TO xeno;
