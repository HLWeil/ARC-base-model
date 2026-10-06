-- Core base-profile schema. Destructive recreation, not a migration.

PRAGMA foreign_keys = OFF;

BEGIN;

DROP VIEW IF EXISTS process_edges;

DROP VIEW IF EXISTS annotation_orphans;

DROP VIEW IF EXISTS core_validation_errors;

DROP TABLE IF EXISTS protocol_parameter;

DROP TABLE IF EXISTS protocol_additional_property;

DROP TABLE IF EXISTS process_io;

DROP TABLE IF EXISTS scholarly_article_additional_property;

DROP TABLE IF EXISTS scholarly_article_author;

DROP TABLE IF EXISTS scholarly_article_identifier;

DROP TABLE IF EXISTS scholarly_article_additional_type;

DROP TABLE IF EXISTS scholarly_article;

DROP TABLE IF EXISTS sample_additional_property;

DROP TABLE IF EXISTS sample_additional_type;

DROP TABLE IF EXISTS sample;

DROP TABLE IF EXISTS recipe_component;

DROP TABLE IF EXISTS recipe_additional_property;

DROP TABLE IF EXISTS recipe_parameter;

DROP TABLE IF EXISTS recipe_additional_type;

DROP TABLE IF EXISTS recipe;

DROP TABLE IF EXISTS process_parameter_value;

DROP TABLE IF EXISTS process_additional_type;

DROP TABLE IF EXISTS process;

DROP TABLE IF EXISTS organization_additional_type;

DROP TABLE IF EXISTS organization;

DROP TABLE IF EXISTS formal_parameter_additional_type;

DROP TABLE IF EXISTS formal_parameter;

DROP TABLE IF EXISTS descriptor_annotation;

DROP TABLE IF EXISTS descriptor_additional_type;

DROP TABLE IF EXISTS descriptor;

DROP TABLE IF EXISTS defined_term_set_additional_type;

DROP TABLE IF EXISTS defined_term_set;

DROP TABLE IF EXISTS defined_term_additional_type;

DROP TABLE IF EXISTS defined_term;

DROP TABLE IF EXISTS dataset_citation;

DROP TABLE IF EXISTS dataset_agent;

DROP TABLE IF EXISTS dataset_data_file;

DROP TABLE IF EXISTS dataset_descriptor;

DROP TABLE IF EXISTS dataset_additional_property;

DROP TABLE IF EXISTS dataset_has_part;

DROP TABLE IF EXISTS dataset_process;

DROP TABLE IF EXISTS dataset_identifier;

DROP TABLE IF EXISTS dataset_conforms_to;

DROP TABLE IF EXISTS dataset_additional_type;

DROP TABLE IF EXISTS dataset;

DROP TABLE IF EXISTS data_additional_property;

DROP TABLE IF EXISTS data_has_part;

DROP TABLE IF EXISTS data_additional_type;

DROP TABLE IF EXISTS data;

DROP TABLE IF EXISTS annotation_additional_type;

DROP TABLE IF EXISTS annotation;

DROP TABLE IF EXISTS agent_job_title;

DROP TABLE IF EXISTS agent_additional_property;

DROP TABLE IF EXISTS agent_identifier;

DROP TABLE IF EXISTS agent_affiliation;

DROP TABLE IF EXISTS agent_email;

DROP TABLE IF EXISTS agent_additional_type;

DROP TABLE IF EXISTS agent;

CREATE TABLE agent (id TEXT NOT NULL, type TEXT NOT NULL, name TEXT NOT NULL, given_name TEXT, family_name TEXT, PRIMARY KEY(id), CHECK(type = 'Agent'));

CREATE TABLE agent_additional_type (agent_id TEXT NOT NULL REFERENCES agent(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), value TEXT NOT NULL, PRIMARY KEY(agent_id,position));

CREATE TABLE agent_email (agent_id TEXT NOT NULL REFERENCES agent(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), value TEXT NOT NULL, PRIMARY KEY(agent_id,position));

CREATE TABLE agent_affiliation (agent_id TEXT NOT NULL REFERENCES agent(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), organization_id TEXT NOT NULL REFERENCES organization(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, PRIMARY KEY(agent_id,position));

CREATE TABLE agent_identifier (agent_id TEXT NOT NULL REFERENCES agent(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), value TEXT NOT NULL, PRIMARY KEY(agent_id,position));

CREATE TABLE agent_additional_property (agent_id TEXT NOT NULL REFERENCES agent(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), annotation_id TEXT NOT NULL REFERENCES annotation(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, PRIMARY KEY(agent_id,position));

CREATE TABLE agent_job_title (agent_id TEXT NOT NULL REFERENCES agent(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), defined_term_id TEXT NOT NULL REFERENCES defined_term(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, PRIMARY KEY(agent_id,position));

CREATE TABLE annotation (id TEXT NOT NULL, type TEXT NOT NULL, name TEXT NOT NULL, value_text TEXT, value_number REAL, unit TEXT, name_tan TEXT, value_tan TEXT, unit_tan TEXT, instance_of_id TEXT REFERENCES formal_parameter(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, PRIMARY KEY(id), CHECK(type = 'Annotation'), CHECK(value_text IS NULL OR value_number IS NULL), CHECK(value_text IS NULL OR typeof(value_text) = 'text'), CHECK(value_number IS NULL OR typeof(value_number) = 'real'));

CREATE INDEX idx_annotation_instance_of_id ON annotation(instance_of_id);

CREATE TABLE annotation_additional_type (annotation_id TEXT NOT NULL REFERENCES annotation(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), value TEXT NOT NULL, PRIMARY KEY(annotation_id,position));

CREATE TABLE data (id TEXT NOT NULL, type TEXT NOT NULL, path TEXT NOT NULL, selector TEXT, selector_format TEXT, encoding_format TEXT, PRIMARY KEY(id), CHECK(type = 'Data'));

CREATE TABLE data_additional_type (data_id TEXT NOT NULL REFERENCES data(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), value TEXT NOT NULL, PRIMARY KEY(data_id,position));

CREATE TABLE data_has_part (data_id TEXT NOT NULL REFERENCES data(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), part_data_id TEXT NOT NULL REFERENCES data(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, PRIMARY KEY(data_id,position));

CREATE TABLE data_additional_property (data_id TEXT NOT NULL REFERENCES data(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), annotation_id TEXT NOT NULL REFERENCES annotation(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, PRIMARY KEY(data_id,position));

CREATE TABLE dataset (id TEXT NOT NULL, type TEXT NOT NULL, title TEXT, description TEXT, license TEXT, date_published TEXT, date_created TEXT, date_modified TEXT, PRIMARY KEY(id), CHECK(type = 'Dataset'));

CREATE TABLE dataset_additional_type (dataset_id TEXT NOT NULL REFERENCES dataset(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), value TEXT NOT NULL, PRIMARY KEY(dataset_id,position));

CREATE TABLE dataset_conforms_to (dataset_id TEXT NOT NULL REFERENCES dataset(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), value TEXT NOT NULL, PRIMARY KEY(dataset_id,position));

CREATE TABLE dataset_identifier (dataset_id TEXT NOT NULL REFERENCES dataset(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), value TEXT NOT NULL, PRIMARY KEY(dataset_id,position));

CREATE TABLE dataset_process (dataset_id TEXT NOT NULL REFERENCES dataset(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), process_id TEXT NOT NULL REFERENCES process(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, PRIMARY KEY(dataset_id,position));

CREATE TABLE dataset_has_part (dataset_id TEXT NOT NULL REFERENCES dataset(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), part_dataset_id TEXT NOT NULL REFERENCES dataset(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, PRIMARY KEY(dataset_id,position));

CREATE INDEX idx_dataset_has_part_part_dataset_id ON dataset_has_part(part_dataset_id);

CREATE TABLE dataset_additional_property (dataset_id TEXT NOT NULL REFERENCES dataset(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), annotation_id TEXT NOT NULL REFERENCES annotation(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, PRIMARY KEY(dataset_id,position));

CREATE TABLE dataset_descriptor (dataset_id TEXT NOT NULL REFERENCES dataset(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), descriptor_id TEXT NOT NULL REFERENCES descriptor(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, PRIMARY KEY(dataset_id,position));

CREATE TABLE dataset_data_file (dataset_id TEXT NOT NULL REFERENCES dataset(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), data_id TEXT NOT NULL REFERENCES data(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, PRIMARY KEY(dataset_id,position));

CREATE TABLE dataset_agent (dataset_id TEXT NOT NULL REFERENCES dataset(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), agent_id TEXT NOT NULL REFERENCES agent(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, PRIMARY KEY(dataset_id,position));

CREATE TABLE dataset_citation (dataset_id TEXT NOT NULL REFERENCES dataset(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), scholarly_article_id TEXT NOT NULL REFERENCES scholarly_article(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, PRIMARY KEY(dataset_id,position));

CREATE TABLE defined_term (id TEXT NOT NULL, type TEXT NOT NULL, name TEXT NOT NULL, identifier TEXT, tan TEXT, in_defined_term_set_url TEXT, in_defined_term_set_id TEXT REFERENCES defined_term_set(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, PRIMARY KEY(id), CHECK(type = 'DefinedTerm'), CHECK(in_defined_term_set_url IS NULL OR in_defined_term_set_id IS NULL));

CREATE TABLE defined_term_additional_type (defined_term_id TEXT NOT NULL REFERENCES defined_term(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), value TEXT NOT NULL, PRIMARY KEY(defined_term_id,position));

CREATE TABLE defined_term_set (id TEXT NOT NULL, type TEXT NOT NULL, name TEXT NOT NULL, identifier TEXT, PRIMARY KEY(id), CHECK(type = 'DefinedTermSet'));

CREATE TABLE defined_term_set_additional_type (defined_term_set_id TEXT NOT NULL REFERENCES defined_term_set(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), value TEXT NOT NULL, PRIMARY KEY(defined_term_set_id,position));

CREATE TABLE descriptor (id TEXT NOT NULL, type TEXT NOT NULL, sample_id TEXT REFERENCES sample(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, data_id TEXT REFERENCES data(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, PRIMARY KEY(id), CHECK(type = 'Descriptor'), CHECK((sample_id IS NOT NULL) + (data_id IS NOT NULL) = 1));

CREATE INDEX idx_descriptor_sample_id ON descriptor(sample_id);

CREATE INDEX idx_descriptor_data_id ON descriptor(data_id);

CREATE TABLE descriptor_additional_type (descriptor_id TEXT NOT NULL REFERENCES descriptor(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), value TEXT NOT NULL, PRIMARY KEY(descriptor_id,position));

CREATE TABLE descriptor_annotation (descriptor_id TEXT NOT NULL REFERENCES descriptor(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), annotation_id TEXT NOT NULL REFERENCES annotation(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, PRIMARY KEY(descriptor_id,position));

CREATE TABLE formal_parameter (id TEXT NOT NULL, type TEXT NOT NULL, name TEXT, name_tan TEXT, default_value_id TEXT REFERENCES annotation(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, PRIMARY KEY(id), CHECK(type = 'FormalParameter'));

CREATE TABLE formal_parameter_additional_type (formal_parameter_id TEXT NOT NULL REFERENCES formal_parameter(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), value TEXT NOT NULL, PRIMARY KEY(formal_parameter_id,position));

CREATE TABLE organization (id TEXT NOT NULL, type TEXT NOT NULL, name TEXT NOT NULL, url TEXT, PRIMARY KEY(id), CHECK(type = 'Organization'));

CREATE TABLE organization_additional_type (organization_id TEXT NOT NULL REFERENCES organization(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), value TEXT NOT NULL, PRIMARY KEY(organization_id,position));

CREATE TABLE process (id TEXT NOT NULL, type TEXT NOT NULL, name TEXT NOT NULL, executes_recipe_id TEXT REFERENCES recipe(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, PRIMARY KEY(id), CHECK(type = 'Process'));

CREATE INDEX idx_process_executes_recipe_id ON process(executes_recipe_id);

CREATE TABLE process_additional_type (process_id TEXT NOT NULL REFERENCES process(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), value TEXT NOT NULL, PRIMARY KEY(process_id,position));

CREATE TABLE process_parameter_value (process_id TEXT NOT NULL REFERENCES process(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), annotation_id TEXT NOT NULL REFERENCES annotation(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, PRIMARY KEY(process_id,position));

CREATE TABLE recipe (id TEXT NOT NULL, type TEXT NOT NULL, name TEXT, description TEXT, intended_use_id TEXT REFERENCES defined_term(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, intended_use_text TEXT, version TEXT, url TEXT, PRIMARY KEY(id), CHECK(type = 'Recipe'), CHECK(intended_use_id IS NULL OR intended_use_text IS NULL));

CREATE INDEX idx_recipe_intended_use_id ON recipe(intended_use_id);

CREATE TABLE recipe_additional_type (recipe_id TEXT NOT NULL REFERENCES recipe(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), value TEXT NOT NULL, PRIMARY KEY(recipe_id,position));

CREATE TABLE recipe_parameter (recipe_id TEXT NOT NULL REFERENCES recipe(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), formal_parameter_id TEXT NOT NULL REFERENCES formal_parameter(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, PRIMARY KEY(recipe_id,position));

CREATE TABLE recipe_additional_property (recipe_id TEXT NOT NULL REFERENCES recipe(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), annotation_id TEXT NOT NULL REFERENCES annotation(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, PRIMARY KEY(recipe_id,position));

CREATE TABLE recipe_component (recipe_id TEXT NOT NULL REFERENCES recipe(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), annotation_id TEXT NOT NULL REFERENCES annotation(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, PRIMARY KEY(recipe_id,position));

CREATE TABLE sample (id TEXT NOT NULL, type TEXT NOT NULL, name TEXT NOT NULL, PRIMARY KEY(id), CHECK(type = 'Sample'));

CREATE TABLE sample_additional_type (sample_id TEXT NOT NULL REFERENCES sample(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), value TEXT NOT NULL, PRIMARY KEY(sample_id,position));

CREATE TABLE sample_additional_property (sample_id TEXT NOT NULL REFERENCES sample(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), annotation_id TEXT NOT NULL REFERENCES annotation(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, PRIMARY KEY(sample_id,position));

CREATE TABLE scholarly_article (id TEXT NOT NULL, type TEXT NOT NULL, headline TEXT NOT NULL, creative_work_status_id TEXT REFERENCES defined_term(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, PRIMARY KEY(id), CHECK(type = 'ScholarlyArticle'));

CREATE TABLE scholarly_article_additional_type (scholarly_article_id TEXT NOT NULL REFERENCES scholarly_article(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), value TEXT NOT NULL, PRIMARY KEY(scholarly_article_id,position));

CREATE TABLE scholarly_article_identifier (scholarly_article_id TEXT NOT NULL REFERENCES scholarly_article(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), value TEXT NOT NULL, PRIMARY KEY(scholarly_article_id,position));

CREATE TABLE scholarly_article_author (scholarly_article_id TEXT NOT NULL REFERENCES scholarly_article(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), agent_id TEXT NOT NULL REFERENCES agent(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, PRIMARY KEY(scholarly_article_id,position));

CREATE TABLE scholarly_article_additional_property (scholarly_article_id TEXT NOT NULL REFERENCES scholarly_article(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, position INTEGER NOT NULL CHECK(position >= 0), annotation_id TEXT NOT NULL REFERENCES annotation(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, PRIMARY KEY(scholarly_article_id,position));

CREATE TABLE process_io (process_id TEXT NOT NULL REFERENCES process(id) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED, direction TEXT NOT NULL, sample_id TEXT REFERENCES sample(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, data_id TEXT REFERENCES data(id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED, PRIMARY KEY(process_id,direction), CHECK(direction IN ('input','output')), CHECK((sample_id IS NOT NULL) + (data_id IS NOT NULL) = 1));

CREATE INDEX idx_process_io_sample_id ON process_io(sample_id);

CREATE INDEX idx_process_io_data_id ON process_io(data_id);

CREATE VIEW process_edges AS
SELECT consumed.process_id,
 CASE WHEN consumed.sample_id IS NOT NULL THEN 'sample' ELSE 'data' END AS input_kind,
 coalesce(consumed.sample_id,consumed.data_id) AS input_id,
 CASE WHEN produced.sample_id IS NOT NULL THEN 'sample' ELSE 'data' END AS output_kind,
 coalesce(produced.sample_id,produced.data_id) AS output_id
FROM process_io consumed JOIN process_io produced ON produced.process_id=consumed.process_id
 AND produced.direction='output' WHERE consumed.direction='input';

CREATE VIEW core_validation_errors AS
SELECT id AS dataset_id, 'identifiers must be nonempty' AS message FROM dataset d
 WHERE NOT EXISTS(SELECT 1 FROM dataset_identifier i WHERE i.dataset_id=d.id)
UNION ALL
SELECT id, 'conformsTo must declare a base profile' FROM dataset d
 WHERE NOT EXISTS(SELECT 1 FROM dataset_conforms_to p WHERE p.dataset_id=d.id
 AND p.value IN ('process-provenance','semantic-designation','administrative'))
UNION ALL
SELECT d.id, 'processes require process-provenance' FROM dataset d
 WHERE EXISTS(SELECT 1 FROM dataset_process r WHERE r.dataset_id=d.id)
 AND NOT EXISTS(SELECT 1 FROM dataset_conforms_to p WHERE p.dataset_id=d.id AND p.value='process-provenance')
UNION ALL
SELECT d.id, 'descriptors require semantic-designation' FROM dataset d
 WHERE EXISTS(SELECT 1 FROM dataset_descriptor r WHERE r.dataset_id=d.id)
 AND NOT EXISTS(SELECT 1 FROM dataset_conforms_to p WHERE p.dataset_id=d.id AND p.value='semantic-designation')
UNION ALL
SELECT d.id, 'administrative properties require administrative' FROM dataset d
 WHERE (d.title IS NOT NULL OR d.description IS NOT NULL OR d.license IS NOT NULL
 OR d.date_published IS NOT NULL OR d.date_created IS NOT NULL OR d.date_modified IS NOT NULL
 OR EXISTS(SELECT 1 FROM dataset_data_file r WHERE r.dataset_id=d.id)
 OR EXISTS(SELECT 1 FROM dataset_agent r WHERE r.dataset_id=d.id)
 OR EXISTS(SELECT 1 FROM dataset_citation r WHERE r.dataset_id=d.id))
 AND NOT EXISTS(SELECT 1 FROM dataset_conforms_to p WHERE p.dataset_id=d.id AND p.value='administrative');

COMMIT;

PRAGMA foreign_keys = ON;
