--------ENCABEZADO DEL SCRIPT-----------------------------------
--INCIDENTE: 
--ANALISTA TECNICO: 
--ANALISTA FUNCIONAL:
--DESCRIPCION: Creación de funcion 
--OBSERVACION: 
-----------------------------------------------------------------

IF OBJECT_ID('{schema}.{objectName}') IS NULL --Si no existe la FN
EXEC('CREATE FUNCTION {schema}.{objectName} (@Parametro int) RETURNS int AS BEGIN select @Parametro=1 RETURN(@Parametro) END') -- CREA una FN con el mismo nombre pero vacia
GO

ALTER FUNCTION {schema}.{objectName} -- modifica siempre la FN, ya sea el creado Vacio o el que ya Existe manteniendo sus permisos
(@Parametro INT)
RETURNS int
AS
BEGIN
--Logica de la FN
END
