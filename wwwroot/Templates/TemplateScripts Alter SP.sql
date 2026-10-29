--------ENCABEZADO DEL SCRIPT-----------------------------------
--INCIDENTE: 
--ANALISTA TECNICO: 
--ANALISTA FUNCIONAL:
--DESCRIPCION: Creación de SP 
--OBSERVACION: 
-----------------------------------------------------------------

IF OBJECT_ID('{schema}.{objectName}') IS NULL --Si no existe el SP 
EXEC('CREATE PROCEDURE {schema}.{objectName} AS SET NOCOUNT ON;') -- CREA un SP con el mismo nombre pero vacio 
GO 

ALTER PROCEDURE {schema}.{objectName} -- modifica siempre el SP, ya sea el creado Vacio o el que ya Existe manteniendo sus permisos 
@Parametro INT
AS 
BEGIN 
--Logica del SP
END 
