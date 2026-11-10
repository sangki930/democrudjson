-- Widen the existing column without changing existing IDs or JSON data.
ALTER TABLE democrud_demo."LAYER" ALTER COLUMN id TYPE varchar(24);
