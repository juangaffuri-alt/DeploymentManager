--------ENCABEZADO DEL SCRIPT-----------------------------------
--INCIDENTE: 
--ANALISTA TECNICO: 
--ANALISTA FUNCIONAL:
--DESCRIPCION: Creación de trigger 
--OBSERVACION: 
-----------------------------------------------------------------

IF OBJECT_ID('{schema}.{objectName}', 'TR') IS NULL --Si no existe el trigger
EXEC('CREATE TRIGGER {schema}.{objectName} ON {schema}.Tabla AFTER INSERT AS BEGIN SET NOCOUNT ON; END') -- CREA un trigger con el mismo nombre pero vacio
GO

ALTER TRIGGER {schema}.{objectName} -- modifica siempre el trigger, ya sea el creado Vacio o el que ya Existe manteniendo sus permisos
ON {schema}.Tabla
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
SET NOCOUNT ON;
--Logica del trigger
END
