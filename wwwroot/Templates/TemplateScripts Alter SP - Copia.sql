IF OBJECT_ID('{schema}.{objectName}') IS NULL --Si no existe el SP #required #defineSchemaAndObjectName
EXEC('CREATE PROCEDURE {schema}.{objectName} AS SET NOCOUNT ON;') -- CREA un SP con el mismo nombre pero vacio #required
GO #required

ALTER PROCEDURE {schema}.{objectName} -- modifica siempre el SP, ya sea el creado Vacio o el que ya Existe manteniendo sus permisos #required
@Parametro INT
AS #required
BEGIN #required
--Logica del SP
END #required
