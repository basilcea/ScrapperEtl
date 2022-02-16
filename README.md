# ScrapperEtl

# Setting up postgres for Airflow locally
```
CREATE USER etl WITH PASSWORD 'etlPassword';
CREATE DATABASE airflow;
GRANT ALL PRIVILEGES ON DATABASE airflow TO etl;
ALTER USER etl SET search_path = public;

```