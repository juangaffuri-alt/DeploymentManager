--------ENCABEZADO DEL SCRIPT-----------------------------------
--INCIDENTE: 
--ANALISTA TECNICO: 
--ANALISTA FUNCIONAL:
--DESCRIPCION: Crea una tabla
--OBSERVACION: 
-----------------------------------------------------------------
 
IF OBJECT_ID('{schema}.{objectName}') IS NOT NULL --Si existe la Tabla
EXEC('DROP TABLE {schema}.{objectName};') -- Elimina la tabla
GO

CREATE TABLE {schema}.{objectName}

.
.
.
