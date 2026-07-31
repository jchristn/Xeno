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

DO
$do$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'netledger') THEN
        CREATE ROLE netledger LOGIN PASSWORD 'netledger';
    ELSE
        ALTER ROLE netledger WITH LOGIN PASSWORD 'netledger';
    END IF;
END
$do$;

DO
$do$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'less3') THEN
        CREATE ROLE less3 LOGIN PASSWORD 'less3';
    ELSE
        ALTER ROLE less3 WITH LOGIN PASSWORD 'less3';
    END IF;
END
$do$;

SELECT 'CREATE DATABASE xeno OWNER xeno'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'xeno')\gexec

ALTER DATABASE xeno OWNER TO xeno;
GRANT ALL PRIVILEGES ON DATABASE xeno TO xeno;

SELECT 'CREATE DATABASE netledger OWNER netledger'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'netledger')\gexec

ALTER DATABASE netledger OWNER TO netledger;
GRANT CONNECT, TEMPORARY ON DATABASE netledger TO netledger;

SELECT 'CREATE DATABASE less3 OWNER less3'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'less3')\gexec

ALTER DATABASE less3 OWNER TO less3;
GRANT CONNECT, TEMPORARY ON DATABASE less3 TO less3;
